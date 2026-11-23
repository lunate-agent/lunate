using System.Globalization;
using System.Net;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// <c>web_search</c>: one offline HTTP request, ranked title/url/snippet results, the count and byte
/// caps, a readable non-error result for a genuine empty page, and a blocked/rate-limited error when
/// the endpoint refuses the search.
/// </summary>
public sealed class WebSearchToolTests
{
    [Fact]
    public async Task Ranked_results_carry_title_url_and_snippet()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("search-results.html"));
        var tool = new WebSearchTool(handler, WaitBudgets.WebStubRequest);

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
        var tool = new WebSearchTool(handler, WaitBudgets.WebStubRequest);

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
        var tool = new WebSearchTool(handler, WaitBudgets.WebStubRequest);

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
        var tool = new WebSearchTool(handler, WaitBudgets.WebStubRequest);

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
        var tool = new WebSearchTool(handler, WaitBudgets.WebStubRequest);

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
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("search-no-results.html"));
        var tool = new WebSearchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args("""{"query":"xqzvbnmplkjhgfdsa"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsError);
        Assert.Contains(
            "No results found for 'xqzvbnmplkjhgfdsa'. Try a different query.",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("blocked", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{"query":""}""", "non-empty")]
    [InlineData("""{"query":"x","bogus":1}""", "accepted")]
    [InlineData("""{"query":"x","max_results":0}""", "max_results")]
    public async Task Bad_arguments_are_refused_without_a_request(string args, string expected)
    {
        using var handler = StubHttpHandler.Html("<html></html>");
        var tool = new WebSearchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args(args),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(expected, result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, "202")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "503")]
    public async Task A_non_200_response_is_a_blocked_endpoint_not_a_search_failure(
        HttpStatusCode status,
        string expectedStatus
    )
    {
        using var handler = StubHttpHandler.Html(
            WebTestSupport.Fixture("search-blocked.html"),
            status
        );
        var tool = new WebSearchTool(handler, WaitBudgets.WebStubRequest);

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
        Assert.Contains(expectedStatus, result.Output, StringComparison.Ordinal);
        Assert.Contains("blocked", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rate-limit", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "No results found",
            result.Output,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task A_200_challenge_page_is_a_blocked_endpoint_not_no_results()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("search-blocked.html"));
        var tool = new WebSearchTool(handler, WaitBudgets.WebStubRequest);

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
        Assert.Contains("blocked", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rate-limit", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "No results found",
            result.Output,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static int CountResults(string output) =>
        output
            .Split('\n')
            .Count(line => line.Contains("](https://example.test/", StringComparison.Ordinal));
}
