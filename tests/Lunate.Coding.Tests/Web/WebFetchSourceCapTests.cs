using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The 2 MiB source cap: the response stream is consumed up to the cap and not one byte further,
/// which a byte count on the read side observes where the rendered output cannot.
/// </summary>
public sealed class WebFetchSourceCapTests
{
    private const string PageUrl = "https://example.test/docs/page";

    [Fact]
    public async Task The_fetch_consumes_no_more_of_the_response_than_the_source_cap()
    {
        byte[] body = Encoding.UTF8.GetBytes(
            "<html><body>"
                + new string('a', WebFetchTool.MaxSourceBytes + (64 * 1024))
                + "</body></html>"
        );
        using var stream = new CountingReadStream(body);
        using var handler = new StubHttpHandler(
            (_, _) =>
            {
                var content = new StreamContent(stream);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(
                    "text/html; charset=utf-8"
                );
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
                );
            }
        );
        var tool = new WebFetchTool(handler, WaitBudgets.WebStubRequest);

        ToolResult result = await tool.ExecuteAsync(
            WebTestSupport.Args($$"""{"url":"{{PageUrl}}"}"""),
            WebTestSupport.Context(),
            TestContext.Current.CancellationToken
        );

        Assert.False(result.IsError);
        Assert.Equal(WebFetchTool.MaxSourceBytes, stream.BytesRead);
    }

    [Fact]
    public async Task The_capped_read_stops_at_the_cap_and_keeps_the_capped_bytes()
    {
        using var stream = CountingReadStream.Utf8(new string('a', 5_000));

        byte[] bytes = await WebText.ReadCappedAsync(
            stream,
            1_000,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(1_000, bytes.Length);
        Assert.Equal(1_000, stream.BytesRead);
    }

    [Fact]
    public async Task The_capped_read_returns_a_shorter_body_unchanged()
    {
        using var stream = CountingReadStream.Utf8("short body");

        byte[] bytes = await WebText.ReadCappedAsync(
            stream,
            1_000,
            TestContext.Current.CancellationToken
        );

        Assert.Equal("short body", Encoding.UTF8.GetString(bytes));
        Assert.Equal(10, stream.BytesRead);
    }
}
