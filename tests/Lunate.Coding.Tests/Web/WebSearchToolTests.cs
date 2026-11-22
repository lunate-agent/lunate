using System.Globalization;
using System.Net;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// <c>web_search</c>: one offline HTTP request, ranked title/url/snippet results, the count and byte
/// caps, and a readable non-error result when the endpoint has no matches.
/// </summary>
public sealed class WebSearchToolTests
{
    [Fact]
    public async Task Ranked_results_carry_title_url_and_snippet()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("search-results.html"));
        var tool = new WebSearchTool(handler, TimeSpan.FromSeconds(5));

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"dotnet html"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsError);
        Assert.Contains("dotnet html", result.Output, StringComparison.Ordinal);
        Assert.Contains(
            "1. [First result](https://example.test/guide)",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains("The first snippet & its detail.", result.Output, StringComparison.Ordinal);
        Assert.Contains(
            "2. [Second result](https://example.test/second)",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "The second snippet with a highlight.",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "3. [Third result](https://example.test/third)",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("duckduckgo.com/l/", result.Output, StringComparison.Ordinal);
        Assert.True(
            result.Output.IndexOf("First result", StringComparison.Ordinal)
                < result.Output.IndexOf("Second result", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task The_request_is_a_get_with_the_escaped_query_and_the_product_agent()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("search-results.html"));
        var tool = new WebSearchTool(handler, TimeSpan.FromSeconds(5));

        await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"a & b"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        StubRequest request = Assert.Single(handler.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal("https://html.duckduckgo.com/html/?q=a%20%26%20b", request.Uri?.AbsoluteUri);
        Assert.StartsWith("lunate/", request.UserAgent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_result_count_is_capped_by_the_argument()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("search-results.html"));
        var tool = new WebSearchTool(handler, TimeSpan.FromSeconds(5));

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"x","max_results":2}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.Contains("First result", result.Output, StringComparison.Ordinal);
        Assert.Contains("Second result", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Third result", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_default_and_maximum_result_caps_hold()
    {
        string results = string.Join(
            "\n",
            Enumerable
                .Range(1, 20)
                .Select(index =>
                    $"""<a class="result__a" href="https://example.test/{index}">Result {index}</a><a class="result__snippet">Snippet {index}</a>"""
                )
        );
        using var handler = StubHttpHandler.Html($"<html><body>{results}</body></html>");
        var tool = new WebSearchTool(handler, TimeSpan.FromSeconds(5));

        ToolResult defaults = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"x"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );
        ToolResult capped = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"x","max_results":100}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(WebSearchTool.DefaultMaxResults, CountResults(defaults.Output));
        Assert.Equal(WebSearchTool.MaxResultsCap, CountResults(capped.Output));
    }

    [Fact]
    public async Task The_output_is_capped_in_utf8_bytes()
    {
        string snippet = new string('a', 1_500);
        string results = string.Join(
            "\n",
            Enumerable
                .Range(1, 10)
                .Select(index =>
                    $"""<a class="result__a" href="https://example.test/{index}">Result {index}</a><a class="result__snippet">{snippet}</a>"""
                )
        );
        using var handler = StubHttpHandler.Html($"<html><body>{results}</body></html>");
        var tool = new WebSearchTool(handler, TimeSpan.FromSeconds(5));

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"x","max_results":10}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(
            WebTestSupport.ByteCount(result.Output) <= WebSearchTool.MaxOutputBytes,
            string.Create(
                CultureInfo.InvariantCulture,
                $"output was {WebTestSupport.ByteCount(result.Output)} bytes"
            )
        );
        Assert.Contains("capped at", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_matches_is_a_readable_result_not_an_error()
    {
        using var handler = StubHttpHandler.Html("<html><body>Nothing here.</body></html>");
        var tool = new WebSearchTool(handler, TimeSpan.FromSeconds(5));

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"no such thing"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsError);
        Assert.Contains("no results", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no such thing", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"query":""}""", "non-empty")]
    [InlineData("""{"query":"x","bogus":1}""", "accepted")]
    [InlineData("""{"query":"x","max_results":0}""", "max_results")]
    public async Task Bad_arguments_are_refused_without_a_request(string args, string expected)
    {
        using var handler = StubHttpHandler.Html("<html></html>");
        var tool = new WebSearchTool(handler, TimeSpan.FromSeconds(5));

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args(args),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_http_failure_names_the_endpoint_and_the_status()
    {
        using var handler = StubHttpHandler.Html("unavailable", HttpStatusCode.ServiceUnavailable);
        var tool = new WebSearchTool(handler, TimeSpan.FromSeconds(5));

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"x"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(
            "https://html.duckduckgo.com/html/",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains("503", result.Output, StringComparison.Ordinal);
    }

    private static int CountResults(string output) =>
        output
            .Split('\n')
            .Count(line => line.Contains("](https://example.test/", StringComparison.Ordinal));
}
