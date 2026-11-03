using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

/// <summary>
/// The Gemini thought-signature shape end to end (slice 5 of the provider-families change):
/// a rendered thinking turn arrives with its signature in a signature-only reasoning content
/// beside the function call, and the signature survives a tool turn, a compaction and a session
/// reload — the same rules the signed-thinking tests pin for Anthropic, here for the content
/// shapes the Google family streams (empty text, <c>ProtectedData</c> only).
/// </summary>
public sealed class GoogleThinkingSignatureTests
{
    private const string Signature = "gem-thought-signature";
    private const string ModelId = "gemini-2.5-flash";

    /// <summary>
    /// A reloaded signature-bearing tool turn keeps its signature and its call, in order, on the
    /// next request: the session file round-trips both, so the SDK re-attach can happen the next
    /// turn.
    /// </summary>
    [Fact]
    public async Task A_reloaded_tool_turn_keeps_its_signature_and_its_call()
    {
        using var temp = new TempDirectory();
        Session created = Session.Create(temp.File("s.jsonl"), "/work");
        created.AppendMessage(SignedToolTurn(), model: ModelId);
        Session session = Session.Load(created.Path);
        ScriptedChatClient client = new ScriptedChatClient().Enqueue(
            LoopScripts.Text("will do"),
            LoopScripts.Stop()
        );
        var harness = new AgentHarness(
            client,
            new ToolRegistry(),
            new AgentHarnessOptions { Session = session, ModelId = ModelId }
        );

        await harness
            .RunAsync("go", TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        ChatMessage assistant = RequestAssistantMessage(client);
        TextReasoningContent signature = Assert.Single(
            assistant.Contents.OfType<TextReasoningContent>()
        );
        Assert.Equal(Signature, signature.ProtectedData);
        FunctionCallContent call = Assert.Single(assistant.Contents.OfType<FunctionCallContent>());
        Assert.Equal("call_1", call.CallId);
        Assert.Contains(Signature, File.ReadAllText(created.Path), StringComparison.Ordinal);
    }

    /// <summary>
    /// The summary replaces whole turns; a surviving assistant turn keeps the bare signature of
    /// the surviving turn and its function call, in order, in the rebuilt history.
    /// </summary>
    [Fact]
    public async Task A_surviving_turn_after_compaction_keeps_its_signature_and_its_call()
    {
        using var temp = new TempDirectory();
        Scenario scenario = Build(temp);

        await AgentTestSupport.Run(scenario.Harness);

        ChatMessage surviving = Assert.Single(
            scenario.Client.Requests[1].Messages,
            message => message.Contents.OfType<FunctionCallContent>().Any()
        );
        TextReasoningContent signature = Assert.Single(
            surviving.Contents.OfType<TextReasoningContent>()
        );
        Assert.Equal(Signature, signature.ProtectedData);
        Assert.Equal(
            "call_1",
            Assert.Single(surviving.Contents.OfType<FunctionCallContent>()).CallId
        );
    }

    /// <summary>
    /// A tool turn after a compaction and a reload sends the signature again: the compaction's
    /// rebuilt history is stored as append-only entries, the loaded session keeps them, and the
    /// next request carries the surviving turn's signature.
    /// </summary>
    [Fact]
    public async Task A_tool_turn_after_compaction_and_a_reload_sends_the_signature()
    {
        using var temp = new TempDirectory();
        Scenario scenario = Build(temp);

        await AgentTestSupport.Run(scenario.Harness);

        Session reloaded = Session.Load(scenario.Session.Path);
        ScriptedChatClient after = new ScriptedChatClient().Enqueue(LoopScripts.Stop());
        AgentHarness harness = new(
            after,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Session = reloaded,
                SystemPrompt = "base rules",
                ModelId = ModelId,
                ModelCatalog = TestCatalog.WithWindow(1000, ModelId),
            }
        );

        await AgentTestSupport.Run(harness);

        ChatMessage assistant = Assert.Single(
            after.Requests[^1].Messages,
            message => message.Contents.OfType<FunctionCallContent>().Any()
        );
        TextReasoningContent signature = Assert.Single(
            assistant.Contents.OfType<TextReasoningContent>()
        );
        Assert.Equal(Signature, signature.ProtectedData);
        FunctionCallContent call = Assert.Single(assistant.Contents.OfType<FunctionCallContent>());
        Assert.Equal("call_1", call.CallId);
    }

    private static ChatMessage SignedToolTurn() =>
        new(
            ChatRole.Assistant,
            [
                new FunctionCallContent(
                    "call_1",
                    "read",
                    new Dictionary<string, object?> { ["path"] = "notes.md" }
                ),
                new TextReasoningContent(null) { ProtectedData = Signature },
            ]
        );

    /// <summary>
    /// Four recorded turns plus the run's own turn with the default keep of four: the first turns
    /// fall inside the replaced part, the signature-bearing tool turn survives in the tail.
    /// </summary>
    private static Scenario Build(TempDirectory temp)
    {
        Session session = Session.Create(temp.File("s.jsonl"), "/work");
        Append(session, new ChatMessage(ChatRole.User, "one " + new string('x', 4000)));
        Append(
            session,
            new ChatMessage(
                ChatRole.Assistant,
                [
                    new TextReasoningContent("reasoning of the replaced turn")
                    {
                        ProtectedData = "sig-replaced",
                    },
                    new TextContent("noted one"),
                ]
            )
        );
        Append(session, new ChatMessage(ChatRole.User, "two"));
        Append(session, new ChatMessage(ChatRole.Assistant, "noted two"));
        Append(session, new ChatMessage(ChatRole.User, "three"));
        Append(session, SignedToolTurn());
        Append(session, new ChatMessage(ChatRole.User, "four"));
        Append(session, new ChatMessage(ChatRole.Assistant, "noted four"));
        var client = new ScriptedChatClient()
            .Enqueue(LoopScripts.Text("goal: continue the work."))
            .Enqueue(LoopScripts.Text("done"), LoopScripts.Stop());
        AgentHarness harness = new(
            client,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Session = session,
                SystemPrompt = "base rules",
                ModelId = ModelId,
                ModelCatalog = TestCatalog.WithWindow(1000, ModelId),
            }
        );
        return new Scenario(session, client, harness);
    }

    private static void Append(Session session, ChatMessage message) =>
        session.AppendMessage(message, model: ModelId);

    private static ChatMessage RequestAssistantMessage(ScriptedChatClient client) =>
        Assert.Single(
            Assert.Single(client.Requests).Messages,
            message => message.Role == ChatRole.Assistant
        );

    private sealed record Scenario(
        Session Session,
        ScriptedChatClient Client,
        AgentHarness Harness
    );
}
