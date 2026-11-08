namespace Lunate.Tui.Tests;

public sealed class TerminalMessageTests
{
    private const string Guard =
        "LUNATE_HOME points inside the workspace; trust records there could be written by the repository";

    [Fact]
    public void The_guard_sentence_wraps_to_the_terminal_width_without_losing_text()
    {
        IReadOnlyList<string> lines = TerminalMessage.Lines(Guard, 40);

        Assert.True(lines.Count > 1, "the guard sentence was not wrapped");
        Assert.All(
            lines,
            line => Assert.True(CellText.Width(line) <= 40, $"'{line}' exceeds 40 cells")
        );
        Assert.Equal(Guard, string.Join(" ", lines.Select(static line => line.Trim())));
    }

    [Fact]
    public void A_long_message_is_not_capped_at_the_notice_slot_and_carries_no_ellipsis()
    {
        const string Message =
            "the extension host could not start because one extension named a tool this session already holds; remove the duplicate and restart the session";

        IReadOnlyList<string> lines = TerminalMessage.Lines(Message, 20);

        Assert.True(lines.Count > 4, "the message was capped at the notice slot's four lines");
        Assert.DoesNotContain(
            lines,
            static line => line.Contains('…') || line.Contains("...", StringComparison.Ordinal)
        );
        Assert.Equal(Message, string.Concat(lines));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    [InlineData(int.MaxValue)]
    public void A_width_with_no_wrap_returns_the_sanitized_text_on_one_line(int columns)
    {
        const string Text = "a\u0007b\tc";

        IReadOnlyList<string> lines = TerminalMessage.Lines(Text, columns);

        Assert.Equal([CellText.Sanitize(Text)], lines);
    }

    [Fact]
    public void Write_emits_each_physical_line_on_its_own_line()
    {
        using var writer = new StringWriter();

        TerminalMessage.Write(writer, Guard, 40);

        using var reader = new StringReader(writer.ToString());
        var written = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            written.Add(line);
        }

        Assert.Equal(TerminalMessage.Lines(Guard, 40), written);
    }
}
