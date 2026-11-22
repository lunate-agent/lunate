using System.Text.Json;
using Lunate.Agent;
using Microsoft.Extensions.AI;

namespace Lunate.Coding.Tests;

/// <summary>
/// The todo list survives a resume through the tool-call messages the session already stores: the
/// last valid call on the active path is the list, and no session format changes.
/// </summary>
public sealed class TodoReplayTests
{
    [Fact]
    public void A_resumed_session_replays_the_last_valid_list()
    {
        using var temp = new TempDirectory();
        string path = temp.File("session.jsonl");
        Session session = Session.Create(path, temp.Root);
        session.AppendMessage(new ChatMessage(ChatRole.User, "plan it"));
        AppendCall(session, "call_1", """[{"text":"first","status":"pending"}]""");
        AppendCall(session, "call_2", """[{"text":"second","status":"in_progress"}]""");

        Session loaded = Session.Load(path);
        IReadOnlyList<TodoItem>? items = TodoReplay.FromSession(loaded);

        Assert.Equal([new TodoItem("second", TodoItemStatus.InProgress)], items);
    }

    [Fact]
    public void An_invalid_later_call_does_not_replace_the_valid_list()
    {
        using var temp = new TempDirectory();
        string path = temp.File("session.jsonl");
        Session session = Session.Create(path, temp.Root);
        session.AppendMessage(new ChatMessage(ChatRole.User, "plan it"));
        AppendCall(session, "call_1", """[{"text":"keep","status":"done"}]""");
        AppendCall(
            session,
            "call_2",
            """[{"text":"one","status":"in_progress"},{"text":"two","status":"in_progress"}]"""
        );

        Session loaded = Session.Load(path);
        IReadOnlyList<TodoItem>? items = TodoReplay.FromSession(loaded);

        Assert.Equal([new TodoItem("keep", TodoItemStatus.Done)], items);
    }

    [Fact]
    public void A_compaction_does_not_erase_the_replayed_list()
    {
        using var temp = new TempDirectory();
        string path = temp.File("session.jsonl");
        Session session = Session.Create(path, temp.Root);
        session.AppendMessage(new ChatMessage(ChatRole.User, "plan it"));
        AppendCall(session, "call_1", """[{"text":"keep","status":"done"}]""");
        session.AppendCompaction("summary", ["e_01", "e_02", "e_03"]);

        Session loaded = Session.Load(path);

        Assert.DoesNotContain(loaded.ToHistory(), message =>
            message.Contents.OfType<FunctionCallContent>().Any()
        );
        Assert.Equal([new TodoItem("keep", TodoItemStatus.Done)], TodoReplay.FromSession(loaded));
    }

    [Fact]
    public void A_session_without_a_call_replays_nothing()
    {
        using var temp = new TempDirectory();
        string path = temp.File("session.jsonl");
        Session session = Session.Create(path, temp.Root);
        session.AppendMessage(new ChatMessage(ChatRole.User, "no plan"));

        Session loaded = Session.Load(path);

        Assert.Null(TodoReplay.FromSession(loaded));
    }

    [Fact]
    public async Task A_loaded_session_shows_its_list_in_the_live_area()
    {
        using var temp = new TempDirectory();
        string path = temp.File("session.jsonl");
        Session session = Session.Create(path, temp.Root);
        session.AppendMessage(new ChatMessage(ChatRole.User, "plan it"));
        AppendCall(
            session,
            "call_1",
            """[{"text":"read the card","status":"done"},{"text":"write the test","status":"in_progress"}]"""
        );

        using var host = new InteractiveSessionHost(
            workingDirectory: temp.Root,
            startSession: path,
            sessionDirectory: temp.File("sessions")
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);
        await InteractiveSessionCommandTests.WaitForFrameAsync(host, "[>] write the test");

        Assert.Contains("[x] read the card", host.LastFrame, StringComparison.Ordinal);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    [Fact]
    public async Task A_child_run_does_not_replace_the_parent_list()
    {
        using var host = new InteractiveSessionHost();
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Session.HandleEvent(
            new TodoUpdated("run_parent", [new TodoItem("parent plan", TodoItemStatus.Pending)])
        );
        host.Advance(33);
        host.Session.HandleEvent(
            new TodoUpdated("run_child", [new TodoItem("child plan", TodoItemStatus.Pending)])
            {
                ParentRunId = "run_parent",
            }
        );
        host.Advance(33);

        Assert.Contains("[ ] parent plan", host.LastFrame, StringComparison.Ordinal);
        Assert.DoesNotContain("[ ] child plan", host.LastFrame, StringComparison.Ordinal);

        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    private static void AppendCall(Session session, string callId, string itemsJson)
    {
        JsonElement arguments = JsonDocument
            .Parse($$"""{"items":{{itemsJson}}}""")
            .RootElement.Clone();
        var dictionary = arguments
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value);
        session.AppendMessage(
            new ChatMessage(
                ChatRole.Assistant,
                [new FunctionCallContent(callId, "todo", dictionary)]
            )
        );
        session.AppendMessage(
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent(callId, "todo list updated")])
        );
    }
}
