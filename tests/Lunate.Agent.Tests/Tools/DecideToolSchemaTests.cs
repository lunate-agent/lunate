using System.Text.Json;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

public sealed class DecideToolSchemaTests
{
    [Fact]
    public void Decide_tool_description_teaches_the_atomic_question_rules()
    {
        var tool = new DecideTool(supportsImages: false);

        Assert.Contains("atomic questions", tool.Description, StringComparison.Ordinal);
        Assert.Contains("named state fields", tool.Description, StringComparison.Ordinal);
        Assert.Contains(
            "never splice untrusted content",
            tool.Description,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "count, compute or compare dates",
            tool.Description,
            StringComparison.Ordinal
        );
        Assert.Contains("low confidence as unknown", tool.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Box_question_is_offered_only_for_a_vision_capable_decision_model(
        bool supportsImages,
        bool expectsBox
    )
    {
        var chat = new ScriptedChatClient().Enqueue(LoopScripts.Stop());
        var decision = new ScriptedChatClient();
        var harness = new AgentHarness(
            chat,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                DecideTool = true,
                UsageModels = new Dictionary<string, UsageModelClient>(StringComparer.Ordinal)
                {
                    ["chat"] = new(
                        chat,
                        "chat-profile",
                        "openai",
                        new ModelInfo("chat-model", "openai", null, 64000, SupportsTools: true)
                    ),
                    ["decision"] = new(
                        decision,
                        "decision-profile",
                        "openai",
                        new ModelInfo("decision-model", "openai", null, 64000, SupportsTools: false)
                        {
                            SupportsVision = supportsImages,
                        }
                    ),
                },
            }
        );

        await AgentTestSupport.Run(harness);

        AIFunction decide = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(chat.Requests[0].Options!.Tools!, tool => tool.Name == "decide")
        );
        JsonElement kinds = decide
            .JsonSchema.GetProperty("properties")
            .GetProperty("questions")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("kind")
            .GetProperty("enum");
        Assert.Equal(expectsBox, kinds.EnumerateArray().Any(kind => kind.GetString() == "box"));
    }
}
