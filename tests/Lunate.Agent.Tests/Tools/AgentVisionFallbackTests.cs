using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

/// <summary>
/// The pre-send image check's fallback: a text-only chat model describes the image through the
/// configured fast profile, and a failing or absent profile never drops the image silently.
/// </summary>
public sealed class AgentVisionFallbackTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly byte[] PngBytes = SessionImageTestData.PngBytes;

    [Fact]
    public async Task A_text_only_chat_model_describes_the_image_through_the_fast_profile()
    {
        var chat = new ScriptedChatClient().Enqueue(LoopScripts.Text("seen"), LoopScripts.Stop());
        var vision = new ScriptedChatClient().Enqueue(LoopScripts.Text("A red square."));
        var harness = new AgentHarness(
            chat,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Model = TextOnlyModel(),
                UsageModels = new Dictionary<string, UsageModelClient>
                {
                    ["fast"] = Model(vision, "fast", VisionModel()),
                },
            }
        );

        List<AgentEvent> events = await harness
            .RunAsync(new UserInput("what is this?", [Image(2, 3)]), Ct)
            .ToListAsync(Ct);

        Assert.DoesNotContain(events, agentEvent => agentEvent is RunError);
        ScriptedRequest visionRequest = Assert.Single(vision.Requests);
        Assert.Single(
            visionRequest.Messages.SelectMany(message => message.Contents).OfType<DataContent>()
        );
        ScriptedRequest chatRequest = Assert.Single(chat.Requests);
        Assert.Empty(
            chatRequest.Messages.SelectMany(message => message.Contents).OfType<DataContent>()
        );
        string chatText = string.Concat(
            chatRequest
                .Messages.SelectMany(message => message.Contents)
                .OfType<TextContent>()
                .Select(content => content.Text)
        );
        Assert.Contains("A red square.", chatText, StringComparison.Ordinal);
        Assert.Contains("[image 2x3]", chatText, StringComparison.Ordinal);

        SteeringInjected transcript = Assert.Single(events.OfType<SteeringInjected>());
        Assert.Contains("A red square.", transcript.Text, StringComparison.Ordinal);
        Assert.Contains("fast", transcript.Text, StringComparison.Ordinal);
        Assert.Contains("[image 2x3]", transcript.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_fast_profile_is_used_even_when_another_usage_also_has_a_vision_model()
    {
        var chat = new ScriptedChatClient().Enqueue(LoopScripts.Text("seen"), LoopScripts.Stop());
        var fast = new ScriptedChatClient().Enqueue(LoopScripts.Text("fast description"));
        var other = new ScriptedChatClient().Enqueue(LoopScripts.Text("other description"));
        var harness = new AgentHarness(
            chat,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Model = TextOnlyModel(),
                UsageModels = new Dictionary<string, UsageModelClient>
                {
                    ["fast"] = Model(fast, "fast", VisionModel()),
                    ["compaction"] = Model(other, "other", VisionModel()),
                },
            }
        );

        await harness.RunAsync(new UserInput("look", [Image(2, 3)]), Ct).ToListAsync(Ct);

        Assert.Single(fast.Requests);
        Assert.Empty(other.Requests);
        Assert.Contains(
            "fast description",
            string.Concat(
                chat.Requests[0]
                    .Messages.SelectMany(message => message.Contents)
                    .OfType<TextContent>()
                    .Select(content => content.Text)
            ),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task A_text_only_fast_profile_leaves_the_image_refusal_unchanged()
    {
        var chat = new ScriptedChatClient().Enqueue(LoopScripts.Text("ok"), LoopScripts.Stop());
        var fast = new ScriptedChatClient().Enqueue(LoopScripts.Text("not used"));
        var harness = new AgentHarness(
            chat,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Model = TextOnlyModel(),
                UsageModels = new Dictionary<string, UsageModelClient>
                {
                    ["fast"] = Model(fast, "fast", TextOnlyModel()),
                },
            }
        );

        List<AgentEvent> events = await harness
            .RunAsync(new UserInput("look", [Image(2, 3)]), Ct)
            .ToListAsync(Ct);

        RunError error = Assert.Single(events.OfType<RunError>());
        Assert.Contains(
            "model text-only-test doesn't accept images; switch profile or remove the image",
            error.Message,
            StringComparison.Ordinal
        );
        Assert.Empty(chat.Requests);
        Assert.Empty(fast.Requests);
    }

    [Fact]
    public async Task A_vision_profile_failure_names_both_errors_and_sends_no_chat_request()
    {
        var chat = new ScriptedChatClient().Enqueue(LoopScripts.Text("ok"), LoopScripts.Stop());
        var vision = new ScriptedChatClient().EnqueueFailure(
            new InvalidOperationException("vision endpoint is down")
        );
        var harness = new AgentHarness(
            chat,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Model = TextOnlyModel(),
                UsageModels = new Dictionary<string, UsageModelClient>
                {
                    ["fast"] = Model(vision, "fast", VisionModel()),
                },
            }
        );

        List<AgentEvent> events = await harness
            .RunAsync(new UserInput("look", [Image(2, 3)]), Ct)
            .ToListAsync(Ct);

        RunError error = Assert.Single(events.OfType<RunError>());
        Assert.Contains("text-only-test", error.Message, StringComparison.Ordinal);
        Assert.Contains("doesn't accept images", error.Message, StringComparison.Ordinal);
        Assert.Contains("fast", error.Message, StringComparison.Ordinal);
        Assert.Contains("vision endpoint is down", error.Message, StringComparison.Ordinal);
        Assert.Contains("[image 2x3]", error.Message, StringComparison.Ordinal);
        Assert.Empty(chat.Requests);
        Assert.Single(vision.Requests);
    }

    [Fact]
    public async Task A_resumed_image_on_a_text_only_model_uses_the_fast_profile()
    {
        using var temp = new TempDirectory();
        Session created = SessionImageTestData.CreateSession(temp);
        ImageContentReference reference = created.Images.Store(PngBytes, "image/png", 2, 3);
        created.AppendMessage(new ChatMessage(ChatRole.User, [reference]));
        Session loaded = Session.Load(created.Path);
        var chat = new ScriptedChatClient().Enqueue(LoopScripts.Text("ok"), LoopScripts.Stop());
        var vision = new ScriptedChatClient().Enqueue(LoopScripts.Text("A resumed image."));
        var harness = new AgentHarness(
            chat,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Session = loaded,
                Model = TextOnlyModel(),
                UsageModels = new Dictionary<string, UsageModelClient>
                {
                    ["fast"] = Model(vision, "fast", VisionModel()),
                },
            }
        );

        await harness.RunAsync("go", Ct).ToListAsync(Ct);

        Assert.Single(vision.Requests);
        ScriptedRequest chatRequest = Assert.Single(chat.Requests);
        Assert.Empty(
            chatRequest.Messages.SelectMany(message => message.Contents).OfType<DataContent>()
        );
        Assert.Contains(
            "A resumed image.",
            string.Concat(
                chatRequest
                    .Messages.SelectMany(message => message.Contents)
                    .OfType<TextContent>()
                    .Select(content => content.Text)
            ),
            StringComparison.Ordinal
        );
        string json = File.ReadAllText(loaded.Path);
        Assert.Contains("\"$type\":\"image\"", json, StringComparison.Ordinal);
    }

    private static UsageModelClient Model(
        ScriptedChatClient client,
        string profile,
        ModelInfo model
    ) => new(client, profile, model.Provider, model);

    private static ModelInfo VisionModel() =>
        new("vision-test", "openai", null, 100000, true) { SupportsVision = true };

    private static ModelInfo TextOnlyModel() =>
        new("text-only-test", "openai", null, 100000, true)
        {
            Capabilities = new ModelCapabilityDescriptor(
                inputModalities: new HashSet<string>(["text"], StringComparer.Ordinal)
            ),
        };

    private static ImageAttachment Image(int width, int height) =>
        new("image/png", width, height, PngBytes);
}
