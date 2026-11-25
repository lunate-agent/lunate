using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The core <c>web_search</c> tool: one request to the configured backend — DuckDuckGo's HTML
/// endpoint by default, or a SearXNG instance, Brave or Tavily — ranked results with a title, an
/// absolute URL and a snippet, bounded in count and total bytes. The backend supplies the request
/// and the answer shape; everything the model sees is the same for every backend. It declares
/// <see cref="ToolRisk.Execute"/>, so the approval path is the same as <c>bash</c> whatever backend
/// is selected.
/// </summary>
public sealed class WebSearchTool : ITool
{
    internal const string ToolName = "web_search";
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
                  "description": "Search query; the terms are sent to the search backend"
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

    private readonly WebSearchConfiguration _configuration;
    private readonly WebSearchBackend _backend;
    private readonly HttpMessageHandler? _handler;
    private readonly TimeSpan _timeout;

    public WebSearchTool()
        : this(new WebSearchConfiguration()) { }

    internal WebSearchTool(
        WebSearchConfiguration configuration,
        HttpMessageHandler? handler = null,
        TimeSpan? timeout = null
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
        _backend = WebSearchBackends.Create(configuration.Resolved);
        _handler = handler;
        _timeout = timeout ?? DefaultTimeout;
    }

    public string Name => ToolName;

    public string Description =>
        "Search the web and return ranked results with a title, URL and snippet. "
        + _backend.Describe
        + " Returns 5 results by default (max 10) and stops at 8 KiB of output.";

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

        if (!TryCredential(out string? credential, out ToolResult? credentialFailure))
        {
            return credentialFailure!;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);
        try
        {
            using HttpClient client = CreateClient();
            using HttpRequestMessage request = _backend.CreateRequest(
                query,
                maxResults,
                credential
            );
            Uri endpoint = request.RequestUri!;
            request.Headers.TryAddWithoutValidation("User-Agent", RequestContext.LunateVersion);
            using HttpResponseMessage response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return Blocked(
                    endpoint,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                    )
                );
            }

            await using Stream stream = await response
                .Content.ReadAsStreamAsync(timeout.Token)
                .ConfigureAwait(false);
            byte[] source = await WebText
                .ReadCappedAsync(stream, MaxSourceBytes, timeout.Token)
                .ConfigureAwait(false);
            SearchPage page = _backend.Read(WebText.Decode(source), endpoint);
            if (page.Results.Count == 0)
            {
                if (!page.NoResults)
                {
                    return Blocked(
                        endpoint,
                        "answered HTTP 200 with a page that is not a search results page"
                    );
                }

                return Result($"No results found for '{query}'. Try a different query.");
            }

            return Result(Render(query, page.Results, maxResults));
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

    /// <summary>
    /// The credential the request carries, or the error result that stops it. A backend that needs
    /// one and has no reference, or a reference that does not resolve, sends no request: the
    /// approval has already been spent, and a request without the key would only burn the endpoint's
    /// rate limit.
    /// </summary>
    private bool TryCredential(out string? credential, out ToolResult? failure)
    {
        credential = null;
        failure = null;
        string? reference = _configuration.Resolved.Credential;
        if (reference is null)
        {
            if (_backend.NeedsCredential)
            {
                failure = Error(
                    $"the {_backend.DisplayName} backend needs a credential, but "
                        + $"'{WebSearchCredential.SettingKey}' is not set; add an 'auth:<name>' or "
                        + "'env:<VARIABLE>' reference and try again."
                );
                return false;
            }

            return true;
        }

        if (
            !WebSearchCredential.TryResolve(
                reference,
                _configuration.NamedKeySource,
                _configuration.Environment,
                out credential,
                out string? error
            )
        )
        {
            failure = Error(error!);
            return false;
        }

        return true;
    }

    private static string Render(string query, IReadOnlyList<SearchResult> results, int maxResults)
    {
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

        return WebText.Cap(output.ToString().TrimEnd('\n'), MaxOutputBytes, "search output");
    }

    private HttpClient CreateClient()
    {
        HttpClient client = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    private static ToolResult Blocked(Uri endpoint, string detail) =>
        Error(
            string.Create(
                CultureInfo.InvariantCulture,
                $"search blocked: {endpoint} {detail}; the search endpoint is blocking or rate-limiting this client. Try again later."
            )
        );

    private static ToolResult Result(string output) => new(output, IsError: false);

    private static ToolResult Error(string output) => new(output, IsError: true);
}
