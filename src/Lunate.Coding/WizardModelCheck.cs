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
        ModelInfo? known = catalog.Find(choice.ModelId);
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
                WireId = choice.ModelId,
            };
        }

        return new ModelInfo(choice.ModelId, provider.Id, null, 0, false)
        {
            ApiFamily = choice.ApiFamily,
        };
    }

    /// <summary>
    /// The minimal request: one user message and one output token through the factory, the way
    /// <c>/auth</c>'s verification sends it. <paramref name="resolveCredential"/> supplies the
    /// resolved key (a typed or referenced value, never an argument); null builds the client
    /// without an explicit key, which resolves the credential the factory already knows.
    /// </summary>
    internal static async Task<WizardCheckResult> RunAsync(
        SetupWizardModel wizard,
        ModelCatalog catalog,
        IChatClientFactory factory,
        Func<string?> resolveCredential,
        CancellationToken ct
    )
    {
        WizardModelChoice choice = wizard.Model!;
        ModelInfo checkModel = ModelFor(wizard, choice, catalog);
        string? credential = resolveCredential();
        try
        {
            IChatClient client = credential is { Length: > 0 }
                ? factory.Create(checkModel, credential)
                : factory.Create(checkModel);
            await client
                .GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "Reply with OK.")],
                    new ChatOptions { MaxOutputTokens = 1 },
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
