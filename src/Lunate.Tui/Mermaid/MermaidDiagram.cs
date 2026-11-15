using System.Globalization;
using Mermaid.Terminal;

namespace Lunate.Tui;

/// <summary>
/// The Mermaid fence adapter: the drawing, parsing and fallback reasons belong to
/// <c>Mermaid.Terminal</c> (ADR-0039). This keeps only what the TUI owns — the fence's label line,
/// the dim frame versus plain labels, and trimming the drawing's padded line ends.
/// </summary>
internal static class MermaidDiagram
{
    public static IReadOnlyList<StyledLine> RenderFence(
        IReadOnlyList<string> source,
        TerminalCapabilities capabilities,
        int terminalWidth
    )
    {
        MermaidRenderResult result = MermaidRenderer.Render(
            string.Join('\n', source),
            terminalWidth,
            capabilities.Unicode ? MermaidGlyphSet.Unicode : MermaidGlyphSet.Ascii
        );
        if (result is MermaidFallbackResult fallback)
        {
            return Fallback(fallback.Source, fallback.Reason);
        }

        var lines = new List<StyledLine> { Label() };
        foreach (MermaidLine line in ((MermaidDiagramResult)result).Lines)
        {
            lines.Add(Styled(line));
        }

        return lines;
    }

    private static StyledLine Label()
    {
        var label = new StyledLine();
        label.Add("mermaid", SpanStyle.Dim);
        return label;
    }

    /// <summary>The drawing's runs, trimmed at the line end the way a host prints them.</summary>
    private static StyledLine Styled(MermaidLine line)
    {
        var runs = line
            .Spans.Select(static span =>
                (
                    Text: span.Text,
                    Style: span.Kind == MermaidSpanKind.Label ? SpanStyle.Plain : SpanStyle.Dim
                )
            )
            .ToList();
        while (runs.Count > 0 && runs[^1].Text.TrimEnd(' ').Length == 0)
        {
            runs.RemoveAt(runs.Count - 1);
        }

        if (runs.Count > 0)
        {
            runs[^1] = (runs[^1].Text.TrimEnd(' '), runs[^1].Style);
        }

        var styled = new StyledLine();
        foreach ((string text, SpanStyle style) in runs)
        {
            styled.Add(text, style);
        }

        return styled;
    }

    private static List<StyledLine> Fallback(IReadOnlyList<string> source, string reason)
    {
        var lines = new List<StyledLine> { Label() };
        foreach (string text in source)
        {
            var line = new StyledLine();
            line.Add(text, SpanStyle.Plain);
            lines.Add(line);
        }

        var note = new StyledLine();
        note.Add(
            string.Create(CultureInfo.InvariantCulture, $"mermaid: {reason} — showing the source"),
            SpanStyle.Plain
        );
        lines.Add(note);
        return lines;
    }
}
