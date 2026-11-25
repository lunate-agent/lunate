using System.Text.RegularExpressions;

namespace Lunate.Coding;

/// <summary>
/// A user-run SearXNG instance: one request to the instance's <c>/search</c> path, its
/// <c>article.result</c> blocks read as ranked results, and an optional bearer credential for an
/// instance that sits behind an authenticating proxy. The instance URL is the setting, so the same
/// request shape serves a loopback container and a hosted instance.
/// </summary>
internal sealed class SearXngBackend : WebSearchBackend
{
    private static readonly Regex ArticlePattern = new(
        """<article\b(?<attrs>[^>]*)>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );

    private static readonly Regex ParagraphPattern = new(
        """<p\b(?<attrs>[^>]*)>(?<text>.*?)</p>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );

    private readonly Uri _instance;

    internal SearXngBackend(Uri instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instance = instance;
    }

    internal override string DisplayName => "SearXNG";

    internal override bool NeedsCredential => false;

    // The instance URL stays out of the description: the description reaches the model provider,
    // and a self-hosted instance's host is the user's business.
    internal override string Describe => "Uses the configured SearXNG instance.";

    internal override HttpRequestMessage CreateRequest(
        string query,
        int maxResults,
        string? credential
    )
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(
                _instance.AbsoluteUri.TrimEnd('/') + "/search?q=" + Uri.EscapeDataString(query),
                UriKind.Absolute
            )
        );
        request.Headers.Accept.ParseAdd("text/html");
        if (!string.IsNullOrWhiteSpace(credential))
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + credential);
        }

        return request;
    }

    internal override SearchPage Read(string body, Uri endpoint)
    {
        List<SearchResult> results = Parse(body, endpoint);
        return new SearchPage(results, results.Count == 0 && SearchHtml.LooksLikeNoResults(body));
    }

    /// <summary>
    /// Reads the instance's result blocks: each <c>article.result</c> holds its link in an anchor and
    /// its snippet in a classed paragraph. Blocks are split on the article open tag, so an instance
    /// that adds wrappers or extra elements keeps working; a block without an absolute http(s)
    /// target is skipped, never rendered as a broken link.
    /// </summary>
    private static List<SearchResult> Parse(string html, Uri endpoint)
    {
        var results = new List<SearchResult>();
        MatchCollection articles = ArticlePattern.Matches(html);
        for (int index = 0; index < articles.Count; index++)
        {
            Match article = articles[index];
            if (
                !SearchHtml
                    .ClassAttribute(article.Groups["attrs"].Value)
                    .Contains("result", StringComparison.Ordinal)
            )
            {
                continue;
            }

            int start = article.Index + article.Length;
            int end = index + 1 < articles.Count ? articles[index + 1].Index : html.Length;
            string block = html[start..end];
            SearchResult? result = ReadBlock(block, endpoint);
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results;
    }

    private static SearchResult? ReadBlock(string block, Uri endpoint)
    {
        string? url = null;
        string title = string.Empty;
        foreach (Match anchor in SearchHtml.AnchorPattern.Matches(block))
        {
            string? candidate = SearchHtml.AbsoluteUrl(
                HtmlToText.Attribute(anchor.Groups["attrs"].Value, "href"),
                endpoint
            );
            if (candidate is null)
            {
                continue;
            }

            url = candidate;
            title = HtmlToText.InlineText(anchor.Groups["text"].Value);
            break;
        }

        if (url is null)
        {
            return null;
        }

        string snippet = string.Empty;
        foreach (Match paragraph in ParagraphPattern.Matches(block))
        {
            if (
                SearchHtml
                    .ClassAttribute(paragraph.Groups["attrs"].Value)
                    .Contains("content", StringComparison.Ordinal)
            )
            {
                snippet = HtmlToText.InlineText(paragraph.Groups["text"].Value);
                break;
            }
        }

        return new SearchResult(title.Length == 0 ? url : title, url, snippet);
    }
}
