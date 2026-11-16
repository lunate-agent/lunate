using System.Net;
using System.Text;
using Lunate.Ai;
using Lunate.Tui;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// <c>/auth</c>'s verification through the real factory (B-4): it runs before the first run, so a
/// fileless session has no session id. A preset whose headers template <c>{session.id}</c> must
/// still send its verification request instead of throwing on the missing identity.
/// </summary>
public sealed class InteractiveSessionAuthWireTests
{
    private const string Secret = "opencode-go-secret";

    [Fact]
    public async Task Verification_without_a_session_sends_the_header_templated_preset()
    {
        var requests = new List<HttpRequestMessage>();
        var factory = new ChatClientFactory(
            NullLoggerFactory.Instance,
            enableOpenTelemetry: false,
            namedKeySource: name => name == "opencode-go" ? Secret : "test-key"
        );
        factory.BaseTransportFactory = () => new CaptureHandler(requests);
        using var host = new InteractiveSessionHost(
            launch: new LaunchOptions { Model = "gpt-4o-mini", NoSession = true },
            factory: factory,
            secretPrompt: new ScriptedSecretPrompt(Secret)
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);
        await SaveOpenCodeGoAsync(host);

        host.Console.SendUp();
        await WaitForFrameAsync(host, "> Yes, send one request");
        host.Console.SendEnter();
        await WaitForFrameAsync(host, "verified opencode-go with one request");

        await host.Session.VerifyTask!.WaitAsync(
            InteractiveSessionHost.DefaultWait,
            TestContext.Current.CancellationToken
        );

        HttpRequestMessage request = Assert.Single(requests);
        string session = Assert.Single(request.Headers.GetValues("x-opencode-session"));
        Assert.False(string.IsNullOrWhiteSpace(session));

        host.Console.Complete();
        await run;
    }

    private static async Task SaveOpenCodeGoAsync(InteractiveSessionHost host)
    {
        host.Console.SendText("/auth");
        host.Console.SendEnter();
        await WaitForFrameAsync(host, "Select provider");

        int index = ProviderPresets.All.ToList().FindIndex(preset => preset.Id == "opencode-go");
        for (var press = 0; press < index; press++)
        {
            host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        }

        await WaitForFrameAsync(host, "> opencode-go — OpenCode Go");
        host.Console.SendEnter();
        await WaitForFrameAsync(host, "logged in: opencode-go");
        await WaitForFrameAsync(host, "Verify opencode-go with one request?");
    }

    private static Task WaitForFrameAsync(InteractiveSessionHost host, string text) =>
        host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return host.LastFrame.Contains(text, StringComparison.Ordinal);
        });

    private sealed class ScriptedSecretPrompt(params string?[] values) : ISecretPrompt
    {
        private readonly Queue<string?> _values = new(values);

        public Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken) =>
            Task.FromResult(_values.Count > 0 ? _values.Dequeue() : null);
    }

    /// <summary>The wire the factory's transport finally reaches: every request, in order.</summary>
    private sealed class CaptureHandler(List<HttpRequestMessage> requests) : HttpMessageHandler
    {
        private const string Response = """
            {
              "id": "chatcmpl-verify",
              "object": "chat.completion",
              "created": 0,
              "model": "beam-free",
              "choices": [
                {
                  "index": 0,
                  "message": { "role": "assistant", "content": "OK" },
                  "finish_reason": "stop"
                }
              ],
              "usage": { "prompt_tokens": 1, "completion_tokens": 1, "total_tokens": 2 }
            }
            """;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            lock (requests)
            {
                requests.Add(request);
            }

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Response, Encoding.UTF8, "application/json"),
                }
            );
        }
    }
}
