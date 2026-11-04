using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

[Collection("setup-wizard")]
public sealed class SetupWizardModelCheckTests
{
    [Fact]
    public async Task The_model_check_uses_the_family_selected_from_discovery()
    {
        var factory = new FakeChatClientFactory(
            new ScriptedChatClient().Enqueue(Scripts.Text("OK"))
        );
        using var host = new InteractiveSessionHost(
            factory: factory,
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler(
                (_, _) =>
                    Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                """{"data":[{"id":"gpt-3.5-turbo","api":{"chat-completions":{}}}]}""",
                                Encoding.UTF8,
                                "application/json"
                            ),
                        }
                    )
            )
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText("model-check-test-key");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("Model (type to search");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model check: estimated cost");
        Assert.Contains("estimated cost <$0.0001", host.LastFrame, StringComparison.Ordinal);
        host.Console.SendUp();
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Profile name");

        Assert.Equal("gpt-3.5-turbo", factory.CreatedModel?.Id);
        Assert.Equal("openai-chat", factory.CreatedModel?.ApiFamily);
        host.Console.SendEscape();
        host.Console.Complete();
        await run;
    }

    [Fact]
    public async Task A_model_check_failure_names_the_model_family_and_status_and_can_go_back()
    {
        var client = new FailOnceChatClient();
        var factory = new FakeChatClientFactory(client);
        using var host = new InteractiveSessionHost(
            factory: factory,
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: DiscoveryResponse()
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await ReachModelCheckAsync(host);
        host.Console.SendUp();
        host.Console.SendEnter();
        await host.WaitForFrameAsync("After the failed model check");
        Assert.Contains(
            "the model check failed for gpt-3.5-turbo (openai-chat): HTTP 404",
            host.Frames,
            StringComparison.Ordinal
        );

        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model (type to search");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model check: estimated cost");
        host.Console.SendUp();
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Profile name");
        Assert.Equal(2, client.Calls);

        host.Console.SendEscape();
        host.Console.Complete();
        await run;
    }

    [Fact]
    public async Task A_custom_endpoint_is_preserved_when_the_model_id_exists_in_the_catalog()
    {
        var factory = new FakeChatClientFactory(
            new ScriptedChatClient().Enqueue(Scripts.Text("OK"))
        );
        var prompt = new ScriptedPrompt(
            "local",
            "https://local.example.test/v1",
            "",
            "",
            "custom-test-key",
            "chat-main"
        );
        using var host = new InteractiveSessionHost(
            factory: factory,
            interactiveConsole: true,
            freshInstall: true,
            secretPrompt: prompt,
            discoveryTransport: DiscoveryResponse()
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        for (var row = 0; row < ProviderPresets.All.Count; row++)
        {
            host.Console.Send(
                new Lunate.Tui.KeyEvent(Lunate.Tui.KeyKind.Down, null, false, false, false)
            );
        }

        await host.WaitForFrameAsync("> Custom OpenAI-compatible endpoint…");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Custom API family");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential placement");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for local");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("Model (type to search");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model check: estimated cost");
        host.Console.SendUp();
        host.Console.SendEnter();
        await host.WaitForFrameAsync("fast usage");

        Assert.Equal("https://local.example.test/v1", factory.CreatedModel?.Endpoint?.ToString());
        Assert.Equal("openai-chat", factory.CreatedModel?.ApiFamily);
        host.Console.SendEscape();
        host.Console.Complete();
        await run;
    }

    private static async Task ReachModelCheckAsync(InteractiveSessionHost host)
    {
        await host.WaitForFrameAsync("Select provider");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText("model-check-test-key");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("Model (type to search");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model check: estimated cost");
    }

    private static HttpMessageHandler DiscoveryResponse() =>
        new RecordingHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{"data":[{"id":"gpt-3.5-turbo","api":{"chat-completions":{}}}]}""",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                )
        );

    private sealed class ScriptedPrompt(params string?[] answers) : ISecretPrompt
    {
        private readonly Queue<string?> _answers = new(answers);

        public Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken) =>
            Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
    }

    private sealed class FailOnceChatClient : IChatClient
    {
        public int Calls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Calls++;
            return Calls == 1
                ? Task.FromException<ChatResponse>(new HttpRequestException("HTTP 404"))
                : Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
