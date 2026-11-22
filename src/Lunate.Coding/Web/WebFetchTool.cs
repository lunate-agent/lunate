using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The core <c>web_fetch</c> tool: an approved GET whose HTML is reduced to readable text with the
/// page title and markdown links, bounded in bytes. Redirects are followed here so a loop is a
/// named failure, and every failure names the URL and the reason. It declares
/// <see cref="ToolRisk.Execute"/>, so the approval path is the same as <c>bash</c>.
/// </summary>
public sealed class WebFetchTool : ITool
{
    internal const string ToolName = "web_fetch";
    internal const int MaxOutputBytes = 20 * 1024;
    internal const int MaxSourceBytes = 2 * 1024 * 1024;
    internal const int MaxRedirects = 10;
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private static readonly IReadOnlyList<string> Accepted = ["url"];

    private static readonly JsonElement Schema = JsonDocument
        .Parse(
            """
            {
              "type": "object",
              "properties": {
                "url": {
                  "type": "string",
                  "description": "Absolute http or https URL to fetch"
                }
              },
              "required": ["url"]
            }
            """
        )
        .RootElement.Clone();

    private readonly HttpMessageHandler? _handler;
    private readonly TimeSpan _timeout;

    public WebFetchTool()
        : this(null, null) { }

    internal WebFetchTool(HttpMessageHandler? handler, TimeSpan? timeout)
    {
        _handler = handler;
        _timeout = timeout ?? DefaultTimeout;
    }

    public string Name => ToolName;

    public string Description =>
        "Fetch an http or https URL and return its readable text with the page title. "
        + "HTML is reduced to text and links become markdown; output stops at 20 KiB. "
        + "A non-2xx response, a redirect loop or a timeout names the URL and the reason.";

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

        if (!ToolArguments.TryString(args, "url", out string? rawUrl, out string? error))
        {
            return Error(error!);
        }

        if (
            string.IsNullOrWhiteSpace(rawUrl)
            || !Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(uri.Host)
        )
        {
            return Error(
                $"'{rawUrl}' is not an absolute http or https URL; pass a URL like https://example.com/page."
            );
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);
        try
        {
            return await FetchAsync(uri, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Error(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"timeout after {_timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s fetching {uri.AbsoluteUri}; the site did not answer. Try again or use another page."
                )
            );
        }
        catch (HttpRequestException exception)
        {
            return Error(
                $"could not fetch {uri.AbsoluteUri}: {exception.Message}; check the URL and your connection, then try again."
            );
        }
        catch (IOException exception)
        {
            return Error(
                $"could not read {uri.AbsoluteUri}: {exception.Message}; try again or use another page."
            );
        }
    }

    private async Task<ToolResult> FetchAsync(Uri uri, CancellationToken timeoutToken)
    {
        using HttpClient client = CreateClient();
        var visited = new HashSet<string>(StringComparer.Ordinal) { uri.AbsoluteUri };
        int redirects = 0;
        Uri current = uri;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.TryAddWithoutValidation("User-Agent", RequestContext.LunateVersion);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,text/plain");
            using HttpResponseMessage response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutToken)
                .ConfigureAwait(false);

            if (IsRedirect(response.StatusCode))
            {
                Uri? next = response.Headers.Location is { } location
                    ? Resolve(current, location)
                    : null;
                if (next is null)
                {
                    return Error(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"{current.AbsoluteUri} returned HTTP {(int)response.StatusCode} without a usable Location; the page cannot be followed."
                        )
                    );
                }

                if (redirects >= MaxRedirects || !visited.Add(next.AbsoluteUri))
                {
                    return Error(
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"redirect loop for {uri.AbsoluteUri}: the chain revisited {next.AbsoluteUri} after {redirects.ToString(CultureInfo.InvariantCulture)} redirect(s); the page cannot be fetched."
                        )
                    );
                }

                redirects++;
                current = next;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                return Error(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{current.AbsoluteUri} returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}; correct the URL or try another page."
                    )
                );
            }

            string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!IsText(contentType))
            {
                return Error(
                    $"{current.AbsoluteUri} returned {contentType}, which is not readable text; fetch an HTML or text page instead."
                );
            }

            await using Stream stream = await response
                .Content.ReadAsStreamAsync(timeoutToken)
                .ConfigureAwait(false);
            byte[] source = await WebText
                .ReadCappedAsync(stream, MaxSourceBytes, timeoutToken)
                .ConfigureAwait(false);
            string body = WebText.Decode(source);
            // The transport reports where a followed chain actually ended; fall back to the URL
            // this loop tracked when the handler leaves RequestMessage unset.
            Uri page = response.RequestMessage?.RequestUri ?? current;
            return Result(Render(body, contentType, page));
        }
    }

    private static string Render(string body, string contentType, Uri pageUri)
    {
        if (!IsHtml(contentType))
        {
            return WebText.Cap(body.Trim(), MaxOutputBytes, "page text");
        }

        HtmlPage page = HtmlToText.Parse(body, pageUri);
        string text = page.Text.Trim();
        if (text.Length == 0)
        {
            return page.Title is { Length: > 0 } title
                ? $"# {title}\n\n(no readable text)"
                : "(the page had no readable text)";
        }

        string output = page.Title is { Length: > 0 } pageTitle ? $"# {pageTitle}\n\n{text}" : text;
        return WebText.Cap(output, MaxOutputBytes, "page text");
    }

    private HttpClient CreateClient()
    {
        HttpClient client = _handler is null
            ? new HttpClient(CreateTransport(), disposeHandler: true)
            : new HttpClient(_handler, disposeHandler: false);
        client.Timeout = Timeout.InfiniteTimeSpan;
        return client;
    }

    /// <summary>
    /// The production transport hands every 3xx back to <see cref="FetchAsync"/>, so the manual
    /// loop is the only place that follows a redirect and the 10-hop and loop limits hold.
    /// </summary>
    internal static HttpClientHandler CreateTransport() => new() { AllowAutoRedirect = false };

    private static Uri? Resolve(Uri current, Uri location)
    {
        try
        {
            Uri next = location.IsAbsoluteUri ? location : new Uri(current, location);
            return next.Scheme is "http" or "https" ? next : null;
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status
            is HttpStatusCode.MovedPermanently
                or HttpStatusCode.Redirect
                or HttpStatusCode.SeeOther
                or HttpStatusCode.TemporaryRedirect
                or HttpStatusCode.PermanentRedirect;

    private static bool IsHtml(string contentType) =>
        contentType.Contains("html", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsText(string contentType) =>
        contentType.Length == 0
        || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);

    private static ToolResult Result(string output) => new(output, IsError: false);

    private static ToolResult Error(string output) => new(output, IsError: true);
}
