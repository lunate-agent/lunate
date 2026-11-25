using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The <c>web_search</c> backend seam: DuckDuckGo is the zero-config default, and SearXNG, Brave and
/// Tavily answer the same tool through their own request shape and their own credential placement.
/// Every answer comes from a committed fixture, so no case reaches the network.
/// </summary>
public sealed class WebSearchBackendTests
{
    [Fact]
    public async Task DuckDuckGo_stays_the_default_backend()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("search-results.html"));
        WebSearchTool tool = Tool(new WebSearchSettings(), handler);

        Assert.Contains("DuckDuckGo", tool.Description, StringComparison.Ordinal);
        ToolResult result = await Run(tool, """{"query":"dotnet html"}""");

        Assert.False(result.IsError);
        StubRequest request = Assert.Single(handler.Requests);
        Assert.Equal("https://html.duckduckgo.com/html/?q=dotnet%20html", request.Uri?.AbsoluteUri);
        Assert.DoesNotContain("Authorization", request.Headers.Keys);
        Assert.DoesNotContain("X-Subscription-Token", request.Headers.Keys);
    }

    [Fact]
    public async Task SearXng_sends_the_query_to_the_configured_instance()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("searxng-results.html"));
        WebSearchTool tool = Tool(
            new WebSearchSettings
            {
                Backend = WebSearchBackendKind.SearXng,
                Url = new Uri("https://search.example.test/"),
            },
            handler
        );

        Assert.Contains("SearXNG", tool.Description, StringComparison.Ordinal);
        ToolResult result = await Run(tool, """{"query":"dotnet html"}""");

        Assert.False(result.IsError);
        StubRequest request = Assert.Single(handler.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Equal(
            "https://search.example.test/search?q=dotnet%20html",
            request.Uri?.AbsoluteUri
        );
        Assert.Contains("text/html", request.Accept, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", request.Headers.Keys);
        Assert.Contains(
            "1. [First result](https://example.test/guide)",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains("The first snippet & its detail.", result.Output, StringComparison.Ordinal);
        Assert.Contains(
            "3. [Third result](https://example.test/third)",
            result.Output,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task A_configured_SearXng_credential_is_a_bearer_token()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("searxng-results.html"));
        WebSearchTool tool = Tool(
            new WebSearchSettings
            {
                Backend = WebSearchBackendKind.SearXng,
                Url = new Uri("http://127.0.0.1:8888/"),
                Credential = "env:SEARXNG_TOKEN",
            },
            handler,
            environment: name => name == "SEARXNG_TOKEN" ? "instance-secret" : null
        );

        ToolResult result = await Run(tool, """{"query":"x"}""");

        Assert.False(result.IsError);
        StubRequest request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer instance-secret", request.Headers["Authorization"]);
        Assert.DoesNotContain(
            "instance-secret",
            request.Uri!.AbsoluteUri,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SearXng_without_matches_is_a_result_not_an_error()
    {
        using var handler = StubHttpHandler.Html(WebTestSupport.Fixture("searxng-no-results.html"));
        WebSearchTool tool = Tool(
            new WebSearchSettings
            {
                Backend = WebSearchBackendKind.SearXng,
                Url = new Uri("https://search.example.test/"),
            },
            handler
        );

        ToolResult result = await Run(tool, """{"query":"xqzvbnmplkjhgfdsa"}""");

        Assert.False(result.IsError);
        Assert.Contains(
            "No results found for 'xqzvbnmplkjhgfdsa'.",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("blocked", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Brave_names_the_query_and_the_count_and_carries_its_key()
    {
        using var handler = StubHttpHandler.Json(WebTestSupport.Fixture("brave-results.json"));
        WebSearchTool tool = Tool(
            new WebSearchSettings
            {
                Backend = WebSearchBackendKind.Brave,
                Credential = "env:BRAVE_SEARCH_KEY",
            },
            handler,
            environment: name => name == "BRAVE_SEARCH_KEY" ? "brave-secret" : null
        );

        ToolResult result = await Run(tool, """{"query":"a & b","max_results":2}""");

        Assert.False(result.IsError);
        StubRequest request = Assert.Single(handler.Requests);
        Assert.Equal("GET", request.Method);
        Assert.Contains("q=a%20%26%20b", request.Uri!.Query, StringComparison.Ordinal);
        Assert.Contains("count=2", request.Uri.Query, StringComparison.Ordinal);
        Assert.Equal("brave-secret", request.Headers["X-Subscription-Token"]);
        Assert.DoesNotContain("Authorization", request.Headers.Keys);
        Assert.Contains(
            "1. [First result](https://example.test/guide)",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains("The first snippet & its detail.", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Third result", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tavily_posts_the_query_and_carries_a_bearer_token()
    {
        using var handler = StubHttpHandler.Json(WebTestSupport.Fixture("tavily-results.json"));
        WebSearchTool tool = Tool(
            new WebSearchSettings
            {
                Backend = WebSearchBackendKind.Tavily,
                Credential = "auth:tavily",
            },
            handler,
            namedKeys: name => name == "tavily" ? "tavily-secret" : null
        );

        ToolResult result = await Run(tool, """{"query":"dotnet html","max_results":2}""");

        Assert.False(result.IsError);
        StubRequest request = Assert.Single(handler.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("https://api.tavily.com/search", request.Uri?.AbsoluteUri);
        Assert.Equal("Bearer tavily-secret", request.Headers["Authorization"]);
        Assert.DoesNotContain("tavily-secret", request.Body!, StringComparison.Ordinal);
        using JsonDocument body = JsonDocument.Parse(request.Body!);
        Assert.Equal("dotnet html", body.RootElement.GetProperty("query").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("max_results").GetInt32());
        Assert.Contains(
            "2. [Second result](https://example.test/second)",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("Third result", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("brave", "brave-no-results.json")]
    [InlineData("tavily", "tavily-no-results.json")]
    public async Task An_empty_api_answer_is_a_result_not_an_error(string backend, string fixture)
    {
        using var handler = StubHttpHandler.Json(WebTestSupport.Fixture(fixture));
        WebSearchTool tool = Tool(Settings(backend), handler, secret: "key");

        ToolResult result = await Run(tool, """{"query":"xqzvbnmplkjhgfdsa"}""");

        Assert.False(result.IsError);
        Assert.Contains(
            "No results found for 'xqzvbnmplkjhgfdsa'.",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("blocked", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_api_backend_non_200_is_a_blocked_endpoint_of_that_endpoint()
    {
        using var handler = StubHttpHandler.Json(
            """{"error":"too many requests"}""",
            System.Net.HttpStatusCode.TooManyRequests
        );
        WebSearchTool tool = Tool(
            new WebSearchSettings
            {
                Backend = WebSearchBackendKind.Brave,
                Credential = "env:PROBE_KEY",
            },
            handler,
            secret: "key"
        );

        ToolResult result = await Run(tool, """{"query":"x"}""");

        Assert.True(result.IsError);
        Assert.Contains(
            "https://api.search.brave.com/res/v1/web/search",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains("429", result.Output, StringComparison.Ordinal);
        Assert.Contains("blocked", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rate-limit", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_api_answer_that_is_not_JSON_is_a_blocked_page()
    {
        using var handler = StubHttpHandler.Json("<html><body>Sign in</body></html>");
        WebSearchTool tool = Tool(
            new WebSearchSettings
            {
                Backend = WebSearchBackendKind.Tavily,
                Credential = "env:PROBE_KEY",
            },
            handler,
            secret: "key"
        );

        ToolResult result = await Run(tool, """{"query":"x"}""");

        Assert.True(result.IsError);
        Assert.Contains("https://api.tavily.com/search", result.Output, StringComparison.Ordinal);
        Assert.Contains("blocked", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No results found", result.Output, StringComparison.Ordinal);
    }

    private static WebSearchSettings Settings(string backend) =>
        backend == "brave"
            ? new WebSearchSettings
            {
                Backend = WebSearchBackendKind.Brave,
                Credential = "env:PROBE_KEY",
            }
            : new WebSearchSettings
            {
                Backend = WebSearchBackendKind.Tavily,
                Credential = "env:PROBE_KEY",
            };

    private static WebSearchTool Tool(
        WebSearchSettings settings,
        HttpMessageHandler handler,
        Func<string, string?>? environment = null,
        Func<string, string?>? namedKeys = null,
        string? secret = null
    ) =>
        new(
            new WebSearchConfiguration(
                settings,
                namedKeys,
                environment ?? (secret is null ? null : name => name == "PROBE_KEY" ? secret : null)
            ),
            handler: handler,
            timeout: WaitBudgets.WebStubRequest
        );

    private static Task<ToolResult> Run(WebSearchTool tool, string args) =>
        tool.ExecuteAsync(
            WebTestSupport.Args(args),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );
}
