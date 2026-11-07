namespace Lunate.Tui.Tests;

public sealed class FrameWriterTests
{
    [Fact]
    public void First_paint_has_no_cursor_up_and_clears_each_line()
    {
        using var console = new FakeConsoleIO();
        var writer = new FrameWriter(console);

        writer.Paint(new RenderedFrame(["> a"], 0, 3));

        Assert.Equal("\r\u001b[2K> a\n\u001b[1A\r\u001b[3C", console.Writes[0]);
    }

    [Fact]
    public void Repaints_start_at_the_previous_frame_top()
    {
        // The cursor rests on the frame's cursor row (the input line), not below the frame:
        // the next paint must move up by exactly that row. Moving up by the frame height
        // would overshoot, drift the area upward and leave unerased ghost rows behind.
        using var console = new FakeConsoleIO();
        var writer = new FrameWriter(console);

        writer.Paint(new RenderedFrame(["status", "> a"], 1, 3));
        writer.Paint(new RenderedFrame(["status", "> ab"], 1, 4));

        Assert.EndsWith("\u001b[1A\r\u001b[3C", console.Writes[0]);

        string second = console.Writes[1];
        Assert.StartsWith("\u001b[1A", second);
        Assert.Contains("\r\u001b[2Kstatus\n", second);
        Assert.Contains("\r\u001b[2K> ab\n", second);
        Assert.EndsWith("\u001b[1A\r\u001b[4C", second);
    }

    [Fact]
    public void Cursor_is_placed_on_the_requested_row_and_column()
    {
        using var console = new FakeConsoleIO();
        var writer = new FrameWriter(console);

        writer.Paint(new RenderedFrame(["a", "b", "c"], 1, 4));

        Assert.EndsWith("\u001b[2A\r\u001b[4C", console.Writes[0]);
    }

    [Fact]
    public void Shrinking_frame_erases_the_rows_it_no_longer_uses()
    {
        using var console = new FakeConsoleIO();
        var writer = new FrameWriter(console);

        writer.Paint(new RenderedFrame(["a", "b", "> c"], 2, 3));
        writer.Paint(new RenderedFrame(["> d"], 0, 3));

        string second = console.Writes[1];
        Assert.StartsWith("\u001b[2A", second);
        Assert.Contains("\r\u001b[2K> d\n", second);
        Assert.Equal(3, CountOccurrences(second, "\r\u001b[2K"));
    }

    [Fact]
    public void Clear_erases_the_area_and_resets_the_height()
    {
        using var console = new FakeConsoleIO();
        var writer = new FrameWriter(console);

        writer.Paint(new RenderedFrame(["status", "> a"], 1, 3));
        writer.Clear();

        Assert.Equal("\u001b[1A\r\u001b[2K\n\r\u001b[2K\u001b[1A", console.Writes[1]);

        writer.Clear();

        Assert.Equal(2, console.Writes.Count);
    }

    [Fact]
    public void Wrapped_lines_count_their_physical_rows_when_clearing()
    {
        using var console = new FakeConsoleIO();
        console.PushResize(new ConsoleSize(10, 24));
        var writer = new FrameWriter(console);

        writer.Paint(new RenderedFrame(["0123456789ABCDEF", "status"], 1, 3));
        writer.Paint(new RenderedFrame(["short", "status"], 1, 3));

        string second = console.Writes[1];
        // Two physical rows for the first line plus one for status: the repaint starts two rows
        // above the cursor and clears three physical rows, not two logical ones.
        Assert.StartsWith("\u001b[2A", second);
        Assert.Equal(3, CountOccurrences(second, "\r\u001b[2K"));
    }

    [Fact]
    public void Ansi_sequences_do_not_add_phantom_wrapped_rows()
    {
        using var console = new FakeConsoleIO();
        console.PushResize(new ConsoleSize(10, 24));
        var writer = new FrameWriter(console);

        writer.Paint(new RenderedFrame(["[31mred[0m"], 0, 0));
        writer.Paint(new RenderedFrame(["[31mred[0m"], 0, 0));

        Assert.Equal(1, CountOccurrences(console.Writes[0], "\r\u001b[2K"));
    }

    [Fact]
    public void Wide_characters_wrap_by_cell_width_not_code_point_count()
    {
        using var console = new FakeConsoleIO();
        console.PushResize(new ConsoleSize(40, 24));
        var writer = new FrameWriter(console);

        // 25 CJK characters (2 cells each) plus one emoji (2 cells) are 52 cells, so the line
        // occupies two physical rows at 40 columns even though it is 26 code points.
        string wide = new string('\u6f22', 25) + "\U0001F600";
        writer.Paint(new RenderedFrame([wide, "status"], 1, 3));
        writer.Paint(new RenderedFrame(["short", "status"], 1, 3));

        string first = console.Writes[0];
        // One clear per physical row: two for the wrapped line plus one for the status line.
        Assert.Equal(3, CountOccurrences(first, "\r\u001b[2K"));
        // The wrapped line is followed by one newline per physical row it occupies.
        Assert.Contains(wide + "\n\n", first, StringComparison.Ordinal);

        // The repaint starts two rows above the cursor and clears the full three-row height, so
        // the wrapped overflow cannot survive into the next frame.
        string second = console.Writes[1];
        Assert.StartsWith("\u001b[2A", second);
        Assert.Equal(3, CountOccurrences(second, "\r\u001b[2K"));
    }

    [Fact]
    public void Sgr_wrapped_cjk_and_emoji_measure_only_their_visible_cells()
    {
        using var console = new FakeConsoleIO();
        console.PushResize(new ConsoleSize(10, 24));
        var writer = new FrameWriter(console);

        // Six CJK characters (12 cells) plus one emoji (2 cells) are fourteen cells: two physical
        // rows at ten columns. The SGR bytes must not count: with them the line would read as
        // thirty-four cells and wrap onto four rows.
        string line =
            "\u001b[38;2;138;138;138m" + new string('\u6f22', 6) + "\U0001F600" + "\u001b[0m";
        writer.Paint(new RenderedFrame([line], 0, 0));

        Assert.Equal(2, CountOccurrences(console.Writes[0], "\r\u001b[2K"));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (
            var i = haystack.IndexOf(needle, StringComparison.Ordinal);
            i >= 0;
            i = haystack.IndexOf(needle, i + 1, StringComparison.Ordinal)
        )
        {
            count++;
        }

        return count;
    }
}
