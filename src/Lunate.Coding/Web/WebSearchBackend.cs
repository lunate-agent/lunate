using System.Text.Json;

namespace Lunate.Coding;

/// <summary>
/// One ranked search result: a title, an absolute http(s) URL and a snippet (possibly empty). The
/// tool renders these; a backend only produces them.
/// </summary>
internal sealed record SearchResult(string Title, string Url, string Snippet);

/// <summary>
/// One backend's reading of its answer: the ranked results, or a genuine empty search. An answer
/// that is neither — a challenge page, a login page — carries no results and is not
/// <see cref="NoResults"/>, so the tool reports the endpoint as blocked.
/// </summary>
internal sealed record SearchPage(IReadOnlyList<SearchResult> Results, bool NoResults);

/// <summary>
/// The backend seam of the local <c>web_search</c>: where one query goes, how the backend's
/// credential is placed, and how one answer becomes ranked results. The tool keeps everything that
/// must not vary by backend — the argument contract, the approval tier, the count and byte caps and
/// the rendering — so selecting a backend never changes what the model sees.
/// </summary>
internal abstract class WebSearchBackend
{
    /// <summary>The backend's name in the tool description.</summary>
    internal abstract string DisplayName { get; }

    /// <summary>Whether the backend refuses to answer without a credential.</summary>
    internal abstract bool NeedsCredential { get; }

    /// <summary>The one sentence the tool's description adds about this backend.</summary>
    internal abstract string Describe { get; }

    /// <summary>The request for one query; the tool adds the product user agent.</summary>
    internal abstract HttpRequestMessage CreateRequest(
        string query,
        int maxResults,
        string? credential
    );

    /// <summary>Reads one answer body into ranked results or a genuine empty search.</summary>
    internal abstract SearchPage Read(string body, Uri endpoint);
}

/// <summary>
/// The backend table: the setting's four names, the enum they map to, and the backend a resolved
/// setting builds. One table keeps the settings validator, the schema's allowed values and the
/// tool's construction agreeing on the names.
/// </summary>
internal static class WebSearchBackends
{
    /// <summary>The names a <c>tools.webSearch.backend</c> value may carry, in documented order.</summary>
    internal static readonly IReadOnlyList<string> Names =
    [
        "duckduckgo",
        "searxng",
        "brave",
        "tavily",
    ];

    /// <summary>The accepted values as the settings errors list them.</summary>
    internal static readonly string Accepted = "'duckduckgo', 'searxng', 'brave' or 'tavily'";

    internal static string Name(WebSearchBackendKind backend) =>
        backend switch
        {
            WebSearchBackendKind.DuckDuckGo => "duckduckgo",
            WebSearchBackendKind.SearXng => "searxng",
            WebSearchBackendKind.Brave => "brave",
            WebSearchBackendKind.Tavily => "tavily",
            _ => throw new InvalidOperationException(
                $"the web search backend '{backend}' has no setting name"
            ),
        };

    internal static bool TryParse(string? name, out WebSearchBackendKind backend)
    {
        switch (name)
        {
            case "duckduckgo":
                backend = WebSearchBackendKind.DuckDuckGo;
                return true;
            case "searxng":
                backend = WebSearchBackendKind.SearXng;
                return true;
            case "brave":
                backend = WebSearchBackendKind.Brave;
                return true;
            case "tavily":
                backend = WebSearchBackendKind.Tavily;
                return true;
            default:
                backend = WebSearchBackendKind.DuckDuckGo;
                return false;
        }
    }

    /// <summary>
    /// The backend one resolved setting builds. The setting is validated when it loads, so the
    /// misses here are a directly constructed <see cref="WebSearchSettings"/>, not a user file.
    /// </summary>
    internal static WebSearchBackend Create(WebSearchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Backend switch
        {
            WebSearchBackendKind.DuckDuckGo => new DuckDuckGoBackend(),
            WebSearchBackendKind.SearXng => new SearXngBackend(
                settings.Url
                    ?? throw new InvalidOperationException(
                        "'tools.webSearch.url' is required by the 'searxng' backend"
                    )
            ),
            WebSearchBackendKind.Brave => new BraveBackend(),
            WebSearchBackendKind.Tavily => new TavilyBackend(),
            _ => throw new InvalidOperationException(
                $"the web search backend '{settings.Backend}' has no implementation"
            ),
        };
    }
}

/// <summary>
/// The JSON reading both API backends share: one object, an optional container property, then a
/// <c>results</c> array of objects with a title, an absolute URL and a snippet property. A body that
/// is not JSON of that shape reads as <c>null</c>, which the tool reports as the endpoint not
/// answering a search; a shape without results is a genuine empty search. Neither is an exception a
/// model cannot act on.
/// </summary>
internal static class SearchJson
{
    internal static SearchPage? Read(string body, string? container, string snippetProperty)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (container is not null)
            {
                if (!root.TryGetProperty(container, out JsonElement scope))
                {
                    // Brave answers a query with no matches without a `web` object at all.
                    return new SearchPage([], NoResults: true);
                }

                if (scope.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                root = scope;
            }

            if (
                !root.TryGetProperty("results", out JsonElement array)
                || array.ValueKind != JsonValueKind.Array
            )
            {
                return new SearchPage([], NoResults: true);
            }

            var results = new List<SearchResult>();
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Text(item, "url") is not { } href)
                {
                    continue;
                }

                if (SearchHtml.AbsoluteUrl(href, baseUri: null) is not { } url)
                {
                    continue;
                }

                string title = HtmlToText.InlineText(Text(item, "title") ?? string.Empty);
                results.Add(
                    new SearchResult(
                        title.Length == 0 ? url : title,
                        url,
                        HtmlToText.InlineText(Text(item, snippetProperty) ?? string.Empty)
                    )
                );
            }

            return new SearchPage(results, NoResults: results.Count == 0);
        }
    }

    private static string? Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
