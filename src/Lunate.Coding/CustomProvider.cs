using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// A user-declared provider endpoint, stored as a <c>providers</c> settings entry. The key lives
/// only in <c>auth.json</c> under <see cref="Id"/>; <see cref="CatalogName"/> is the model id the
/// catalog exposes (default <c>&lt;id&gt;/&lt;model&gt;</c>). The entry serves exactly one API
/// family (<see cref="ApiFamily"/>, default <c>openai-chat</c> for the OpenAI-compatible path the
/// entry shape grew out of), declares how the credential is sent
/// (<see cref="CredentialPlacement"/>, default bearer) and may name a discovery
/// <see cref="ModelsUrl"/>; the written shape is the provider entry v2 the presets use.
/// </summary>
public sealed record CustomProvider(
    string Id,
    Uri BaseUrl,
    string Model,
    string CatalogName,
    ProviderApiFamily ApiFamily = ProviderApiFamily.OpenAiChat,
    CredentialPlacement? CredentialPlacement = null,
    Uri? ModelsUrl = null,
    IReadOnlyDictionary<string, string>? Headers = null
)
{
    /// <summary>
    /// Validates and trims the entered fields. The URLs must be absolute and <c>https</c>, or
    /// <c>http</c> only for a loopback host; the provider id must not contain spaces or a slash
    /// (it prefixes the catalog name) and must not shadow a built-in preset; the API family and
    /// credential placement parse by their wire names and an unknown one is refused by name.
    /// Error messages never echo an entered value except the refused scheme.
    /// </summary>
    internal static bool TryCreate(
        string? id,
        string? baseUrl,
        string? model,
        string? catalogName,
        out CustomProvider? provider,
        out string? error,
        string? apiFamily = null,
        CredentialPlacement? credentialPlacement = null,
        string? modelsUrl = null,
        IReadOnlyDictionary<string, string>? headers = null
    )
    {
        provider = null;
        string? trimmedId = id?.Trim();
        if (string.IsNullOrEmpty(trimmedId))
        {
            error = "the provider id is required";
            return false;
        }

        if (trimmedId.Any(char.IsWhiteSpace) || trimmedId.Contains('/'))
        {
            error = "the provider id must not contain spaces or '/'";
            return false;
        }

        if (ProviderPresets.Find(trimmedId) is not null)
        {
            error = "the provider id is a built-in provider; choose a different id";
            return false;
        }

        if (
            !ProviderApiFamilies.TryParse(apiFamily, out ProviderApiFamily family)
            && apiFamily is not null
        )
        {
            error = ProviderApiFamilies.Unsupported(apiFamily, "--api-family");
            return false;
        }

        string? trimmedBaseUrl = baseUrl?.Trim();
        if (string.IsNullOrEmpty(trimmedBaseUrl))
        {
            error = "the base URL is required";
            return false;
        }

        if (!TryParseEndpoint(trimmedBaseUrl, out Uri? uri, out string? urlError))
        {
            error = urlError;
            return false;
        }

        Uri? parsedModelsUrl = null;
        if (
            modelsUrl is not null
            && (
                !TryParseEndpoint(modelsUrl.Trim(), out parsedModelsUrl, out string? modelsError)
                || parsedModelsUrl is null
            )
        )
        {
            error = modelsError;
            return false;
        }

        string? trimmedModel = model?.Trim();
        if (string.IsNullOrEmpty(trimmedModel))
        {
            error = "the model id is required";
            return false;
        }

        Dictionary<string, string>? copiedHeaders = null;
        if (headers is { Count: > 0 })
        {
            copiedHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((string name, string value) in headers)
            {
                if (!ProviderHeaderValues.TryValidate(name, value, out string? headerError))
                {
                    error = $"the header '{name}' is refused: {headerError}";
                    return false;
                }

                copiedHeaders.Add(name, value);
            }
        }

        string resolvedCatalogName = catalogName?.Trim() is { Length: > 0 } explicitName
            ? explicitName
            : $"{trimmedId}/{trimmedModel}";
        provider = new CustomProvider(
            trimmedId,
            uri!,
            trimmedModel,
            resolvedCatalogName,
            family,
            credentialPlacement,
            parsedModelsUrl,
            copiedHeaders
        );
        error = null;
        return true;
    }

    /// <summary>
    /// The catalog entry the stored provider contributes. It routes through the preset kind the
    /// family's wire speaks (the Anthropic SDK for <c>anthropic-messages</c>, the OpenAI SDK for
    /// the OpenAI families), carries the family so the adapter is selected from it, and names its
    /// auth-store key, which is the explicit opt-in that lets that key reach the declared
    /// endpoint.
    /// </summary>
    internal ModelInfo ToModelInfo() =>
        new(
            CatalogName,
            KindFor(ApiFamily),
            BaseUrl,
            ContextWindow: 0,
            SupportsTools: true,
            AuthRef: Id
        )
        {
            // The catalog id is what /model shows; the upstream endpoint receives the model id.
            WireId = Model,
            ApiFamily = ProviderApiFamilies.Name(ApiFamily),
            CredentialPlacement = CredentialPlacement?.ToCompact(),
            ModelsUrl = ModelsUrl,
            ProviderHeaders = Headers,
        };

    /// <summary>
    /// The preset kind whose adapter speaks the family's wire: the Anthropic SDK for
    /// <c>anthropic-messages</c>, the Google SDK for <c>google</c>, the OpenAI SDK for both
    /// OpenAI families.
    /// </summary>
    internal static string KindFor(ProviderApiFamily family) =>
        family switch
        {
            ProviderApiFamily.AnthropicMessages => "anthropic",
            ProviderApiFamily.Google => "google",
            _ => "openai",
        };

    private static bool TryParseEndpoint(string value, out Uri? uri, out string? error)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed))
        {
            error = "the URL must be an absolute URL";
            return false;
        }

        bool isHttps = string.Equals(
            parsed.Scheme,
            Uri.UriSchemeHttps,
            StringComparison.OrdinalIgnoreCase
        );
        bool isLoopbackHttp =
            string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && parsed.IsLoopback;
        if (!isHttps && !isLoopbackHttp)
        {
            error =
                $"the URL scheme '{parsed.Scheme}' is not supported; use https, or http for a loopback host";
            return false;
        }

        uri = parsed;
        error = null;
        return true;
    }
}
