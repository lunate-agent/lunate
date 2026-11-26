using System.Text.RegularExpressions;

namespace Lunate.Coding;

/// <summary>
/// DuckDuckGo's HTML endpoint — the backend of a session that configures none: one unauthenticated
/// request, ranked <c>result__a</c> anchors with a <c>result__snippet</c> each, and the
/// <c>uddg</c> redirect parameter unwrapped into the real URL. Its no-results element is the
/// genuine-marker rule the blocked-endpoint change introduced.
/// </summary>
internal sealed class DuckDuckGoBackend : WebSearchBackend
{
    internal const string SearchEndpoint = "https://html.duckduckgo.com/html/";

    private static readonly Uri Endpoint = new(SearchEndpoint, UriKind.Absolute);

    internal override string DisplayName => "DuckDuckGo";

    internal override bool NeedsCredential => false;

    internal override string Describe => "Uses DuckDuckGo's HTML endpoint and needs no API key.";

    internal override HttpRequestMessage CreateRequest(
        string query,
        int maxResults,
        string? credential
    )
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(SearchEndpoint + "?q=" + Uri.EscapeDataString(query), UriKind.Absolute)
        );
        request.Headers.Accept.ParseAdd("text/html");
        return request;
    }

    internal override SearchPage Read(string body, Uri endpoint)
    {
        List<SearchResult> results = Parse(body);
        return new SearchPage(results, results.Count == 0 && SearchHtml.LooksLikeNoResults(body));
    }

    private static List<SearchResult> Parse(string html)
    {
        var results = new List<SearchResult>();
        foreach (Match match in SearchHtml.AnchorPattern.Matches(html))
        {
            string attributes = match.Groups["attrs"].Value;
            string classes = SearchHtml.ClassAttribute(attributes);
            if (classes.Contains("result__a", StringComparison.Ordinal))
            {
                string? url = Resolve(HtmlToText.Attribute(attributes, "href"));
                if (url is null)
                {
                    continue;
                }

                string title = HtmlToText.InlineText(match.Groups["text"].Value);
                results.Add(new SearchResult(title.Length == 0 ? url : title, url, string.Empty));
            }
            else if (
                classes.Contains("result__snippet", StringComparison.Ordinal)
                && results.Count > 0
                && results[^1].Snippet.Length == 0
            )
            {
                results[^1] = results[^1] with
                {
                    Snippet = HtmlToText.InlineText(match.Groups["text"].Value),
                };
            }
        }

        return results;
    }

    /// <summary>
    /// DuckDuckGo wraps a result target in its own redirect: <c>/l/?uddg=&lt;encoded target&gt;</c>.
    /// The target is unwrapped before absolute resolution, so the model gets the destination.
    /// </summary>
    private static string? Resolve(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        string decoded = System.Net.WebUtility.HtmlDecode(href).Trim();
        int redirect = decoded.IndexOf("uddg=", StringComparison.Ordinal);
        if (redirect >= 0)
        {
            int start = redirect + "uddg=".Length;
            int end = decoded.IndexOf('&', start);
            string encoded = end < 0 ? decoded[start..] : decoded[start..end];
            try
            {
                decoded = Uri.UnescapeDataString(encoded);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        return SearchHtml.AbsoluteUrl(decoded, Endpoint);
    }
}
