using System.Globalization;
using System.Net;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// <c>web_fetch</c>: URL validation, manual redirects, the source and text caps, HTML reduced to
/// readable text with absolute markdown links, and every failure naming the URL and its reason.
/// </summary>
public sealed class WebFetchToolTests
{
    private const string PageUrl = "https://example.test/docs/page";

    [Fact]
    public async Task A_page_returns_its_title_and_readable_text()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("article.html"));
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsError);
        Assert.Contains("# Guide & Reference", result.Output, StringComparison.Ordinal);
        Assert.Contains("Getting started", result.Output, StringComparison.Ordinal);
        Assert.Contains(
            "Read the [guide](https://example.test/guide) and the [API](https://example.test/api).",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains("- One", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("do not show", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("color: red", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_relative_link_uses_the_final_response_url()
    {
        using var handler = new StubHttpHandler(
            (request, _) =>
                request.RequestUri!.AbsolutePath == "/start"
                    ? Task.FromResult(Redirect("https://example.test/docs/page"))
                    : Task.FromResult(
                        StubHttpHandler.Response(
                            HttpStatusCode.OK,
                            """<html><body><a href="next">next page</a></body></html>""",
                            "text/html; charset=utf-8"
                        )
                    )
        );
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"url":"https://example.test/start"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsError);
        Assert.Contains(
            "[next page](https://example.test/docs/next)",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task The_output_is_capped_in_utf8_bytes()
    {
        string paragraph = $"<p>{new string('a', 1_000)}</p>";
        string body =
            $"<html><head><title>Long</title></head><body>{string.Concat(Enumerable.Repeat(paragraph, 40))}</body></html>";
        using var handler = StubHttpHandler.Html(body);
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(
            WebTestSupport.ByteCount(result.Output) <= WebFetchTool.MaxOutputBytes,
            string.Create(
                CultureInfo.InvariantCulture,
                $"output was {WebTestSupport.ByteCount(result.Output)} bytes"
            )
        );
        Assert.Contains("capped at", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_response_body_read_is_capped()
    {
        string tail = "TAIL-BEYOND-THE-SOURCE-CAP";
        string body =
            "<html><body>"
            + new string('a', WebFetchTool.MaxSourceBytes + 1_000)
            + tail
            + "</body></html>";
        using var handler = StubHttpHandler.Html(body);
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsError);
        Assert.DoesNotContain(tail, result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_text_response_is_returned_as_text()
    {
        using var handler = StubHttpHandler.Text("first line\nsecond line");
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsError);
        Assert.Contains("first line\nsecond line", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_non_text_response_names_the_content_type()
    {
        using var handler = StubHttpHandler.Html("%PDF-1.7", contentType: "application/pdf");
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(PageUrl, result.Output, StringComparison.Ordinal);
        Assert.Contains("application/pdf", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("file:///etc/passwd", "http")]
    [InlineData("ftp://example.test/file", "http")]
    [InlineData("not a url", "URL")]
    [InlineData("https:///missing-host", "URL")]
    public async Task Only_absolute_http_and_https_urls_are_fetched(string url, string expected)
    {
        using var handler = StubHttpHandler.Html("<html></html>");
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{url}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_non_2xx_response_names_the_url_status_and_reason()
    {
        using var handler = StubHttpHandler.Html("missing", HttpStatusCode.NotFound);
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(PageUrl, result.Output, StringComparison.Ordinal);
        Assert.Contains("404", result.Output, StringComparison.Ordinal);
        Assert.Contains("Not Found", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_production_transport_leaves_redirects_to_the_tool()
    {
        using HttpClientHandler transport = WebFetchTool.CreateTransport();

        Assert.False(transport.AllowAutoRedirect);
    }

    [Fact]
    public async Task A_relative_link_uses_the_url_the_transport_reports()
    {
        using var handler = new StubHttpHandler(
            (request, _) =>
            {
                HttpResponseMessage response = StubHttpHandler.Response(
                    HttpStatusCode.OK,
                    """<html><body><a href="next">next page</a></body></html>""",
                    "text/html; charset=utf-8"
                );
                response.RequestMessage = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://example.test/docs/page"
                );
                return Task.FromResult(response);
            }
        );
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"url":"https://example.test/start"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsError);
        Assert.Contains(
            "[next page](https://example.test/docs/next)",
            result.Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task A_redirect_loop_names_the_url_and_stops_following()
    {
        using var handler = StubHttpHandler.Redirect(PageUrl);
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(PageUrl, result.Output, StringComparison.Ordinal);
        Assert.Contains("redirect loop", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task A_get_fetch_times_out_with_the_url_and_duration()
    {
        using var handler = StubHttpHandler.NeverCompletes();
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubExpiry);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(PageUrl, result.Output, StringComparison.Ordinal);
        Assert.Contains("timeout", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0.05", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_transport_failure_names_the_url_and_reason()
    {
        using var handler = StubHttpHandler.Throws(new HttpRequestException("connection refused"));
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(PageUrl, result.Output, StringComparison.Ordinal);
        Assert.Contains("connection refused", result.Output, StringComparison.Ordinal);
        Assert.Contains("try", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_get_fetch_request_carries_the_product_agent()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("article.html"));
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        StubRequest request = Assert.Single(handler.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal(PageUrl, request.Uri?.AbsoluteUri);
        Assert.StartsWith("lunate/", request.UserAgent, StringComparison.Ordinal);
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
        return response;
    }
}
