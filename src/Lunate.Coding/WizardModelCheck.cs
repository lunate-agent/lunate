using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The model check's shared request path (design §4): one minimal real request through the
/// model's resolved API family. The interactive wizard and <c>lunate setup</c> both run it, so
/// a family or model id discovery accepted but a real request refuses fails in both entry points
/// the same way. A typed or referenced credential stays in memory and is redacted from any
/// failure detail; never logged.
/// </summary>
internal static class WizardModelCheck
{
    /// <summary>The request's model: a known catalog entry stays itself; the custom path builds
    /// the model on the entry's endpoint, placement and models URL; a preset rides its kind.</summary>
    internal static ModelInfo ModelFor(
        SetupWizardModel wizard,
        WizardModelChoice choice,
        ModelCatalog catalog
    )
    {
        WizardProviderChoice provider = wizard.Provider!;
        ModelInfo? known = WizardModelLookup.Resolve(provider, catalog, choice.ModelId).Match;
        if (provider.Custom)
        {
            _ = ProviderApiFamilies.TryParse(choice.ApiFamily, out ProviderApiFamily family);
            return new ModelInfo(
                choice.ModelId,
                CustomProvider.KindFor(family),
                provider.Endpoint,
                known?.ContextWindow ?? 0,
                known?.SupportsTools ?? false,
                AuthRef: provider.Id,
                Reasoning: known?.Reasoning
            )
            {
                Pricing = known?.Pricing,
                MaxOutput = known?.MaxOutput,
                SupportsVision = known?.SupportsVision ?? false,
                Capabilities = known?.Capabilities,
                ApiFamily = choice.ApiFamily,
                CredentialPlacement = provider.Placement.ToCompact(),
                ModelsUrl = provider.ModelsUrl,
                WireId = choice.ModelId,
            };
        }

        if (known is not null)
        {
            return known with
            {
                Provider = provider.Id,
                Endpoint = null,
                AuthRef = null,
                ApiFamily = choice.ApiFamily,
                CredentialPlacement = null,
                ProviderHeaders = null,
                WireId = known.WireId,
            };
        }

        return new ModelInfo(choice.ModelId, provider.Id, null, 0, false)
        {
            ApiFamily = choice.ApiFamily,
        };
    }

    /// <summary>
    /// The decision-profile check (S-13): one tiny noul through the decision client — the native
    /// System One transport for a native profile, the chat client under the decision layer's
    /// adapter for an adapter one. The request is the contract's own: a single yes/no question
    /// with the fixed answers and an empty state, so nothing a model could read as instructions
    /// rides along. A failure marks the profile unverified with the response's own words.
    /// <paramref name="context"/> names the identity the wire headers render over — the session's
    /// when one exists, null for a fresh ephemeral id (the flag path runs with no session). It must
    /// not be omitted: an OpenCode Go decision check outside a session otherwise throws on
    /// <c>x-opencode-session: {session.id}</c> before the first byte.
    /// </summary>
    internal static async Task<WizardCheckResult> RunDecisionAsync(
        SetupWizardModel wizard,
        ModelCatalog catalog,
        IChatClientFactory factory,
        AuthStore auth,
        Func<string, string?> environment,
        Func<string?> resolveCredential,
        RequestContext? context,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(wizard);
        WizardDecisionChoice decision =
            wizard.DecisionProfile
            ?? throw new InvalidDataException(
                "setup: the decision check needs the decision-profile answer."
            );
        ModelInfo checkModel = DecisionModelFor(wizard, decision, catalog);
        ProviderEntry entry = SetupWizardCommit.StagedEntry(wizard);
        string? credential = resolveCredential();
        RequestContext identity = context ?? RequestContext.Ephemeral();
        try
        {
            // The same resolution the session's decision usage takes, over the entry this walk is
            // about to store: a native profile builds the System One transport, an adapter one
            // the provider's chat client that the decision layer wraps.
            ResolvedModelProfile profile = new(
                new ModelProfile(
                    decision.ProfileName,
                    entry.Name,
                    decision.ModelId,
                    ApiFamily: decision.Native ? ProviderApiFamilies.SystemOne : null
                ),
                entry,
                checkModel,
                checkModel.ContextWindow,
                checkModel.Capabilities ?? new ModelCapabilityDescriptor()
            );
            IChatClient client = DecisionUsageModels.CreateUsageClient(
                profile,
                auth,
                environment,
                factory,
                identity,
                credentialOverride: credential
            );
            await DecisionEngine
                .EvaluateAsync(
                    DecisionCheckRequest(),
                    client,
                    checkModel.Id,
                    decision.Native ? "native" : "chat",
                    ct
                )
                .ConfigureAwait(false);
            return new WizardCheckResult(WizardProgress.Passed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            string reason = exception.Message;
            if (credential is { Length: > 0 })
            {
                reason = reason.Replace(credential, "[redacted]", StringComparison.Ordinal);
            }

            return new WizardCheckResult(WizardProgress.Failed, reason);
        }
    }

    /// <summary>
    /// The decision check's request: one noul, the fixed yes/no answers the contract requires and
    /// an empty state. The question id is the check's own, so a decision transport's reply is read
    /// against exactly one question.
    /// </summary>
    private static Lunate.Contracts.DecisionRequest DecisionCheckRequest()
    {
        using JsonDocument state = JsonDocument.Parse("{}");
        return new(
            state.RootElement.Clone(),
            [
                new Lunate.Contracts.DecisionQuestion(
                    "transport_reachable",
                    "Answer yes when the decision transport answered this request.",
                    Lunate.Contracts.DecisionQuestionKind.YesNo
                ),
            ]
        );
    }

    /// <summary>
    /// The model the decision check sends through: an adapter profile borrows the chat model's
    /// choice (the chat client answers the decision JSON), while a native profile is the catalog's
    /// Jev entry — its own wire id, the preset's System One URL and the decision-only family.
    /// </summary>
    internal static ModelInfo DecisionModelFor(
        SetupWizardModel wizard,
        WizardDecisionChoice decision,
        ModelCatalog catalog
    )
    {
        WizardProviderChoice provider = wizard.Provider!;
        if (!decision.Native)
        {
            return ModelFor(
                wizard,
                new WizardModelChoice(decision.ModelId, AdapterFamily(wizard, provider)),
                catalog
            );
        }

        ProviderPreset? preset = ProviderPresets.Find(provider.Id);
        if (preset is null)
        {
            throw new InvalidDataException(
                $"setup: provider '{provider.Id}' is not a preset; its native decision profile "
                    + "cannot resolve the System One transport."
            );
        }

        ModelInfo model =
            WizardModelLookup.Resolve(provider, catalog, decision.ModelId).Match
            ?? new ModelInfo(decision.ModelId, provider.Id, null, 0, false);
        return model with
        {
            Provider = provider.Id,
            Endpoint = preset.UrlFor(ProviderApiFamily.SystemOne) ?? preset.BaseUrl,
            ApiFamily = ProviderApiFamilies.SystemOne,
            WireId = model.WireId ?? model.Id,
            AuthRef = null,
            CredentialPlacement = null,
            ProviderHeaders = null,
        };
    }

    /// <summary>The family an adapter decision profile answers on: the chat model's, falling back
    /// to the provider answer's own when the walk recorded none.</summary>
    private static string AdapterFamily(SetupWizardModel wizard, WizardProviderChoice provider) =>
        wizard.Model?.ApiFamily
        ?? (
            provider.Custom ? provider.ApiFamily
            : ProviderPresets.Find(provider.Id) is { } preset
                ? ProviderApiFamilies.Name(preset.DefaultFamily)
            : ProviderApiFamilies.Name(ProviderApiFamily.OpenAiChat)
        );

    /// <summary>
    /// The minimal request: one user message and one output token through the factory, the way
    /// <c>/auth</c>'s verification sends it. <paramref name="resolveCredential"/> supplies the
    /// resolved key (a typed or referenced value, never an argument); null builds the client
    /// without an explicit key, which resolves the credential the factory already knows.
    /// <paramref name="context"/> names the identity the wire headers render over — the session's
    /// when one exists, null for a fresh ephemeral id (the flag path runs with no session). It must
    /// not be omitted: an OpenCode Go check outside a session otherwise throws on
    /// <c>x-opencode-session: {session.id}</c> before the first byte.
    /// </summary>
    internal static async Task<WizardCheckResult> RunAsync(
        SetupWizardModel wizard,
        ModelCatalog catalog,
        IChatClientFactory factory,
        AuthStore auth,
        Func<string, string?> environment,
        Func<string?> resolveCredential,
        RequestContext? context,
        CancellationToken ct
    )
    {
        WizardModelChoice choice = wizard.Model!;
        WizardProviderChoice provider = wizard.Provider!;
        ProviderEntry entry = SetupWizardCommit.StagedEntry(wizard);
        string? credential = resolveCredential();
        RequestContext identity = context ?? RequestContext.Ephemeral();
        try
        {
            // The check resolves the staged profile through the same provider entry and family
            // path the first session uses. A custom provider's model is not in the built-in
            // catalog, so its ModelFor result supplies the endpoint and wire id; a preset model
            // goes through ModelProfileResolver, which keeps the catalog's unqualified wire id.
            ResolvedModelProfile profile;
            ModelInfo? known = WizardModelLookup.Resolve(provider, catalog, choice.ModelId).Match;
            if (
                provider.Custom
                || known is null
                || !string.Equals(known.Provider, entry.Kind, StringComparison.Ordinal)
            )
            {
                // A custom endpoint, a model discovery listed but the catalog does not know, or a
                // qualified id the catalog resolves to a different provider has no catalog entry
                // the resolver can validate for this provider. ModelFor supplies the staged
                // endpoint/family/wire id; the provider entry still comes from the commit.
                ModelInfo stagedModel = ModelFor(wizard, choice, catalog);
                profile = new ResolvedModelProfile(
                    new ModelProfile(
                        "setup-check",
                        provider.Id,
                        choice.ModelId,
                        ApiFamily: choice.ApiFamily
                    ),
                    entry,
                    stagedModel,
                    stagedModel.ContextWindow,
                    stagedModel.Capabilities ?? new ModelCapabilityDescriptor()
                );
            }
            else
            {
                profile = ModelProfileResolver.Resolve(
                    new ModelProfile(
                        "setup-check",
                        provider.Id,
                        choice.ModelId,
                        ApiFamily: choice.ApiFamily
                    ),
                    [entry],
                    catalog
                );
            }

            IChatClient client = DecisionUsageModels.CreateUsageClient(
                profile,
                auth,
                environment,
                factory,
                identity,
                credentialOverride: credential
            );
            await client
                .GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "Reply with OK.")],
                    new ChatOptions { MaxOutputTokens = 16 },
                    ct
                )
                .ConfigureAwait(false);
            return new WizardCheckResult(WizardProgress.Passed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            string reason = exception.Message;
            if (credential is { Length: > 0 })
            {
                reason = reason.Replace(credential, "[redacted]", StringComparison.Ordinal);
            }

            return new WizardCheckResult(WizardProgress.Failed, reason);
        }
    }
}
