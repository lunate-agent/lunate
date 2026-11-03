using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>The outcome of a discovery run: the draft on success, an actionable message otherwise.</summary>
public sealed record DiscoverResult(bool Succeeded, string Draft, string? Error);

/// <summary>
/// The <c>--discover</c> helper: lists an OpenAI-compatible endpoint's models
/// (<c>GET {base}/models</c>, response shape <c>{ "data": [ { "id": ... } ] }</c>) and returns a
/// <c>models.json</c>-shaped draft for the user catalog. Nothing is ever written to disk; the
/// transport, the key lookup and the environment lookup are injectable, so tests are fully offline.
/// </summary>
public static class DiscoverCommand
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions DraftJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Runs discovery for a catalog name or an http(s) URL.</summary>
    public static async Task<DiscoverResult> RunAsync(
        string target,
        ModelCatalog catalog,
        HttpMessageHandler? handler = null,
        Func<string, string?>? namedKeySource = null,
        Func<string, string?>? environment = null,
        CancellationToken ct = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(catalog);

        string? provider = null;
        string? authRef = null;
        bool customEndpoint = false;
        bool qualified = false;
        ProviderPreset? credentialPreset = null;
        CredentialPlacement? declaredPlacement = null;
        IReadOnlyDictionary<string, string>? entryHeaders = null;
        Uri? modelsUrl = null;
        string baseUrl;

        if (IsHttpUrl(target, out string url))
        {
            baseUrl = TrimTrailingSlash(url);
            customEndpoint = true;
            // A direct URL is the provider's own endpoint only when it is exactly a preset base URL;
            // anything else is foreign and gets no credential.
            credentialPreset = PresetAtUrl(baseUrl);
            provider = credentialPreset?.Id;
        }
        else if (ProviderPresets.Find(target) is { } presetTarget)
        {
            // A preset name is a provider target even when no catalog entry carries that id.
            provider = presetTarget.Id;
            credentialPreset = presetTarget;
            qualified = presetTarget.Adapter == ProviderAdapter.OpenAiCompatible;
            baseUrl = TrimTrailingSlash(presetTarget.BaseUrl.AbsoluteUri);
        }
        else
        {
            ModelInfo? model;
            try
            {
                model = catalog.Find(target);
            }
            catch (InvalidOperationException exception)
            {
                // An ambiguous bare id names its candidates; discovery reports, never guesses.
                return Failure(exception.Message);
            }

            if (model is null)
            {
                return Failure(
                    $"Unknown model or preset '{target}'. Add it to ~/.lunate/models.json, name a preset, or pass an http(s) URL."
                );
            }

            provider = model.Provider;
            authRef = model.AuthRef;
            entryHeaders = model.ProviderHeaders;
            if (model.CredentialPlacement is { } declared)
            {
                if (
                    !CredentialPlacement.TryParseCompact(
                        declared,
                        out CredentialPlacement? parsed,
                        out string? placementError
                    )
                )
                {
                    return Failure(
                        $"Model '{target}' declares an unusable credentialPlacement '{declared}': {placementError}"
                    );
                }

                declaredPlacement = parsed;
            }

            modelsUrl = model.ModelsUrl;
            if (model.Endpoint is not null)
            {
                baseUrl = TrimTrailingSlash(model.Endpoint.AbsoluteUri);
                customEndpoint = true;
                if (
                    ProviderPresets.Find(model.Provider) is { } endpointPreset
                    && EndpointBelongsToPreset(model.Endpoint, endpointPreset)
                )
                {
                    // The provider's own endpoint keeps the preset's credential precedence; a
                    // declared endpoint anywhere else only gets a key through an explicit auth ref.
                    credentialPreset = endpointPreset;
                }
            }
            else if (ProviderPresets.Find(model.Provider) is { } modelPreset)
            {
                // Native presets keep bare ids; the OpenAI-compatible ones emit qualified ids.
                credentialPreset = modelPreset;
                qualified = modelPreset.Adapter == ProviderAdapter.OpenAiCompatible;
                baseUrl = TrimTrailingSlash(modelPreset.BaseUrl.AbsoluteUri);
            }
            else
            {
                return Failure(
                    $"Model '{target}' has no endpoint and provider '{model.Provider}' has no known default endpoint."
                );
            }
        }

        string? key;
        if (!string.IsNullOrEmpty(authRef))
        {
            key = namedKeySource?.Invoke(authRef);
            if (string.IsNullOrEmpty(key))
            {
                return Failure(
                    $"The auth reference '{authRef}' cannot be resolved. "
                        + $"Add a key named '{authRef}' to auth.json or set the matching environment variable."
                );
            }
        }
        else
        {
            key = PresetKey(credentialPreset, environment, namedKeySource);
        }

        // A self-declared endpoint stays inside its host: the credential may only go where the
        // entry points, so redirects off the configured host are refused before they are sent.
        bool pinnedToHost = customEndpoint && credentialPreset is null;
        string requestUrl = modelsUrl?.AbsoluteUri ?? $"{baseUrl}/models";
        // The protocol header a preset's SDK adds on its own (anthropic-version) is discovery's
        // too, even when the endpoint is a custom one that routes through the preset's wire.
        ProviderPreset? protocolPreset =
            credentialPreset ?? (provider is null ? null : ProviderPresets.Find(provider));
        using var client = DiscoveryClient(
            handler ?? new HttpClientHandler(),
            disposeBase: handler is null,
            pinnedToHost,
            protocolPreset,
            entryHeaders,
            key,
            Placement(declaredPlacement, credentialPreset),
            environment ?? Environment.GetEnvironmentVariable,
            namedKeySource
        );

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(CreateRequest(requestUrl, protocolPreset), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
            when (exception
                    is HttpRequestException
                        or TaskCanceledException
                        or InvalidOperationException
            )
        {
            return Failure($"Discovering models from {requestUrl} failed: {exception.Message}");
        }

        if (pinnedToHost)
        {
            (response, requestUrl, DiscoverResult? refused) = await FollowRedirectsInsideHost(
                client,
                response,
                requestUrl,
                protocolPreset,
                ct
            );
            if (refused is not null)
            {
                return refused;
            }
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string message =
                    $"Discovering models from {requestUrl} failed: "
                    + $"HTTP {((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)} ({response.ReasonPhrase}).";
                if (
                    customEndpoint
                    && credentialPreset is null
                    && string.IsNullOrEmpty(key)
                    && response.StatusCode
                        is HttpStatusCode.Unauthorized
                            or HttpStatusCode.Forbidden
                )
                {
                    message +=
                        " No key was sent because the endpoint is not the provider's own; "
                        + "a catalog entry with an 'auth' reference opts in to a named key.";
                }

                return Failure(message);
            }

            string body = await response.Content.ReadAsStringAsync(ct);
            return ParseListing(
                body,
                requestUrl,
                provider,
                customEndpoint,
                baseUrl,
                qualified,
                authRef
            );
        }
    }

    private static DiscoverResult ParseListing(
        string body,
        string requestUrl,
        string? provider,
        bool customEndpoint,
        string baseUrl,
        bool qualified,
        string? authRef
    )
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            return Failure(
                $"The response from {requestUrl} is not valid JSON ({exception.Message})."
            );
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (
                root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Array
            )
            {
                return Failure(
                    $"The response from {requestUrl} does not contain a 'data' array of models."
                );
            }

            var models = new List<DraftModel>();
            foreach (JsonElement entry in data.EnumerateArray())
            {
                if (
                    entry.ValueKind != JsonValueKind.Object
                    || !entry.TryGetProperty("id", out JsonElement id)
                    || id.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(id.GetString())
                )
                {
                    return Failure(
                        $"The response from {requestUrl} has a model entry without an id."
                    );
                }

                string listed = id.GetString()!;
                models.Add(
                    qualified
                        ? new DraftModel(
                            $"{provider}/{listed}",
                            provider,
                            Endpoint: null,
                            authRef,
                            WireId: listed
                        )
                        : new DraftModel(
                            listed,
                            provider,
                            customEndpoint ? baseUrl : null,
                            authRef,
                            WireId: null
                        )
                );
            }

            string draft = JsonSerializer.Serialize(
                new DraftFile(ModelCatalog.SupportedSchemaVersion, models),
                DraftJson
            );
            return new DiscoverResult(true, draft, null);
        }
    }

    private static HttpRequestMessage CreateRequest(string url, ProviderPreset? preset)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (preset?.Adapter == ProviderAdapter.Anthropic)
        {
            // The one protocol header the chat client's SDK adds on its own; the discovery
            // request carries the same value.
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        }

        return request;
    }

    /// <summary>
    /// The discovery client sits on the same pipeline as a chat client: the lunate user agent,
    /// the preset's declared headers (values resolved onto this request's context, which for a
    /// discovery run names nothing) and the credential in the declared placement. One place adds
    /// headers, so discovery cannot forget what the chat path sends. A pinned client keeps its
    /// redirects under the command's own control, so a self-declared endpoint cannot be followed
    /// off its host before the refusal fires.
    /// </summary>
    private static HttpClient DiscoveryClient(
        HttpMessageHandler baseHandler,
        bool disposeBase,
        bool pinnedToHost,
        ProviderPreset? preset,
        IReadOnlyDictionary<string, string>? entryHeaders,
        string? key,
        CredentialPlacement placement,
        Func<string, string?> environment,
        Func<string, string?>? namedKeySource
    )
    {
        if (pinnedToHost && baseHandler is HttpClientHandler clientHandler)
        {
            clientHandler.AllowAutoRedirect = false;
        }

        var declared = new List<(string Name, string Raw)>();
        if (preset?.Headers is { Count: > 0 } presetHeaders)
        {
            declared.AddRange(
                presetHeaders
                    .Where(header => entryHeaders?.ContainsKey(header.Key) != true)
                    .Select(header => (header.Key, header.Value))
            );
        }

        if (entryHeaders is { Count: > 0 })
        {
            declared.AddRange(entryHeaders.Select(header => (header.Key, header.Value)));
        }

        var headers = new ProviderHeadersHandler(
            declared,
            context: null,
            namedKeySource: namedKeySource,
            environment: environment ?? Environment.GetEnvironmentVariable
        );
        DelegatingHandler outermost = headers;
        if (!string.IsNullOrEmpty(key))
        {
            // An auth-referenced key is the explicit opt-in that reaches any declared endpoint;
            // only a preset's own key directions can name a different placement.
            var credential = new ProviderCredentialHandler(key, placement);
            credential.InnerHandler = baseHandler;
            headers.InnerHandler = credential;
            outermost = headers;
        }
        else
        {
            headers.InnerHandler = baseHandler;
        }

        return new HttpClient(outermost, disposeHandler: disposeBase) { Timeout = RequestTimeout };
    }

    /// <summary>The credential placement the discovery wire uses.</summary>
    private static CredentialPlacement Placement(
        CredentialPlacement? declared,
        ProviderPreset? preset
    ) => declared ?? preset?.PlacementFor(preset.DefaultFamily) ?? CredentialPlacement.Bearer;

    /// <summary>
    /// Follows redirects while they stay inside the request URL's host; a redirect to another
    /// host is refused before anything is sent there, because a self-declared endpoint's
    /// credential must not leave the host the entry configures.
    /// </summary>
    private static async Task<(
        HttpResponseMessage Response,
        string RequestUrl,
        DiscoverResult? Failure
    )> FollowRedirectsInsideHost(
        HttpClient client,
        HttpResponseMessage response,
        string requestUrl,
        ProviderPreset? protocolPreset,
        CancellationToken ct
    )
    {
        const int MaxRedirects = 5;
        int redirects = 0;
        while (IsRedirect(response, out Uri? location))
        {
            Uri baseUri = new(requestUrl);
            Uri target = location!.IsAbsoluteUri ? location : new Uri(baseUri, location);
            if (
                !string.Equals(
                    target.Host,
                    baseUri.Host,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                response.Dispose();
                return (
                    response,
                    requestUrl,
                    Failure(
                        $"Discovering models from {requestUrl} was redirected to {target.Host}, which is "
                            + "outside the custom provider's configured host; the redirect was refused "
                            + "and no request was sent there."
                    )
                );
            }

            if (++redirects > MaxRedirects)
            {
                response.Dispose();
                return (
                    response,
                    requestUrl,
                    Failure(
                        $"Discovering models from {requestUrl} was redirected more than "
                            + MaxRedirects.ToString(CultureInfo.InvariantCulture)
                            + " times inside its host."
                    )
                );
            }

            HttpResponseMessage next;
            try
            {
                next = await client.SendAsync(CreateRequest(target.AbsoluteUri, protocolPreset), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
                when (exception
                        is HttpRequestException
                            or TaskCanceledException
                            or InvalidOperationException
                )
            {
                response.Dispose();
                return (
                    response,
                    target.AbsoluteUri,
                    Failure(
                        $"Discovering models from {target.AbsoluteUri} failed: {exception.Message}"
                    )
                );
            }

            response.Dispose();
            response = next;
            requestUrl = target.AbsoluteUri;
        }

        return (response, requestUrl, null);
    }

    private static bool IsRedirect(HttpResponseMessage response, out Uri? location)
    {
        location = null;
        if (
            response.StatusCode
            is not (
                HttpStatusCode.MovedPermanently
                or HttpStatusCode.Found
                or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect
            )
        )
        {
            return false;
        }

        location = response.Headers.Location;
        return location is not null;
    }

    private static bool IsHttpUrl(string target, out string url)
    {
        if (
            Uri.TryCreate(target, UriKind.Absolute, out Uri? parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
        )
        {
            url = parsed.AbsoluteUri;
            return true;
        }

        url = string.Empty;
        return false;
    }

    private static string TrimTrailingSlash(string url) => url.TrimEnd('/');

    /// <summary>The preset whose base URL is exactly this URL, if any.</summary>
    private static ProviderPreset? PresetAtUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? target))
        {
            return null;
        }

        return ProviderPresets.All.FirstOrDefault(preset =>
            ProviderEndpoint.IsOwn(target, preset.BaseUrl)
            && string.Equals(
                target.AbsolutePath.TrimEnd('/'),
                preset.BaseUrl.AbsolutePath.TrimEnd('/'),
                StringComparison.Ordinal
            )
        );
    }

    /// <summary>
    /// Whether a declared endpoint is the preset's own: the base URL itself or a path under it.
    /// </summary>
    private static bool EndpointBelongsToPreset(Uri endpoint, ProviderPreset preset) =>
        ProviderEndpoint.IsOwn(endpoint, preset.BaseUrl);

    /// <summary>
    /// The preset's key precedence: the preset's environment variable, its alias second, then the
    /// auth store under the preset id. A target without a preset gets no credential, so a
    /// self-declared provider never receives another provider's key.
    /// </summary>
    private static string? PresetKey(
        ProviderPreset? preset,
        Func<string, string?>? environment,
        Func<string, string?>? namedKeySource
    )
    {
        if (preset is null)
        {
            return null;
        }

        string? key = EnvironmentValue(preset.KeyVariable, environment);
        if (string.IsNullOrEmpty(key) && preset.KeyVariableAlias is { } alias)
        {
            key = EnvironmentValue(alias, environment);
        }

        return string.IsNullOrEmpty(key) ? namedKeySource?.Invoke(preset.Id) : key;
    }

    private static string? EnvironmentValue(string variable, Func<string, string?>? environment) =>
        environment is null ? Environment.GetEnvironmentVariable(variable) : environment(variable);

    private static DiscoverResult Failure(string message) => new(false, string.Empty, message);

    private sealed record DraftFile(int SchemaVersion, IReadOnlyList<DraftModel> Models);

    private sealed record DraftModel(
        string Id,
        string? Provider,
        string? Endpoint,
        string? Auth,
        string? WireId
    );
}
