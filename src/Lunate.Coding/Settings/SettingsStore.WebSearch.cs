using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>The search backend a <c>web_search</c> call runs on.</summary>
public enum WebSearchBackendKind
{
    /// <summary>DuckDuckGo's HTML endpoint: the zero-config default, no credential.</summary>
    DuckDuckGo,

    /// <summary>A user-run SearXNG instance named by URL, with an optional bearer credential.</summary>
    SearXng,

    /// <summary>The Brave Search API, with a credential reference.</summary>
    Brave,

    /// <summary>The Tavily search API, with a credential reference.</summary>
    Tavily,
}

/// <summary>
/// The resolved <c>tools.webSearch</c> settings section: the backend of a session's local
/// <c>web_search</c>, the SearXNG instance URL, and the backend's credential as a reference the
/// send-time resolver reads — never a resolved secret. A backend is a tool setting, not a usage:
/// nothing here is a model attribution value.
/// </summary>
public sealed record WebSearchSettings
{
    /// <summary>The backend; DuckDuckGo when nothing is configured.</summary>
    public WebSearchBackendKind Backend { get; init; } = WebSearchBackendKind.DuckDuckGo;

    /// <summary>The SearXNG instance's base URL; required by that backend and refused by others.</summary>
    public Uri? Url { get; init; }

    /// <summary>The backend's <c>auth:</c>/<c>env:</c>/<c>helper:</c> credential reference.</summary>
    public string? Credential { get; init; }
}

public static partial class SettingsStore
{
    /// <summary>
    /// Reads the <c>tools.webSearch</c> section: the backend, its URL and its credential reference.
    /// Every cross-field rule is checked here, so a session never starts on a backend it cannot use —
    /// a missing URL or credential, or one that belongs to another backend, is refused by key rather
    /// than silently ignored.
    /// </summary>
    private static WebSearchSettings ReadWebSearch(JsonElement value, List<string> problems)
    {
        var settings = new WebSearchSettings();
        if (value.ValueKind != JsonValueKind.Object)
        {
            problems.Add("'tools.webSearch' must be an object");
            return settings;
        }

        string? backendName = null;
        string? urlText = null;
        string? credential = null;
        bool hasUrl = false;
        bool hasCredential = false;
        foreach (JsonProperty property in value.EnumerateObject())
        {
            switch (property.Name)
            {
                case "backend":
                    backendName = ReadProviderField(
                        property.Value,
                        "tools.webSearch",
                        "backend",
                        problems
                    );
                    break;
                case "url":
                    hasUrl = true;
                    urlText = ReadProviderField(property.Value, "tools.webSearch", "url", problems);
                    break;
                case "credential":
                    hasCredential = true;
                    credential = ReadProviderField(
                        property.Value,
                        "tools.webSearch",
                        "credential",
                        problems
                    );
                    break;
                default:
                    problems.Add($"unknown key 'tools.webSearch.{property.Name}'");
                    break;
            }
        }

        WebSearchBackendKind backend = WebSearchBackendKind.DuckDuckGo;
        if (backendName is not null && !WebSearchBackends.TryParse(backendName, out backend))
        {
            problems.Add(
                $"'tools.webSearch.backend' must be {WebSearchBackends.Accepted}, but was "
                    + $"'{backendName}'"
            );
            return settings;
        }

        Uri? url = null;
        if (urlText is not null && !TrySearchUrl(urlText, out url))
        {
            problems.Add(
                "'tools.webSearch.url' must be an absolute https URL, or http only for a loopback host"
            );
        }

        string name = WebSearchBackends.Name(backend);
        if (backend == WebSearchBackendKind.SearXng)
        {
            if (!hasUrl)
            {
                problems.Add(
                    $"'tools.webSearch.url' is required when 'tools.webSearch.backend' is '{name}'"
                );
            }
        }
        else if (hasUrl)
        {
            problems.Add(
                $"'tools.webSearch.url' belongs to the 'searxng' backend; remove it or select "
                    + $"'searxng', not '{name}'"
            );
        }

        if (backend is WebSearchBackendKind.Brave or WebSearchBackendKind.Tavily)
        {
            if (!hasCredential)
            {
                problems.Add(
                    $"'tools.webSearch.credential' is required when 'tools.webSearch.backend' is '{name}'"
                );
            }
        }
        else if (hasCredential && backend == WebSearchBackendKind.DuckDuckGo)
        {
            problems.Add(
                "'tools.webSearch.credential' belongs to the 'searxng', 'brave' and 'tavily' "
                    + "backends; remove it or select one, not 'duckduckgo'"
            );
        }

        if (credential is not null && !ProviderEntry.IsCredentialReference(credential))
        {
            problems.Add(
                "'tools.webSearch.credential' must be an auth:<name>, env:<VARIABLE> or "
                    + "helper:<name> reference, never a literal key"
            );
        }

        return settings with
        {
            Backend = backend,
            Url = url,
            Credential = credential,
        };
    }

    /// <summary>The URL rule the provider store applies to an endpoint: https, or http for loopback.</summary>
    private static bool TrySearchUrl(string value, out Uri? url)
    {
        if (
            !Uri.TryCreate(value, UriKind.Absolute, out url)
            || !(
                url.Scheme == Uri.UriSchemeHttps
                || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback)
            )
        )
        {
            url = null;
            return false;
        }

        return true;
    }
}
