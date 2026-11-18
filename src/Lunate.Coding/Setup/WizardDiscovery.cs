using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>The shapes a failed discovery can take; the failure text names which and when.</summary>
internal enum WizardFailureKind
{
    /// <summary>The request did not reach the endpoint (connection, DNS, timeout).</summary>
    Network,

    /// <summary>The endpoint answered 401 or 403: the credential was sent and refused.</summary>
    Authentication,

    /// <summary>An HTTP status other than the authentication pair, named in the message.</summary>
    Rejected,

    /// <summary>A response body that could not be read as a model listing.</summary>
    Undecipherable,
}

/// <summary>One discovered model, as the listing's own data names it: the id, plus the metadata the
/// model step's picker and the expensive re-checks use.</summary>
/// <param name="Id">The listing's model id (the wire form a custom endpoint serves).</param>
/// <param name="ApiFamily">The listing's API family when it declares one.</param>
/// <param name="Reasoning">The declared reasoning style; null when none.</param>
/// <param name="ContextWindow">The declared context window; null when unknown.</param>
internal sealed record WizardModelListing(
    string Id,
    string? ApiFamily,
    string? Reasoning,
    int? ContextWindow
);

/// <summary>The discovery run's outcome: a listing on success, a classified failure otherwise.</summary>
internal sealed record WizardDiscoveryResult(
    IReadOnlyList<WizardModelListing> Models,
    WizardFailureKind? FailureKind,
    string? Error
)
{
    public bool Passed => FailureKind is null;
}

/// <summary>
/// The wizard's connection test and model listing (design §3): one real discovery request through
/// the same discovery client <c>--discover</c> uses, with the credential the credential step
/// resolved. The transport, the clock and the key lookup are injectable, so fixtures exercise every
/// failure shape offline; the request rides the provider entry's models URL (the preset's own
/// discovery URL when it declares one) and nothing is written. There is no retry here: the
/// go-back loop is the retry the user controls.
/// </summary>
internal static class WizardDiscovery
{
    /// <summary>
    /// Runs discovery for the walked provider: a preset hitches the command's own preset
    /// resolution; the custom path targets one synthesised catalog entry so the endpoint's auth
    /// reference, models URL and credential placement ride the shipped request path. The typed
    /// and referenced credentials resolve through the named-key source, never an argument.
    /// </summary>
    public static async Task<WizardDiscoveryResult> RunAsync(
        WizardProviderChoice provider,
        WizardCredentialChoice? credential,
        Func<string, string?> environment,
        Func<string, string?>? namedKeySource = null,
        HttpMessageHandler? handler = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(environment);

        string? ResolveKey(string name) =>
            name == provider.Id
                ? CredentialValue(provider, credential, environment, namedKeySource)
                : namedKeySource?.Invoke(name);

        DiscoverResult result = provider.Custom
            ? await DiscoverCommand
                .RunAsync(
                    provider.Id,
                    SingleCatalog(provider),
                    handler,
                    ResolveKey,
                    environment,
                    ct
                )
                .ConfigureAwait(false)
            : await DiscoverCommand
                .RunAsync(
                    provider.PresetKey,
                    ModelCatalog.Load(null),
                    handler,
                    ResolveKey,
                    environment,
                    ct
                )
                .ConfigureAwait(false);

        return result.Succeeded ? List(result.Draft) : Classify(result.Error ?? "discovery failed");
    }

    /// <summary>The credential the discovery request sends, resolved like the auth picker's probe:
    /// the typed key first, then the referenced source (`env:` variable, `auth:` stored key).</summary>
    private static string? CredentialValue(
        WizardProviderChoice provider,
        WizardCredentialChoice? credential,
        Func<string, string?> environment,
        Func<string, string?>? namedKeySource
    )
    {
        if (credential is null)
        {
            return null;
        }

        if (credential.Key is { Length: > 0 } typed)
        {
            return typed;
        }

        if (credential.Reference.StartsWith("env:", StringComparison.Ordinal))
        {
            string variable = credential.Reference["env:".Length..];
            return environment(variable);
        }

        if (credential.Reference.StartsWith("auth:", StringComparison.Ordinal))
        {
            return namedKeySource?.Invoke(credential.Reference["auth:".Length..]);
        }

        return namedKeySource?.Invoke(provider.Id);
    }

    /// <summary>
    /// One synthesised entry for the custom provider: the endpoint, the id the credential is
    /// stored under as the auth reference, the declared placement and models URL.
    /// </summary>
    private static ModelCatalog SingleCatalog(WizardProviderChoice provider) =>
        ModelCatalog.Single(
            new ModelInfo(
                provider.Id,
                provider.Id,
                provider.Endpoint,
                ContextWindow: 0,
                SupportsTools: false,
                AuthRef: provider.Id,
                Reasoning: null
            )
            {
                CredentialPlacement = provider.Placement.ToCompact(),
                ModelsUrl = provider.ModelsUrl,
            }
        );

    /// <summary>Reads the command's draft — the models.json shape the discovery response parsed to —
    /// for the listing's ids and metadata.</summary>
    private static WizardDiscoveryResult List(string draft)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(draft);
            var models = new List<WizardModelListing>();
            if (document.RootElement.TryGetProperty("models", out JsonElement entries))
            {
                foreach (JsonElement entry in entries.EnumerateArray())
                {
                    string? id = entry.TryGetProperty("id", out JsonElement idElement)
                        ? idElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    models.Add(
                        new WizardModelListing(
                            id,
                            TextOrNull(entry, "apiFamily"),
                            TextOrNull(entry, "reasoning"),
                            entry.TryGetProperty("contextWindow", out JsonElement window)
                            && window.ValueKind == JsonValueKind.Number
                                ? window.GetInt32()
                                : null
                        )
                    );
                }
            }

            return new WizardDiscoveryResult(models, null, null);
        }
        catch (JsonException exception)
        {
            return Fail(WizardFailureKind.Undecipherable, exception.Message);
        }
    }

    private static string? TextOrNull(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>Classifies the command's failure message: what failed, named for the go-back notice.</summary>
    private static WizardDiscoveryResult Classify(string message)
    {
        if (
            message.Contains("HTTP 401", StringComparison.Ordinal)
            || message.Contains("HTTP 403", StringComparison.Ordinal)
        )
        {
            return Fail(WizardFailureKind.Authentication, message);
        }

        if (message.Contains("HTTP ", StringComparison.Ordinal))
        {
            return Fail(WizardFailureKind.Rejected, message);
        }

        if (
            message.Contains("not valid JSON", StringComparison.Ordinal)
            || message.Contains("does not contain", StringComparison.Ordinal)
            || message.Contains("without an id", StringComparison.Ordinal)
        )
        {
            return Fail(WizardFailureKind.Undecipherable, message);
        }

        return Fail(WizardFailureKind.Network, message);
    }

    private static WizardDiscoveryResult Fail(WizardFailureKind kind, string message) =>
        new([], kind, message);
}
