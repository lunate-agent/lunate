using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

/// <summary>
/// The model check's shared request path (design §4): one minimal real request through the
/// model's resolved API family. The interactive wizard and <c>lunate setup</c> both run it, so
/// a family or model id discovery accepted but a real request refuses fails in both entry points
/// the same way. Every overload takes a <see cref="WizardModelRef"/> — the provider that owns the
/// model, its entry and the credential the walk holds — so a fast or decision usage that lives on
/// another provider is checked against that provider, not the walk's own. A typed or referenced
/// credential stays in memory and is redacted from any failure detail; never logged.
/// </summary>
internal static class WizardModelCheck
{
    /// <summary>The request's model: a known catalog entry stays itself; the custom path builds
    /// the model on the entry's endpoint, placement and models URL; a preset rides its kind.</summary>
    internal static ModelInfo ModelFor(WizardModelRef model, ModelCatalog catalog)
    {
        WizardProviderChoice provider = model.Provider;
        WizardModelChoice choice = model.Model;
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

    /// <summary>The flag path's model: the walk's own provider, credential and staged entry.</summary>
    internal static ModelInfo ModelFor(
        SetupWizardModel wizard,
        WizardModelChoice choice,
        ModelCatalog catalog
    ) => ModelFor(RefFor(wizard, choice), catalog);

    /// <summary>The staged model as a check ref: the walk's provider answer, its credential and
    /// the entry the commit will write.</summary>
    internal static WizardModelRef RefFor(SetupWizardModel wizard, WizardModelChoice choice) =>
        new(wizard.Provider!, wizard.Credential, SetupWizardCommit.StagedEntry(wizard), choice);

    /// <summary>
    /// The decision check's request model: an adapter profile borrows the chat model's choice (the
    /// chat client answers the decision JSON), while a native profile is the catalog's Jev entry —
    /// its own wire id, the provider entry's System One URL and the decision-only family.
    /// </summary>
    internal static ModelInfo DecisionModelFor(WizardModelRef model, ModelCatalog catalog)
    {
        if (!model.Native)
        {
            return ModelFor(
                model with
                {
                    Model = new WizardModelChoice(model.Model.ModelId, AdapterFamily(model)),
                },
                catalog
            );
        }

        ProviderPreset? preset = ProviderPresets.Find(model.Provider.Id);
        if (preset is null)
        {
            throw new InvalidDataException(
                $"setup: provider '{model.Provider.Id}' is not a preset; its native decision profile "
                    + "cannot resolve the System One transport."
            );
        }

        ModelInfo modelInfo =
            WizardModelLookup.Resolve(model.Provider, catalog, model.Model.ModelId).Match
            ?? new ModelInfo(model.Model.ModelId, model.Provider.Id, null, 0, false);
        return modelInfo with
        {
            Provider = model.Provider.Id,
            Endpoint =
                model.Entry.UrlFor(ProviderApiFamily.SystemOne)
                ?? preset.UrlFor(ProviderApiFamily.SystemOne)
                ?? preset.BaseUrl,
            ApiFamily = ProviderApiFamilies.SystemOne,
            WireId = modelInfo.WireId ?? modelInfo.Id,
            AuthRef = null,
            CredentialPlacement = null,
            ProviderHeaders = null,
        };
    }

    /// <summary>The flag path's and summary's decision model: the walk's own provider.</summary>
    internal static ModelInfo DecisionModelFor(
        SetupWizardModel wizard,
        WizardUsageChoice decision,
        ModelCatalog catalog
    ) => DecisionModelFor(DecisionRef(wizard, decision), catalog);

    /// <summary>The decision answer as a check ref over the walk's own provider and entry.</summary>
    internal static WizardModelRef DecisionRef(
        SetupWizardModel wizard,
        WizardUsageChoice decision
    ) =>
        new(
            wizard.Provider!,
            wizard.Credential,
            SetupWizardCommit.StagedEntry(wizard),
            new WizardModelChoice(decision.ModelId, decision.ApiFamily),
            decision.Native,
            decision.PinnedVersion
        );

    /// <summary>The family an adapter decision profile answers on: the model's own family, falling
    /// back to the provider answer's own when the walk recorded none.</summary>
    private static string AdapterFamily(WizardModelRef model) =>
        model.Model.ApiFamily
        ?? (
            model.Provider.Custom
                ? model.Provider.ApiFamily
                : ProviderApiFamilies.Name(model.Entry.DefaultFamily)
        );

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
    internal static async Task<WizardCheckResult> RunDecisionForAsync(
        WizardModelRef model,
        ModelCatalog catalog,
        IChatClientFactory factory,
        AuthStore auth,
        Func<string, string?> environment,
        string? credentialOverride,
        RequestContext? context,
        CancellationToken ct
    )
    {
        ModelInfo checkModel = DecisionModelFor(model, catalog);
        ProviderEntry entry = model.Entry;
        string? credential = credentialOverride;
        try
        {
            credential = CheckCredential(entry, auth, environment, credentialOverride);
            // The same resolution the session's decision usage takes, over the entry this walk is
            // about to store: a native profile builds the System One transport, an adapter one
            // the provider's chat client that the decision layer wraps.
            ResolvedModelProfile profile = new(
                new ModelProfile(
                    "decisions",
                    entry.Name,
                    model.Model.ModelId,
                    ApiFamily: model.Native ? ProviderApiFamilies.SystemOne : null
                ),
                entry,
                checkModel,
                checkModel.ContextWindow,
                checkModel.Capabilities ?? new ModelCapabilityDescriptor()
            );
            // The System One wire carries the session id header; a check outside a session (the
            // flag path) renders it over an ephemeral identity rather than failing before the wire.
            IChatClient client = DecisionUsageModels.CreateUsageClient(
                profile,
                auth,
                environment,
                factory,
                context ?? RequestContext.Ephemeral(),
                credentialOverride: credential
            );
            await DecisionEngine
                .EvaluateAsync(
                    DecisionCheckRequest(),
                    client,
                    checkModel.Id,
                    model.Native ? "native" : "chat",
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
            return new WizardCheckResult(
                WizardProgress.Failed,
                Redact(exception.Message, credential)
            );
        }
    }

    /// <summary>The flag path's decision check over the walk's own provider.</summary>
    internal static Task<WizardCheckResult> RunDecisionAsync(
        SetupWizardModel wizard,
        ModelCatalog catalog,
        IChatClientFactory factory,
        AuthStore auth,
        Func<string, string?> environment,
        Func<string?> resolveCredential,
        RequestContext? context,
        CancellationToken ct
    ) =>
        RunDecisionForAsync(
            DecisionRef(wizard, wizard.DecisionProfile!),
            catalog,
            factory,
            auth,
            environment,
            resolveCredential(),
            context,
            ct
        );

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
    /// The chat model check for one ref: one short completion through the factory on the model's
    /// own provider entry. The cost is printed by the caller before this runs; a skipped or failed
    /// check is the caller's to mark unverified. <paramref name="context"/> names the identity the
    /// wire headers render over — the session's when one exists, null for a fresh ephemeral id (the
    /// flag path runs with no session). It must not be omitted: an OpenCode Go check outside a
    /// session otherwise throws on <c>x-opencode-session: {session.id}</c> before the first byte.
    /// </summary>
    internal static async Task<WizardCheckResult> RunForAsync(
        WizardModelRef model,
        ModelCatalog catalog,
        IChatClientFactory factory,
        AuthStore auth,
        Func<string, string?> environment,
        string? credentialOverride,
        RequestContext? context,
        CancellationToken ct
    )
    {
        WizardProviderChoice provider = model.Provider;
        WizardModelChoice choice = model.Model;
        ProviderEntry entry = model.Entry;
        RequestContext identity = context ?? RequestContext.Ephemeral();
        string? credential = credentialOverride;
        try
        {
            credential = CheckCredential(entry, auth, environment, credentialOverride);
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
                ModelInfo stagedModel = ModelFor(model, catalog);
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
            return new WizardCheckResult(
                WizardProgress.Failed,
                Redact(exception.Message, credential)
            );
        }
    }

    /// <summary>The flag path's chat model check over the walk's own provider.</summary>
    internal static Task<WizardCheckResult> RunAsync(
        SetupWizardModel wizard,
        ModelCatalog catalog,
        IChatClientFactory factory,
        AuthStore auth,
        Func<string, string?> environment,
        Func<string?> resolveCredential,
        RequestContext? context,
        CancellationToken ct
    ) =>
        RunForAsync(
            RefFor(wizard, wizard.Model!),
            catalog,
            factory,
            auth,
            environment,
            resolveCredential(),
            context,
            ct
        );

    /// <summary>
    /// The key the check's client will send, so a failure detail can redact exactly that value: the
    /// caller's held key when there is one, otherwise the entry's own credential reference resolved
    /// the way <see cref="DecisionUsageModels.CreateUsageClient"/> resolves it — a preset's
    /// environment variable or alias first, then the auth store; an <c>env:</c> reference names its
    /// variable; any other reference leaves this host no key to send. A configured provider's ref
    /// carries no credential, so without this the stored key would escape redaction.
    /// </summary>
    private static string? CheckCredential(
        ProviderEntry entry,
        AuthStore auth,
        Func<string, string?> environment,
        string? credentialOverride
    )
    {
        if (credentialOverride is { Length: > 0 } held)
        {
            return held;
        }

        string reference = entry.CredentialReference;
        if (reference.StartsWith("auth:", StringComparison.Ordinal))
        {
            string keyName = reference["auth:".Length..];
            ProviderPreset? preset = ProviderPresets.Find(entry.Kind);
            string? environmentKey = preset is null
                ? null
                : environment(preset.KeyVariable)
                    ?? (preset.KeyVariableAlias is { } alias ? environment(alias) : null);
            if (!string.IsNullOrWhiteSpace(environmentKey))
            {
                return environmentKey;
            }

            string? storedKey = auth.TryGet(keyName);
            return string.IsNullOrWhiteSpace(storedKey) ? null : storedKey;
        }

        if (reference.StartsWith("env:", StringComparison.Ordinal))
        {
            string? environmentKey = environment(reference["env:".Length..]);
            return string.IsNullOrWhiteSpace(environmentKey) ? null : environmentKey;
        }

        return null;
    }

    private static string Redact(string reason, string? credential) =>
        credential is { Length: > 0 }
            ? reason.Replace(credential, "[redacted]", StringComparison.Ordinal)
            : reason;
}
