using System.Security.Cryptography;
using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The image-generation tool over a stub client: the placeholder and typed attachment, the optional
/// workspace write under the write tool's path rules and risk, the image limits, the actionable
/// errors, and the <c>image</c> usage attribution. No network is reached.
/// </summary>
public sealed class GenerateImageToolTests
{
    [Fact]
    public async Task A_generated_image_is_attached_with_the_placeholder()
    {
        using var temp = new TempDirectory();
        var client = new StubImageClient(ImageSamples.Png(2, 3));
        ToolResult result = await RunAsync(temp, client, """{"prompt":"a red square"}""");

        Assert.False(result.IsError, result.Output);
        Assert.Equal("[image 2x3]", result.Output);
        ImageAttachment image = Assert.Single(result.Images!);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(2, image.Width);
        Assert.Equal(3, image.Height);
        Assert.Equal(ImageSamples.Png(2, 3), image.Data.ToArray());
        Assert.Equal("a red square", client.Prompt);
    }

    [Fact]
    public async Task A_path_writes_the_bytes_and_reports_the_relative_path()
    {
        using var temp = new TempDirectory();
        var workspace = new Workspace(temp.Root);
        var sink = new RecordingSink();
        var client = new StubImageClient(ImageSamples.Png(4, 5));
        ToolResult result = await RunAsync(
            temp,
            client,
            """{"prompt":"a hero","path":"images/hero.png"}""",
            sink: sink
        );

        Assert.False(result.IsError, result.Output);
        Assert.Equal("[image 4x5] wrote images/hero.png", result.Output);
        Assert.Equal(ImageSamples.Png(4, 5), File.ReadAllBytes(temp.File("images/hero.png")));
        FileChangeEvent change = Assert.Single(sink.Events);
        Assert.Equal("images/hero.png", change.RelativePath);
        Assert.Equal(FileChangeKind.Created, change.Kind);
        Assert.Equal(workspace.WorktreeRoot, change.WorkspaceId);
    }

    [Fact]
    public async Task A_path_outside_the_workspace_is_refused_before_generating()
    {
        using var temp = new TempDirectory();
        var client = new StubImageClient(ImageSamples.Png(2, 3));
        ToolResult result = await RunAsync(
            temp,
            client,
            """{"prompt":"a cat","path":"../outside.png"}"""
        );

        Assert.True(result.IsError);
        Assert.Contains("outside the workspace", result.Output, StringComparison.Ordinal);
        Assert.Contains("../outside.png", result.Output, StringComparison.Ordinal);
        Assert.Equal(0, client.Calls);
        Assert.Null(result.Images);
    }

    [Fact]
    public async Task A_directory_target_is_refused_before_generating()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.File("images"));
        var client = new StubImageClient(ImageSamples.Png(2, 3));
        ToolResult result = await RunAsync(temp, client, """{"prompt":"a cat","path":"images"}""");

        Assert.True(result.IsError);
        Assert.Contains("is a directory", result.Output, StringComparison.Ordinal);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task A_failed_workspace_write_keeps_the_image_attached()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(temp.File("blocked"), "not a directory");
        var client = new StubImageClient(ImageSamples.Png(2, 3));
        ToolResult result = await RunAsync(
            temp,
            client,
            """{"prompt":"a cat","path":"blocked/hero.png"}"""
        );

        Assert.True(result.IsError);
        Assert.Contains("blocked/hero.png", result.Output, StringComparison.Ordinal);
        Assert.Contains("still attached", result.Output, StringComparison.Ordinal);
        Assert.Single(result.Images!);
    }

    [Fact]
    public async Task A_provider_failure_is_an_error_naming_the_profile_and_model()
    {
        using var temp = new TempDirectory();
        var client = new StubImageClient(ImageSamples.Png(2, 3))
        {
            Failure = new InvalidOperationException("the endpoint refused the request"),
        };
        ToolResult result = await RunAsync(temp, client, """{"prompt":"a cat"}""");

        Assert.True(result.IsError);
        Assert.Contains("image-profile", result.Output, StringComparison.Ordinal);
        Assert.Contains("gpt-image-1", result.Output, StringComparison.Ordinal);
        Assert.Contains("usages.image", result.Output, StringComparison.Ordinal);
        Assert.Null(result.Images);
    }

    [Fact]
    public async Task Bytes_over_the_image_limit_are_refused()
    {
        using var temp = new TempDirectory();
        var client = new StubImageClient(ImageSamples.PaddedPng(2, 3, 6 * 1024 * 1024));
        ToolResult result = await RunAsync(temp, client, """{"prompt":"a cat"}""");

        Assert.True(result.IsError);
        Assert.Contains("6.0 MB", result.Output, StringComparison.Ordinal);
        Assert.Contains("5.0 MB", result.Output, StringComparison.Ordinal);
        Assert.Contains(
            "downscaling is not available yet",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Null(result.Images);
    }

    [Fact]
    public async Task A_dimension_over_the_limit_is_refused()
    {
        using var temp = new TempDirectory();
        var client = new StubImageClient(ImageSamples.Png(5000, 10));
        ToolResult result = await RunAsync(temp, client, """{"prompt":"a cat"}""");

        Assert.True(result.IsError);
        Assert.Contains("5000x10", result.Output, StringComparison.Ordinal);
        Assert.Contains("4096", result.Output, StringComparison.Ordinal);
        Assert.Null(result.Images);
    }

    [Fact]
    public async Task Bytes_that_are_not_a_supported_image_are_refused()
    {
        using var temp = new TempDirectory();
        var client = new StubImageClient("not an image"u8.ToArray());
        ToolResult result = await RunAsync(temp, client, """{"prompt":"a cat"}""");

        Assert.True(result.IsError);
        Assert.Contains("PNG, JPEG, GIF or WebP", result.Output, StringComparison.Ordinal);
        Assert.Null(result.Images);
    }

    [Fact]
    public async Task The_call_is_attributed_as_image_usage()
    {
        using var temp = new TempDirectory();
        var recorded =
            new List<(UsageDetails Usage, string ModelId, ModelUsageAttribution Value)>();
        var client = new StubImageClient(ImageSamples.Png(2, 3));
        ToolResult result = await RunAsync(
            temp,
            client,
            """{"prompt":"a cat"}""",
            record: (usage, modelId, attribution) => recorded.Add((usage, modelId, attribution))
        );

        Assert.False(result.IsError, result.Output);
        (UsageDetails usage, string modelId, ModelUsageAttribution attribution) = Assert.Single(
            recorded
        );
        Assert.Equal(10, usage.InputTokenCount);
        Assert.Equal("gpt-image-1", modelId);
        Assert.Equal("image", attribution.Usage);
        Assert.Equal("image-profile", attribution.ProfileName);
        Assert.Equal("fake-provider", attribution.ProviderName);
        Assert.Equal("openai", attribution.ProviderKind);
    }

    [Fact]
    public void The_tool_carries_the_write_risk()
    {
        using var temp = new TempDirectory();
        var tool = new GenerateImageTool(
            new Workspace(temp.Root),
            new ImageGenerationBinding(
                new StubImageClient(ImageSamples.Png(2, 3)),
                "image-profile",
                "fake-provider",
                "openai",
                "gpt-image-1"
            )
        );

        Assert.Equal(ToolRisk.Write, tool.Risk);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"prompt":"   "}""")]
    [InlineData("""{"prompt":"a cat","size":"1024x1024"}""")]
    public async Task Bad_arguments_are_refused_without_a_generation_call(string args)
    {
        using var temp = new TempDirectory();
        var client = new StubImageClient(ImageSamples.Png(2, 3));
        ToolResult result = await RunAsync(temp, client, args);

        Assert.True(result.IsError);
        Assert.Equal(0, client.Calls);
        Assert.Null(result.Images);
    }

    [Fact]
    public async Task The_generated_image_is_a_typed_part_in_the_transcript_and_a_sidecar_blob()
    {
        using var temp = new TempDirectory();
        string workspace = temp.File("workspace");
        Directory.CreateDirectory(workspace);
        Session session = Session.Create(temp.File("sessions/session.jsonl"), workspace);
        byte[] png = ImageSamples.Png(2, 3);
        var chat = new ScriptedChatClient()
            .Enqueue(
                Scripts.Call(
                    "call-1",
                    GenerateImageTool.ToolName,
                    Scripts.Args(("prompt", "a red square"))
                ),
                Scripts.ToolCalls()
            )
            .Enqueue(Scripts.Text("done"), Scripts.Stop());
        var tools = new ToolRegistry();
        tools.Add(
            new GenerateImageTool(
                new Workspace(workspace),
                new ImageGenerationBinding(
                    new StubImageClient(png),
                    "image-profile",
                    "fake-provider",
                    "openai",
                    "gpt-image-1"
                )
            )
        );
        var harness = new AgentHarness(
            chat,
            tools,
            new AgentHarnessOptions
            {
                Session = session,
                WorkingDirectory = workspace,
                Model = new ModelInfo("vision-test", "openai", null, 128_000, true)
                {
                    SupportsVision = true,
                },
            }
        );

        var events = new List<AgentEvent>();
        await foreach (
            AgentEvent captured in harness.RunAsync(
                "draw a red square",
                TestContext.Current.CancellationToken
            )
        )
        {
            events.Add(captured);
        }

        Assert.True(Assert.Single(events.OfType<ToolCallResult>()).HasImages);
        SessionMessageEntry toolEntry = Assert.Single(
            session.Entries.OfType<SessionMessageEntry>(),
            entry =>
                entry.Message.Role == ChatRole.Tool
                && entry.Message.Contents.OfType<FunctionResultContent>().Any()
        );
        Assert.Equal(
            "[image 2x3]",
            Assert.IsType<FunctionResultContent>(Assert.Single(toolEntry.Message.Contents)).Result
        );

        ImageContentReference reference = Assert.Single(
            session
                .Entries.OfType<SessionMessageEntry>()
                .SelectMany(entry => entry.Message.Contents)
                .OfType<ImageContentReference>()
        );
        string digest = Convert.ToHexStringLower(SHA256.HashData(png));
        Assert.Equal(digest, reference.Digest);
        Assert.Equal("image/png", reference.MimeType);
        Assert.Equal(2, reference.Width);
        Assert.Equal(3, reference.Height);

        string blob = Path.Combine(
            temp.File("sessions"),
            session.SessionId,
            "images",
            digest + ".png"
        );
        Assert.True(File.Exists(blob), $"expected the sidecar blob at {blob}");
        Assert.Equal(png, File.ReadAllBytes(blob));

        string json = File.ReadAllText(session.Path);
        Assert.Contains("\"$type\":\"image\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(png), json, StringComparison.Ordinal);

        UsageUpdated usage = Assert.Single(events.OfType<UsageUpdated>());
        Assert.Equal("image", usage.Attribution?.Usage);
        Assert.Equal("image-profile", usage.Attribution?.ProfileName);
    }

    private static async Task<ToolResult> RunAsync(
        TempDirectory temp,
        StubImageClient client,
        string args,
        RecordingSink? sink = null,
        Action<UsageDetails, string, ModelUsageAttribution>? record = null
    )
    {
        var workspace = new Workspace(temp.Root);
        var tool = new GenerateImageTool(
            workspace,
            new ImageGenerationBinding(
                client,
                "image-profile",
                "fake-provider",
                "openai",
                "gpt-image-1"
            )
        );
        var context = new ToolContext(temp.Root, new NullAgentEvents())
        {
            RunId = "run_test",
            FileChanges = sink is null ? NullFileChangeSink.Instance : sink,
            AgentState = new AgentToolContextState(null, null, null)
            {
                RecordUsage = record is null
                    ? null
                    : (usage, modelId, attribution) => record(usage, modelId, attribution),
            },
        };
        return await tool.ExecuteAsync(
            JsonDocument.Parse(args).RootElement.Clone(),
            context,
            TestContext.Current.CancellationToken
        );
    }

    private sealed class StubImageClient(byte[] data) : IImageGenerationClient
    {
        public string ModelId => "gpt-image-1";

        public int Calls { get; private set; }

        public string? Prompt { get; private set; }

        public Exception? Failure { get; init; }

        public Task<GeneratedImageResult> GenerateAsync(
            string prompt,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            Prompt = prompt;
            if (Failure is { } failure)
            {
                throw failure;
            }

            return Task.FromResult(
                new GeneratedImageResult(
                    data,
                    "image/png",
                    new UsageDetails { InputTokenCount = 10, OutputTokenCount = 20 }
                )
            );
        }
    }

    private sealed class RecordingSink : IFileChangeSink
    {
        public List<FileChangeEvent> Events { get; } = [];

        public void Notify(FileChangeEvent change) => Events.Add(change);
    }
}
