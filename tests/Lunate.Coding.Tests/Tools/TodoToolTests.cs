using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The core <c>todo</c> tool: one full-list argument, at most one in-progress item, a short cap and
/// one event per successful call.
/// </summary>
public sealed class TodoToolTests
{
    private static readonly JsonElement Items = JsonDocument
        .Parse(
            """
            [
              {"text":"read the card","status":"done"},
              {"text":"write the test","status":"in_progress"},
              {"text":"commit","status":"pending"}
            ]
            """
        )
        .RootElement.Clone();

    [Fact]
    public void Tool_shape_is_pinned()
    {
        var tool = new TodoTool();

        Assert.Equal("todo", tool.Name);
        Assert.Equal(ToolRisk.ReadOnly, tool.Risk);
        Assert.True(tool.Annotations!.ReadOnly);
        JsonElement schema = tool.ParametersSchema;
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), element =>
            element.GetString() == "items"
        );
        JsonElement status = schema
            .GetProperty("properties")
            .GetProperty("items")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("status");
        Assert.Equal(
            ["pending", "in_progress", "done"],
            status.GetProperty("enum").EnumerateArray().Select(element => element.GetString())
        );
    }

    [Fact]
    public async Task A_call_creates_the_list_and_emits_the_full_list()
    {
        var events = new RecordingEvents();

        ToolResult result = await ExecuteAsync(events, $$"""{"items":{{Items.GetRawText()}}}""");

        Assert.False(result.IsError);
        Assert.Equal(
            "todo list updated: 3 items (1 pending, 1 in progress, 1 done)",
            result.Output
        );
        TodoUpdated update = Assert.Single(events.Events.OfType<TodoUpdated>());
        Assert.Equal("run_1", update.RunId);
        Assert.Equal(
            [
                new TodoItem("read the card", TodoItemStatus.Done),
                new TodoItem("write the test", TodoItemStatus.InProgress),
                new TodoItem("commit", TodoItemStatus.Pending),
            ],
            update.Items
        );
    }

    [Fact]
    public async Task A_second_call_replaces_the_whole_list()
    {
        var events = new RecordingEvents();

        await ExecuteAsync(events, $$"""{"items":{{Items.GetRawText()}}}""");
        ToolResult result = await ExecuteAsync(
            events,
            """{"items":[{"text":"only one","status":"pending"}]}"""
        );

        Assert.False(result.IsError);
        Assert.Equal(2, events.Events.Count);
        TodoUpdated update = events.Events.OfType<TodoUpdated>().Last();
        Assert.Equal([new TodoItem("only one", TodoItemStatus.Pending)], update.Items);
    }

    [Fact]
    public async Task An_empty_list_clears_the_plan()
    {
        var events = new RecordingEvents();

        ToolResult result = await ExecuteAsync(events, """{"items":[]}""");

        Assert.False(result.IsError);
        Assert.Equal("todo list cleared", result.Output);
        Assert.Empty(Assert.Single(events.Events.OfType<TodoUpdated>()).Items);
    }

    [Fact]
    public async Task More_than_one_in_progress_is_refused_without_an_event()
    {
        var events = new RecordingEvents();

        ToolResult result = await ExecuteAsync(
            events,
            """{"items":[{"text":"one","status":"in_progress"},{"text":"two","status":"in_progress"}]}"""
        );

        Assert.True(result.IsError);
        Assert.Contains(
            "mark at most one item in progress",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Empty(events.Events);
    }

    [Theory]
    [InlineData("""{"items":[{"text":"   ","status":"pending"}]}""", "items[0].text")]
    [InlineData(
        """{"items":[{"text":"one","status":"waiting"}]}""",
        "items[0].status must be 'pending', 'in_progress' or 'done'"
    )]
    [InlineData("""{"items":[{"text":"one"}]}""", "items[0].status must be")]
    [InlineData(
        """{"items":[{"text":"one","status":"pending","owner":"me"}]}""",
        "unknown argument 'owner'"
    )]
    [InlineData("""{"items":{}}""", "items is required and must be an array")]
    [InlineData("""{}""", "items is required and must be an array")]
    [InlineData("""{"item":[]}""", "unknown argument 'item'")]
    public async Task Invalid_items_are_refused_with_guidance(string arguments, string expected)
    {
        var events = new RecordingEvents();

        ToolResult result = await ExecuteAsync(events, arguments);

        Assert.True(result.IsError);
        Assert.Contains(expected, result.Output, StringComparison.Ordinal);
        Assert.Empty(events.Events);
    }

    [Fact]
    public async Task The_list_cap_is_twenty_items()
    {
        var events = new RecordingEvents();
        string items = string.Join(
            ',',
            Enumerable
                .Range(0, TodoTool.MaxItems + 1)
                .Select(index => $$"""{"text":"task {{index}}","status":"pending"}""")
        );

        ToolResult result = await ExecuteAsync(events, $$"""{"items":[{{items}}]}""");

        Assert.True(result.IsError);
        Assert.Contains("at most 20 items", result.Output, StringComparison.Ordinal);
        Assert.Empty(events.Events);
    }

    [Fact]
    public async Task Outside_a_run_the_call_succeeds_without_an_event()
    {
        var events = new RecordingEvents();
        var context = new ToolContext("unused", events);

        ToolResult result = await new TodoTool().ExecuteAsync(
            JsonDocument.Parse("""{"items":[]}""").RootElement,
            context,
            CancellationToken.None
        );

        Assert.False(result.IsError);
        Assert.Empty(events.Events);
    }

    private static Task<ToolResult> ExecuteAsync(RecordingEvents events, string arguments) =>
        new TodoTool().ExecuteAsync(
            JsonDocument.Parse(arguments).RootElement,
            new ToolContext("unused", events) { RunId = "run_1" },
            CancellationToken.None
        );

    private sealed class RecordingEvents : IAgentEvents
    {
        public List<AgentEvent> Events { get; } = [];

        public void Emit(AgentEvent agentEvent) => Events.Add(agentEvent);
    }
}
