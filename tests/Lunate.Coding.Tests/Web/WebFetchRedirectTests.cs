using System.Net;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// Redirect failures carry the next step the spec requires: every fetch error tells the model to
/// correct the URL or try another page, including a redirect it cannot follow.
/// </summary>
public sealed class WebFetchRedirectTests
{
    private const string PageUrl = "https://example.test/docs/page";

    [Fact]
    public async Task A_redirect_without_a_usable_location_names_the_url_and_the_next_step()
    {
        using var handler = new StubHttpHandler(
            (_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Found);
                response.Headers.Location = new Uri("file:///etc/passwd");
                return Task.FromResult(response);
            }
        );
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.True(result.IsError);
        Assert.Contains(PageUrl, result.Output, StringComparison.Ordinal);
        Assert.Contains("302", result.Output, StringComparison.Ordinal);
        Assert.Contains("Location", result.Output, StringComparison.Ordinal);
        Assert.Contains(
            "Correct the URL or try another page",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Single(handler.Requests);
    }
}
