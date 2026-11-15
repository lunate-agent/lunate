using Spectre.Console;

namespace Lunate.Tui.Tests;

/// <summary>The TUI keeps only the fence presentation; the drawing comes from Mermaid.Terminal.</summary>
public sealed class MermaidAdapterTests
{
    private static readonly string[] Flowchart = ["flowchart TD", "A[One] --> B[Two]"];

    [Fact]
    public void The_frame_is_dim_and_the_label_is_plain()
    {
        IReadOnlyList<StyledLine> lines = MermaidDiagram.RenderFence(
            Flowchart,
            new TerminalCapabilities(ColorSystemSupport.NoColors, Unicode: true),
            terminalWidth: 80
        );

        Assert.Equal("mermaid", lines[0].Spans.Single().Text);
        Assert.Contains(
            lines.Skip(1).SelectMany(static line => line.Spans),
            static span => span.Style == SpanStyle.Dim && span.Text.Contains('┌')
        );
        Assert.Contains(
            lines.Skip(1).SelectMany(static line => line.Spans),
            static span => span.Style == SpanStyle.Plain && span.Text.Contains("One")
        );
    }

    [Fact]
    public void A_terminal_without_unicode_gets_the_ascii_glyphs()
    {
        IReadOnlyList<StyledLine> lines = MermaidDiagram.RenderFence(
            Flowchart,
            new TerminalCapabilities(ColorSystemSupport.NoColors, Unicode: false),
            terminalWidth: 80
        );

        string drawing = string.Concat(
            lines.SelectMany(static line => line.Spans).Select(static s => s.Text)
        );
        Assert.Contains('+', drawing);
        Assert.Contains('|', drawing);
        Assert.DoesNotContain('┌', drawing);
    }
}
