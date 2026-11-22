using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lunate.Agent;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The core <c>web_search</c> tool: one unauthenticated request to DuckDuckGo's HTML endpoint,
/// ranked results with a title, an absolute URL and a snippet, bounded in count and total bytes.
/// It declares <see cref="ToolRisk.Execute"/>, so the approval path is the same as <c>bash</c>.
/// </summary>
public sealed class WebSearchTool : ITool
{
    internal const string ToolName = "web_search";
    internal const string SearchEndpoint = "https://html.duckduckgo.com/html/";
    internal const int DefaultMaxResults = 5;
    internal const int MaxResultsCap = 10;
    internal const int MaxOutputBytes = 8 * 1024;
    internal const int MaxSourceBytes = 1024 * 1024;
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private static readonly IReadOnlyList<string> Accepted = ["query", "max_results"];

    private static readonly JsonElement Schema = JsonDocument
        .Parse(
            """
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string",
                  "description": "Search query; the terms are sent to the search endpoint"
                },
                "max_results": {
                  "type": "integer",
                  "description": "Maximum results (default 5, max 10)"
                }
              },
              "required": ["query"]
            }
            """
        )
        .RootElement.Clone();

    private static readonly Regex AnchorPattern = new(
        """<a\b(?<attrs>[^>]*)>(?<text>.*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1)
    );

    private static readonly Uri Endpoint = new(SearchEndpoint, UriKind.Absolute);
    private readonly HttpMessageHandler? _handler;
    private readonly TimeSpan _timeout;

    public WebSearchTool()
        : this(null, null) { }

    internal WebSearchTool(HttpMessageHandler? handler, TimeSpan? timeout)
    {
        _handler = handler;
        _timeout = timeout ?? DefaultTimeout;
    }

    public string Name => ToolName;

    public string Description =>
        "Search the web and return ranked results with a title, URL and snippet. "
        + "Uses DuckDuckGo's HTML endpoint, needs no API key, returns 5 results by default "
        + "(max 10) and stops at 8 KiB of output.";

    public JsonElement ParametersSchema => Schema;

    public ToolRisk Risk => ToolRisk.Execute;

    public async Task<ToolResult> ExecuteAsync(
        JsonElement args,
        ToolContext ctx,
        CancellationToken ct
    )
    {
        if (!ToolArguments.TryValidate(args, Accepted, out string? argumentError))
        {
            return Error(argumentError!);
        }

        if (
            !ToolArguments.TryString(args, "query", out string? query, out string? error)
            || !ToolArguments.TryInteger(args, "max_results", out int? requested, out error)
        )
        {
            return Error(error!);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return Error("query is required and must be a non-empty string");
        }

        int maxResults = requested ?? DefaultMaxResults;
        if (maxResults < 1)
        {
            return Error("max_results must be at least 1");
        }

        maxResults = Math.Min(maxResults, MaxResultsCap);

        Uri endpoint = new(SearchEndpoint + "?q=" + Uri.EscapeDataString(query), UriKind.Absolute);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);
        try
        {
            using HttpClient client = CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.TryAddWithoutValidation("User-Agent", RequestContext.LunateVersion);
            request.Headers.Accept.ParseAdd("text/html");
            using HttpResponseMessage response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Error(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"search failed: {endpoint} returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}; try again later."
                    )
                );
            }

            await using Stream stream = await response
                .Content.ReadAsStreamAsync(timeout.Token)
                .ConfigureAwait(false);
            byte[] source = await WebText
                .ReadCappedAsync(stream, MaxSourceBytes, timeout.Token)
                .ConfigureAwait(false);
            List<SearchResult> results = Parse(WebText.Decode(source), Endpoint);
            if (results.Count == 0)
            {
                return Result($"No results found for '{query}'. Try a different query.");
            }

            var output = new StringBuilder();
            output.Append("Search results for '").Append(query).Append("':\n");
            int number = 1;
            foreach (SearchResult result in results.Take(maxResults))
            {
                output
                    .Append(number.ToString(CultureInfo.InvariantCulture))
                    .Append(". [")
                    .Append(result.Title)
                    .Append("](")
                    .Append(result.Url)
                    .Append(")\n   ")
                    .Append(result.Snippet.Length == 0 ? "(no snippet)" : result.Snippet)
                    .Append('\n');
                number++;
            }

            return Result(
                WebText.Cap(output.ToString().TrimEnd('\n'), MaxOutputBytes, "search output")
            );
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Error(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"search timeout after {_timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s for '{query}'; the search endpoint did not answer. Try again or a narrower query."
                )
            );
        }
        catch (HttpRequestException exception)
        {
            return Error(
                $"could not search for '{query}': {exception.Message}; check the query and your connection, then try again."
            );
        }
        catch (IOException exception)
        {
            return Error(
                $"could not read the search response for '{query}': {exception.Message}; try again."
            );
        }
    }

    private HttpClient CreateClient()
    {
        HttpClient client = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    private static List<SearchResult> Parse(string html, Uri endpoint)
    {
        var results = new List<SearchResult>();
        foreach (Match match in AnchorPattern.Matches(html))
        {
            string attributes = match.Groups["attrs"].Value;
            string classes = HtmlToText.Attribute(attributes, "class") ?? string.Empty;
            if (classes.Contains("result__a", StringComparison.Ordinal))
            {
                string? href = HtmlToText.Attribute(attributes, "href");
                string? url = Unwrap(href, endpoint);
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

    private static string? Unwrap(string? href, Uri endpoint)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        string value = WebUtility.HtmlDecode(href).Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            value = endpoint.Scheme + ":" + value;
        }

        int redirect = value.IndexOf("uddg=", StringComparison.Ordinal);
        if (redirect >= 0)
        {
            int start = redirect + "uddg=".Length;
            int end = value.IndexOf('&', start);
            string encoded = end < 0 ? value[start..] : value[start..end];
            try
            {
                value = Uri.UnescapeDataString(encoded);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absolute))
        {
            return absolute.Scheme is "http" or "https" ? absolute.AbsoluteUri : null;
        }

        if (Uri.TryCreate(endpoint, value, out absolute))
        {
            return absolute.Scheme is "http" or "https" ? absolute.AbsoluteUri : null;
        }

        return null;
    }

    private static ToolResult Result(string output) => new(output, IsError: false);

    private static ToolResult Error(string output) => new(output, IsError: true);

    private sealed record SearchResult(string Title, string Url, string Snippet);
}
