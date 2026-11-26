using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

/// <summary>
/// The provider-native search layer: with the opt-in on, the request's model declaring the
/// <c>web_search</c> feature, and the tool selection offering <c>web_search</c>, the request carries
/// MEAI's hosted search instead of the local function. Every other combination keeps the local tool
/// and the chat call's attribution.
/// </summary>
public sealed class NativeWebSearchTests
{
    [Fact]
    public async Task The_opt_in_and_a_declaring_model_declare_the_hosted_search()
    {
        ScriptedChatClient client = await RunAsync(
            optIn: true,
            model: ModelWithWebSearch(),
            options: null
        );

        IList<AITool> tools = client.Requests[0].Options!.Tools!;

        Assert.Single(tools.OfType<HostedWebSearchTool>());
        Assert.DoesNotContain(tools.OfType<AIFunction>(), tool =>
            string.Equals(tool.Name, "web_search", StringComparison.Ordinal)
        );
        Assert.Contains(tools.OfType<AIFunction>(), tool =>
            string.Equals(tool.Name, "web_fetch", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Without_the_opt_in_the_local_tool_stays_and_no_native_search_is_declared()
    {
        ScriptedChatClient client = await RunAsync(
            optIn: false,
            model: ModelWithWebSearch(),
            options: null
        );

        IList<AITool> tools = client.Requests[0].Options!.Tools!;

        Assert.DoesNotContain(tools, tool => tool is HostedWebSearchTool);
        Assert.Contains(tools.OfType<AIFunction>(), tool =>
            string.Equals(tool.Name, "web_search", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task A_model_without_the_feature_keeps_the_local_tool_even_when_opted_in()
    {
        ScriptedChatClient client = await RunAsync(
            optIn: true,
            model: ModelWithFeatures("tools"),
            options: null
        );

        IList<AITool> tools = client.Requests[0].Options!.Tools!;

        Assert.DoesNotContain(tools, tool => tool is HostedWebSearchTool);
        Assert.Contains(tools.OfType<AIFunction>(), tool =>
            string.Equals(tool.Name, "web_search", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task The_tool_selection_still_governs_the_native_declaration()
    {
        ScriptedChatClient client = await RunAsync(
            optIn: true,
            model: ModelWithWebSearch(),
            options: new AgentHarnessOptions
            {
                Model = ModelWithWebSearch(),
                ModelId = ModelWithWebSearch().Id,
                NativeWebSearch = true,
                Hooks = new RecordingHookPoints
                {
                    RunStarting = static _ => new AgentRunStartingResult.Apply([], ["read"]),
                },
            }
        );

        IList<AITool> tools = client.Requests[0].Options!.Tools!;

        Assert.DoesNotContain(tools, tool => tool is HostedWebSearchTool);
        Assert.DoesNotContain(tools.OfType<AIFunction>(), tool =>
            string.Equals(tool.Name, "web_search", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Billing_and_attribution_stay_on_the_chat_call()
    {
        ModelInfo model = ModelWithWebSearch();
        var usageClient = new ScriptedChatClient().Enqueue(
            LoopScripts.Usage(9, 2),
            LoopScripts.Text("searched"),
            LoopScripts.Stop()
        );
        var harness = new AgentHarness(
            usageClient,
            Registry(),
            new AgentHarnessOptions
            {
                NativeWebSearch = true,
                UsageModels = new Dictionary<string, UsageModelClient>(StringComparer.Ordinal)
                {
                    ["chat"] = new(usageClient, "chat-profile", "openai", model),
                },
            }
        );

        List<AgentEvent> events = await AgentTestSupport.Run(harness);

        UsageUpdated usage = Assert.Single(events.OfType<UsageUpdated>());
        Assert.Equal("chat", usage.Attribution?.Usage);
        Assert.Equal(model.Id, usage.ModelId);
        Assert.Equal(9L, usage.Usage.InputTokenCount);
        Assert.Equal(2L, usage.Usage.OutputTokenCount);
        Assert.Single(usageClient.Requests[0].Options!.Tools!.OfType<HostedWebSearchTool>());
    }

    private static async Task<ScriptedChatClient> RunAsync(
        bool optIn,
        ModelInfo model,
        AgentHarnessOptions? options
    )
    {
        var client = new ScriptedChatClient().Enqueue(LoopScripts.Text("ok"), LoopScripts.Stop());
        options ??= new AgentHarnessOptions();
        var harness = new AgentHarness(
            client,
            Registry(),
            options with
            {
                NativeWebSearch = optIn,
                Model = model,
                ModelId = model.Id,
            }
        );

        await AgentTestSupport.Run(harness);
        return client;
    }

    private static ToolRegistry Registry() =>
        AgentTestSupport.Registry(
            new ScriptedTool("web_search", "Searches the web.", """{"type":"object"}"""),
            new ScriptedTool("web_fetch", "Fetches a page.", """{"type":"object"}""")
        );

    private static ModelInfo ModelWithWebSearch() => ModelWithFeatures("tools", "web_search");

    private static ModelInfo ModelWithFeatures(params string[] features) =>
        new("native-search-model", "openai", null, 64_000, SupportsTools: true)
        {
            Capabilities = new ModelCapabilityDescriptor(
                features: new HashSet<string>(features, StringComparer.Ordinal)
            ),
        };
}
