using Lunate.Agent;
using Lunate.Ai;
using Lunate.Protocols.Acp;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// ACP's chat-model resolution: an editor session runs on the same <c>usages.chat</c> profile as
/// the interactive session and print mode, and the legacy top-level <c>model</c> still works when
/// no chat usage is configured.
/// </summary>
public sealed class AcpChatModelTests
{
    [Fact]
    public async Task CreateHarness_resolves_the_chat_usage_profile_with_no_top_level_model()
    {
        using var temp = new TempDirectory();
        ChatUsageProfileTestSupport.SaveProvider(temp);
        var client = new ScriptedChatClient().Enqueue(Scripts.Text("ok"), Scripts.Stop());
        var factory = new FakeChatClientFactory(client);
        AgentHarness harness = AcpMode.CreateHarness(
            new AcpSessionContext(temp.Root, null, null),
            null,
            [],
            [],
            null,
            factory,
            temp.File("sessions"),
            ChatUsageProfileTestSupport.Settings(),
            ModelCatalog.Load(temp.File("models.json")),
            authPath: temp.File("auth.json")
        );

        var events = new List<AgentEvent>();
        await foreach (
            AgentEvent agentEvent in harness.RunAsync("Hi", TestContext.Current.CancellationToken)
        )
        {
            events.Add(agentEvent);
        }

        ModelInfo model = Assert.IsType<ModelInfo>(factory.CreatedModel);
        Assert.Equal(ChatUsageProfileTestSupport.ModelId, model.Id);
        Assert.Equal(new Uri(ChatUsageProfileTestSupport.Endpoint), model.Endpoint);
        Assert.Equal("openai-chat", model.ApiFamily);
        Assert.Equal(
            ChatUsageProfileTestSupport.HeaderValue,
            model.ProviderHeaders![ChatUsageProfileTestSupport.HeaderName]
        );
        Assert.Equal(ChatUsageProfileTestSupport.CredentialPlacement, model.CredentialPlacement);
        string? answer = events.OfType<TextMessageContent>().LastOrDefault()?.Text;
        Assert.Contains("ok", answer, StringComparison.Ordinal);
        // The profile's reasoning level is the session's thinking level.
        Assert.Equal(ReasoningEffort.High, Assert.Single(client.RequestOptions)?.Reasoning?.Effort);
    }

    [Fact]
    public async Task CreateHarness_keeps_the_legacy_top_level_model_working()
    {
        using var temp = new TempDirectory();
        var client = new ScriptedChatClient().Enqueue(Scripts.Text("ok"), Scripts.Stop());
        var factory = new FakeChatClientFactory(client);
        AgentHarness harness = AcpMode.CreateHarness(
            new AcpSessionContext(temp.Root, null, null),
            null,
            [],
            [],
            null,
            factory,
            temp.File("sessions"),
            ChatUsageProfileTestSupport.LegacySettings(),
            ModelCatalog.Load(temp.File("models.json")),
            authPath: temp.File("auth.json")
        );

        await foreach (
            AgentEvent _ in harness.RunAsync("Hi", TestContext.Current.CancellationToken)
        ) { }

        ModelInfo model = Assert.IsType<ModelInfo>(factory.CreatedModel);
        Assert.Equal(ChatUsageProfileTestSupport.LegacyModelId, model.Id);
        Assert.Null(model.Endpoint);
    }
}
