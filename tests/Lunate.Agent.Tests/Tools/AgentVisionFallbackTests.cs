using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

/// <summary>
/// The pre-send image fallback: a text-only chat model describes the image through the configured
/// <c>vision</c> usage, attributes that call as <c>vision</c>, and a failing or absent profile never
/// drops the image silently.
/// </summary>
public sealed class AgentVisionFallbackTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly byte[] PngBytes = SessionImageTestData.PngBytes;

    [Fact]
    public async Task A_text_only_chat_model_describes_the_image_through_the_vision_profile()
    {
        var chat = new ScriptedChatClient().Enqueue(LoopScripts.Text("seen"), LoopScripts.Stop());
        var vision = new ScriptedChatClient().Enqueue(
            LoopScripts.Text("A red square."),
            LoopScripts.Usage(11, 3)
        );
        var harness = new AgentHarness(
            chat,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Model = TextOnlyModel(),
                UsageModels = new Dictionary<string, UsageModelClient>
                {
                    ["vision"] = Model(vision, "sight", VisionModel()),
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

        ImageDescribed transcript = Assert.Single(events.OfType<ImageDescribed>());
        Assert.Equal("[image 2x3]", transcript.Placeholder);
        Assert.Equal("A red square.", transcript.Description);
        Assert.Equal("sight", transcript.Profile);
        Assert.Equal("vision-test", transcript.ModelId);

        UsageUpdated usage = Assert.Single(events.OfType<UsageUpdated>());
        Assert.Equal("vision", usage.Attribution?.Usage);
        Assert.Equal("sight", usage.Attribution?.ProfileName);
    }

    [Fact]
    public async Task The_vision_profile_is_used_even_when_another_usage_also_has_a_vision_model()
    {
        var chat = new ScriptedChatClient().Enqueue(LoopScripts.Text("seen"), LoopScripts.Stop());
        var vision = new ScriptedChatClient().Enqueue(LoopScripts.Text("vision description"));
        var other = new ScriptedChatClient().Enqueue(LoopScripts.Text("other description"));
        var harness = new AgentHarness(
            chat,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Model = TextOnlyModel(),
                UsageModels = new Dictionary<string, UsageModelClient>
                {
                    ["vision"] = Model(vision, "sight", VisionModel()),
                    ["compaction"] = Model(other, "other", VisionModel()),
                },
            }
        );

        await harness.RunAsync(new UserInput("look", [Image(2, 3)]), Ct).ToListAsync(Ct);

        Assert.Single(vision.Requests);
        Assert.Empty(other.Requests);
        Assert.Contains(
            "vision description",
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
    public async Task A_text_only_vision_profile_leaves_the_image_refusal_unchanged()
    {
        var chat = new ScriptedChatClient().Enqueue(LoopScripts.Text("ok"), LoopScripts.Stop());
        var vision = new ScriptedChatClient().Enqueue(LoopScripts.Text("not used"));
        var harness = new AgentHarness(
            chat,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Model = TextOnlyModel(),
                UsageModels = new Dictionary<string, UsageModelClient>
                {
                    ["vision"] = Model(vision, "sight", TextOnlyModel()),
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
        Assert.DoesNotContain(events, agentEvent => agentEvent is ImageDescribed);
        Assert.Empty(chat.Requests);
        Assert.Empty(vision.Requests);
    }

    [Fact]
    public async Task A_fast_profile_with_a_vision_model_does_not_trigger_the_fallback()
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
                    ["fast"] = Model(fast, "fast", VisionModel()),
                },
            }
        );

        List<AgentEvent> events = await harness
            .RunAsync(new UserInput("look", [Image(2, 3)]), Ct)
            .ToListAsync(Ct);

        RunError error = Assert.Single(events.OfType<RunError>());
        Assert.Contains("doesn't accept images", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(events, agentEvent => agentEvent is ImageDescribed);
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
                    ["vision"] = Model(vision, "sight", VisionModel()),
                },
            }
        );

        List<AgentEvent> events = await harness
            .RunAsync(new UserInput("look", [Image(2, 3)]), Ct)
            .ToListAsync(Ct);

        RunError error = Assert.Single(events.OfType<RunError>());
        Assert.Contains("text-only-test", error.Message, StringComparison.Ordinal);
        Assert.Contains("doesn't accept images", error.Message, StringComparison.Ordinal);
        Assert.Contains("sight", error.Message, StringComparison.Ordinal);
        Assert.Contains("vision endpoint is down", error.Message, StringComparison.Ordinal);
        Assert.Contains("[image 2x3]", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(events, agentEvent => agentEvent is ImageDescribed);
        Assert.Empty(chat.Requests);
        Assert.Single(vision.Requests);
    }

    [Fact]
    public async Task A_resumed_image_on_a_text_only_model_uses_the_vision_profile()
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
                    ["vision"] = Model(vision, "sight", VisionModel()),
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
