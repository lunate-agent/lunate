namespace Lunate.Tui.Tests;

/// <summary>The todo block in the live area: one status glyph per line, five lines at most, and no
/// change to a session that never called the tool.</summary>
public sealed class LiveAreaTodoTests
{
    [Fact]
    public void Items_render_in_order_with_a_glyph_per_status()
    {
        var state = new LiveAreaState
        {
            TodoItems =
            [
                new TodoItemView("read the card", TodoItemState.Done),
                new TodoItemView("write the test", TodoItemState.InProgress),
                new TodoItemView("commit", TodoItemState.Pending),
            ],
            Size = new ConsoleSize(60, 24),
        };

        var frame = LiveAreaRenderer.Render(state);

        Assert.Equal(
            ["[x] read the card", "[>] write the test", "[ ] commit"],
            frame.Lines.Take(3)
        );
    }

    [Fact]
    public void The_block_is_capped_and_names_the_unshown_count()
    {
        var state = new LiveAreaState
        {
            TodoItems =
            [
                .. Enumerable
                    .Range(1, 20)
                    .Select(index => new TodoItemView($"task {index}", TodoItemState.Pending)),
            ],
            Size = new ConsoleSize(80, 24),
        };

        var frame = LiveAreaRenderer.Render(state);

        Assert.Equal(
            LiveAreaRenderer.MaxTodoLines - 1,
            frame.Lines.Count(line => line.StartsWith('['))
        );
        Assert.Equal($"[ ] task {LiveAreaRenderer.MaxTodoLines - 1}", frame.Lines[3]);
        Assert.Equal($"    \u2026 16 more", frame.Lines[4]);
        Assert.Contains("> Ask anything, / for commands", frame.Lines);
    }

    [Fact]
    public void A_long_item_is_clipped_to_the_width()
    {
        var state = new LiveAreaState
        {
            TodoItems = [new TodoItemView(new string('x', 200), TodoItemState.Pending)],
            Size = new ConsoleSize(20, 24),
        };

        var frame = LiveAreaRenderer.Render(state);

        Assert.All(frame.Lines, line => Assert.True(CellText.Width(line) <= 20));
    }

    [Fact]
    public void An_empty_list_renders_the_same_frame_as_before_the_tool_existed()
    {
        var state = new LiveAreaState { Size = new ConsoleSize(20, 8) };

        var frame = LiveAreaRenderer.Render(state);

        Assert.Equal(
            [new string('─', 20), "> Ask anything, / fo", new string('─', 20)],
            frame.Lines
        );
    }
}
