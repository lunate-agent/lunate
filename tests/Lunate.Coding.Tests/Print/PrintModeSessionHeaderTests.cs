using System.Net;
using System.Text;
using Lunate.Ai;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// A fileless run still has a conversation identity. This drives <c>PrintMode.RunAsync</c> end to
/// end over the real client factory and HTTP pipeline for the OpenCode Go preset, whose
/// <c>x-opencode-session: {session.id}</c> header used to fail every request under
/// <c>--no-session</c>.
/// </summary>
public sealed class PrintModeSessionHeaderTests
{
    [Fact]
    public async Task A_no_session_run_sends_an_ephemeral_session_id_for_the_header_template()
    {
        using var temp = new TempDirectory();
        var wire = new CapturedWire();
        var factory = new ChatClientFactory(
            NullLoggerFactory.Instance,
            enableOpenTelemetry: false,
            namedKeySource: name => name == "opencode-go" ? "fixture-only-key" : null
        );
        factory.BaseTransportFactory = () => wire;
        PrintModeOptions options = PrintModeTestSupport.BaseOptions(temp, factory) with
        {
            Prompt = "Hi",
            Launch = new LaunchOptions { NoSession = true },
            Environment = PrintModeTestSupport.Environment(model: "opencode-go/beam-free"),
        };

        (int exitCode, string output, string errors) = await PrintModeTestSupport.RunAsync(options);

        Assert.Equal(0, exitCode);
        Assert.Equal("ok", output);
        Assert.Empty(errors);
        Assert.NotNull(wire.Last);
        string session = Assert.Single(wire.Last!.Headers.GetValues("x-opencode-session"));
        Assert.True(
            Guid.TryParse(session, out _),
            $"the fileless run should send an ephemeral UUID, but sent '{session}'"
        );
    }

    /// <summary>The streaming response the OpenAI-compatible chat wire returns.</summary>
    private const string StreamBody = """
        data: {"id":"chatcmpl-no-session","object":"chat.completion.chunk","created":0,"model":"beam-free","choices":[{"index":0,"delta":{"role":"assistant","content":"ok"},"finish_reason":null}]}

        data: {"id":"chatcmpl-no-session","object":"chat.completion.chunk","created":0,"model":"beam-free","choices":[{"index":0,"delta":{},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}

        data: [DONE]

        """;

    private sealed class CapturedWire : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Last = request;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(StreamBody, Encoding.UTF8, "text/event-stream"),
                }
            );
        }
    }
}
