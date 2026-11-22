using Lunate.TestSupport;
using Microsoft.Extensions.AI;

namespace Lunate.Ai.Tests;

/// <summary>
/// The Responses family's committed fixture: a three-exchange transcript — streamed text, a tool
/// call, the turn after the tool result — recorded over the Responses transport (the scripted
/// SSE wire of <see cref="OpenAiResponsesWireTests"/>), and replayed offline with no network and
/// no keys.
/// </summary>
[Collection(ChatClientFactoryEnvCollection.Name)]
public sealed class OpenAiResponsesFixtureTests
{
    private const string FileName = "openai-responses-tool-loop.jsonl";
    private const string OpenAiApiKeyVariable = "OPENAI_API_KEY";

    private static string FixturePath =>
        Path.Combine(RepositoryRoot.Find(), "tests", "fixtures", "streams", FileName);

    /// <summary>The committed transcript is the one the Responses wire produces today.</summary>
    [Fact]
    public async Task The_committed_responses_fixture_matches_a_fresh_wire_recording()
    {
        using var temp = new TempDirectory();
        string fresh = temp.File(FileName);
        using var environment = new EnvironmentScope((OpenAiApiKeyVariable, "fixture-only-key"));
        IChatClient recorder = new RecordingChatClient(
            WireClient(),
            fresh,
            OpenAiResponsesWireTests.ResponsesModel.Id
        );
        await OpenAiResponsesFixtureTests.RunToolLoop(recorder);

        string[] committedLines = File.ReadAllLines(FixturePath);
        string[] recordedLines = File.ReadAllLines(fresh);
        FixtureHeader committed = FixtureFormat.ReadFile(FixturePath).Header;
        FixtureHeader recorded = FixtureFormat.ReadFile(fresh).Header;
        Assert.Equal(committed.Schema, recorded.Schema);
        Assert.Equal(committed.Model, recorded.Model);
        Assert.Equal(committedLines[1..], recordedLines[1..]);
    }

    /// <summary>
    /// The committed transcript replays through the factory: streamed text, the tool call and the
    /// post-result turn come from the file, never from the provider.
    /// </summary>
    [Fact]
    public async Task The_committed_responses_fixture_replays_offline_through_the_factory()
    {
        var throwing = new ThrowingChatClient();
        ChatClientFactory factory = new(
            new MarkingLoggerFactory(static () => { }),
            enableOpenTelemetry: false,
            providerClientFactory: (_, _) => throwing,
            recorderDecorator: _ => new ReplayChatClient(FixturePath)
        );
        FixtureDocument document = FixtureFormat.ReadFile(FixturePath);
        IChatClient client = factory.Create(
            OpenAiResponsesWireTests.ResponsesModel with
            {
                Id = document.Header.Model,
            }
        );

        ToolLoopTurns turns = await RunToolLoop(client);

        Assert.False(throwing.Called);
        Assert.Equal(
            "pong",
            Assert.Single(turns.TextUpdates.SelectMany(u => u.Contents).OfType<TextContent>()).Text
        );
        Assert.Equal(
            "echo",
            Assert
                .Single(turns.CallUpdates.SelectMany(u => u.Contents).OfType<FunctionCallContent>())
                .Name
        );
        Assert.Equal(
            "pong",
            Assert.Single(turns.FinalUpdates.SelectMany(u => u.Contents).OfType<TextContent>()).Text
        );
    }

    /// <summary>
    /// The tool loop the fixture records: a plain text turn, a turn that calls <c>echo</c>, and
    /// the turn that sends the result back.
    /// </summary>
    internal static async Task<ToolLoopTurns> RunToolLoop(IChatClient client)
    {
        ChatMessage textPrompt = new(ChatRole.User, "Reply with the single word: pong");
        List<ChatResponseUpdate> textUpdates = await StreamAsync(
            client,
            [textPrompt],
            options: null
        );

        ChatMessage toolPrompt = new(ChatRole.User, "Use the echo tool to echo the word ping.");
        ChatOptions toolOptions = OpenAiResponsesWireTests.ToolLoopOptions();
        List<ChatResponseUpdate> callUpdates = await StreamAsync(client, [toolPrompt], toolOptions);

        ChatMessage toolResult = new(ChatRole.Tool, [new FunctionResultContent("call_1", "pong")]);
        List<ChatResponseUpdate> finalUpdates = await StreamAsync(
            client,
            [toolPrompt, callUpdates.ToChatResponse().Messages[0], toolResult],
            toolOptions
        );

        return new ToolLoopTurns(textUpdates, callUpdates, finalUpdates);
    }

    /// <summary>
    /// The Responses transport inside the full factory pipeline, driven by the scripted SSE wire.
    /// </summary>
    internal static IChatClient WireClient()
    {
        ChatClientFactory factory = new(
            new MarkingLoggerFactory(static () => { }),
            enableOpenTelemetry: false
        )
        {
            BaseTransportFactory = () =>
                new ScriptedResponsesHandler(
                    OpenAiResponsesWirePayloads.TextTurn,
                    OpenAiResponsesWirePayloads.ToolCallTurn,
                    OpenAiResponsesWirePayloads.TextTurn
                ),
        };
        return factory.Create(OpenAiResponsesWireTests.ResponsesModel);
    }

    private static async Task<List<ChatResponseUpdate>> StreamAsync(
        IChatClient client,
        ChatMessage[] messages,
        ChatOptions? options
    )
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (
            ChatResponseUpdate update in client.GetStreamingResponseAsync(
                messages,
                options,
                TestContext.Current.CancellationToken
            )
        )
        {
            updates.Add(update);
        }

        return updates;
    }
}

/// <summary>The three recorded turns of the tool loop.</summary>
internal sealed record ToolLoopTurns(
    List<ChatResponseUpdate> TextUpdates,
    List<ChatResponseUpdate> CallUpdates,
    List<ChatResponseUpdate> FinalUpdates
);
