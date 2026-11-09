using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

public sealed class InteractiveSessionUsageProfileTests
{
    [Fact]
    public async Task Interactive_decision_profile_lists_decide_in_the_prompt_and_tool_declarations()
    {
        using var temp = new TempDirectory();
        string modelsPath = temp.File("models.json");
        File.WriteAllText(
            modelsPath,
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "chat-model", "provider": "openai", "contextWindow": 64000,
                  "supportsTools": true },
                { "id": "decision-model", "provider": "openai", "contextWindow": 64000,
                  "supportsTools": false }
              ]
            }
            """
        );
        var chat = new ScriptedChatClient().Enqueue(
            new ChatResponseUpdate(ChatRole.Assistant, []) { FinishReason = ChatFinishReason.Stop }
        );
        var factory = new UsageFactory(chat);
        using var host = new InteractiveSessionHost(factory: factory, modelsPath: modelsPath);
        File.WriteAllText(
            host.Temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "chat-profile": {
                  "provider": "chat-provider",
                  "model": "chat-model"
                },
                "decision-profile": {
                  "provider": "chat-provider",
                  "model": "decision-model"
                }
              },
              "usages": { "chat": "chat-profile" },
              "decisions": { "profile": "decision-profile", "tool": "on" }
            }
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            [Provider("chat-provider", "openai")]
        );
        Task run = host.RunAsync();

        host.Console.SendText("hello");
        host.Console.SendEnter();
        await host.WaitUntilAsync(() => chat.Requests.Count > 0);

        var request = chat.Requests[0];
        Assert.Contains("decide", request[0].Text, StringComparison.Ordinal);
        Assert.Contains(
            chat.RequestOptions[0]!.Tools!,
            declaration => declaration.Name == "decide"
        );

        host.Console.Complete();
        await run;
    }

    [Fact]
    public async Task Interactive_decision_profile_offered_without_the_tool_switch_offers_nothing()
    {
        using var temp = new TempDirectory();
        string modelsPath = temp.File("models.json");
        File.WriteAllText(
            modelsPath,
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "chat-model", "provider": "openai", "contextWindow": 64000,
                  "supportsTools": true },
                { "id": "decision-model", "provider": "openai", "contextWindow": 64000,
                  "supportsTools": false }
              ]
            }
            """
        );
        var chat = new ScriptedChatClient().Enqueue(
            new ChatResponseUpdate(ChatRole.Assistant, []) { FinishReason = ChatFinishReason.Stop }
        );
        var factory = new UsageFactory(chat);
        using var host = new InteractiveSessionHost(factory: factory, modelsPath: modelsPath);
        File.WriteAllText(
            host.Temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "chat-profile": {
                  "provider": "chat-provider",
                  "model": "chat-model"
                },
                "decision-profile": {
                  "provider": "chat-provider",
                  "model": "decision-model"
                }
              },
              "usages": { "chat": "chat-profile" },
              "decisions": { "profile": "decision-profile", "tool": "off" }
            }
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            [Provider("chat-provider", "openai")]
        );
        Task run = host.RunAsync();

        host.Console.SendText("hello");
        host.Console.SendEnter();
        await host.WaitUntilAsync(() => chat.Requests.Count > 0);

        Assert.DoesNotContain(
            chat.RequestOptions[0]!.Tools!,
            declaration => declaration.Name == "decide"
        );

        host.Console.Complete();
        await run;
    }

    [Fact]
    public async Task Tool_on_without_a_profile_offers_nothing_and_reports_no_error()
    {
        using var temp = new TempDirectory();
        string modelsPath = temp.File("models.json");
        File.WriteAllText(
            modelsPath,
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "chat-model", "provider": "openai", "contextWindow": 64000,
                  "supportsTools": true }
              ]
            }
            """
        );
        var chat = new ScriptedChatClient().Enqueue(
            new ChatResponseUpdate(ChatRole.Assistant, []) { FinishReason = ChatFinishReason.Stop }
        );
        var factory = new UsageFactory(chat);
        using var host = new InteractiveSessionHost(factory: factory, modelsPath: modelsPath);
        File.WriteAllText(
            host.Temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "chat-profile": {
                  "provider": "chat-provider",
                  "model": "chat-model"
                }
              },
              "usages": { "chat": "chat-profile" },
              "decisions": { "tool": "on" }
            }
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            [Provider("chat-provider", "openai")]
        );
        Task run = host.RunAsync();

        host.Console.SendText("hello");
        host.Console.SendEnter();
        await host.WaitUntilAsync(() => chat.Requests.Count > 0);

        Assert.DoesNotContain(
            chat.RequestOptions[0]!.Tools!,
            declaration => declaration.Name == "decide"
        );

        host.Console.Complete();
        await run;
    }

    [Fact]
    public async Task Interactive_chat_uses_the_configured_profile_and_emits_attribution_once()
    {
        using var temp = new TempDirectory();
        string modelsPath = temp.File("models.json");
        File.WriteAllText(
            modelsPath,
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "chat-model", "provider": "openai", "contextWindow": 64000,
                  "supportsTools": true }
              ]
            }
            """
        );
        var chat = new ScriptedChatClient().Enqueue(
            new ChatResponseUpdate(
                ChatRole.Assistant,
                [
                    new TextContent("answer"),
                    new UsageContent(
                        new UsageDetails { InputTokenCount = 12, OutputTokenCount = 3 }
                    ),
                ]
            )
            {
                FinishReason = ChatFinishReason.Stop,
            }
        );
        var factory = new UsageFactory(chat);
        using var host = new InteractiveSessionHost(factory: factory, modelsPath: modelsPath);
        File.WriteAllText(
            host.Temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "chat-profile": {
                  "provider": "chat-provider",
                  "model": "chat-model"
                }
              },
              "usages": { "chat": "chat-profile" }
            }
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            [Provider("chat-provider", "openai")]
        );
        Task run = host.RunAsync();

        host.Console.SendText("hello");
        host.Console.SendEnter();
        await host.WaitUntilAsync(() => host.Events.OfType<UsageUpdated>().Any());

        UsageUpdated usage = Assert.Single(host.Events.OfType<UsageUpdated>());
        Assert.Equal("chat-model", usage.ModelId);
        Assert.Equal(
            new ModelUsageAttribution("chat", "chat-profile", "chat-provider", "openai"),
            usage.Attribution
        );
        Assert.Single(factory.Models);
        Assert.Equal("chat-model", factory.Models[0].Id);
        Assert.Equal(new Uri("https://provider.example.test/v1"), factory.Models[0].Endpoint);

        host.Console.Complete();
        await run;
    }

    [Fact]
    public async Task A_refused_chat_binding_refuses_the_first_message_with_the_binding_message()
    {
        var factory = new FakeChatClientFactory(new ScriptedChatClient());
        using var host = new InteractiveSessionHost(
            factory: factory,
            environment: _ => null,
            interactiveConsole: true
        );
        // The notice caps at four lines; a wide terminal keeps the whole binding message on screen.
        host.Console.Size = new Lunate.Tui.ConsoleSize(400, 24);
        File.WriteAllText(
            host.Temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "decisions": {
                  "provider": "opencode-go",
                  "model": "opencode-go/jev-1.13",
                  "apiFamily": "systemone"
                }
              },
              "usages": { "chat": "decisions" },
              "decisions": { "profile": "decisions" }
            }
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            [Provider("opencode-go", "opencode-go")]
        );
        Task run = host.RunAsync();

        host.Console.SendText("hello");
        host.Console.SendEnter();
        await host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return host.LastFrame.Contains("run failed", StringComparison.Ordinal);
        });

        string notice = host.LastFrame;
        Assert.Contains("Profile 'decisions'", notice, StringComparison.Ordinal);
        Assert.Contains("opencode-go/jev-1.13", notice, StringComparison.Ordinal);
        Assert.Contains("decision-only", notice, StringComparison.Ordinal);
        Assert.Contains("systemone", notice, StringComparison.Ordinal);
        Assert.Contains("/setup", notice, StringComparison.Ordinal);
        Assert.Contains("lunate setup", notice, StringComparison.Ordinal);
        Assert.DoesNotContain("no chat model is configured", notice, StringComparison.Ordinal);
        Assert.Null(factory.CreatedModel);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    [Fact]
    public async Task A_session_with_no_chat_answer_refuses_the_first_message_with_the_setup_text()
    {
        var factory = new FakeChatClientFactory(new ScriptedChatClient());
        using var host = new InteractiveSessionHost(
            factory: factory,
            environment: _ => null,
            interactiveConsole: true,
            freshInstall: true,
            // A fresh machine builds the placeholder; the picker seam keeps the setup walk from
            // opening, so a first message reaches the placeholder with no chat answer.
            startWithPicker: true
        );
        // The notice caps at four lines; a wide terminal keeps the whole refusal on screen.
        host.Console.Size = new Lunate.Tui.ConsoleSize(400, 24);
        Task run = host.RunAsync();
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "Select session");
        host.Console.SendEscape();
        await InteractiveSessionCommandTests.WaitForFrameGoneAsync(host, "Select session");

        host.Console.SendText("hello");
        host.Console.SendEnter();
        await host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return host.LastFrame.Contains("run failed", StringComparison.Ordinal);
        });

        string notice = host.LastFrame;
        Assert.Contains("no chat model is configured", notice, StringComparison.Ordinal);
        Assert.Contains("lunate setup", notice, StringComparison.Ordinal);
        Assert.Null(factory.CreatedModel);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    private static ProviderEntry Provider(string name, string kind)
    {
        bool valid = ProviderEntry.TryCreate(
            name,
            kind,
            new Uri("https://provider.example.test/v1"),
            $"auth:{name}",
            headers: null,
            region: null,
            out ProviderEntry? provider,
            out string? error
        );

        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(provider);
    }

    private sealed class UsageFactory(ScriptedChatClient client) : IChatClientFactory
    {
        public List<ModelInfo> Models { get; } = [];

        public IChatClient Create(ModelInfo model)
        {
            Models.Add(model);
            return client;
        }

        public IChatClient Create(ModelInfo model, string? apiKey) => Create(model);
    }
}
