using System.Globalization;

namespace Mermaid.Terminal;

/// <summary>
/// The renderer entry point: classify the first meaningful line, parse the diagram it names, lay
/// it out and draw it. Anything outside the bounded grammar, and any drawing that does not fit the
/// supplied width, comes back as <see cref="MermaidFallbackResult"/> — the source and one reason,
/// never a partial picture. The call never throws for a source it cannot draw.
/// </summary>
public static class MermaidRenderer
{
    /// <summary>The narrowest terminal that draws a diagram; below this the source is shown.</summary>
    public const int MinimumWidth = 40;

    /// <summary>Render a Mermaid source with the Unicode glyph set.</summary>
    public static MermaidRenderResult Render(string source, int width) =>
        Render(source, width, MermaidGlyphSet.Unicode);

    /// <summary>Render a Mermaid source for a width and a glyph set.</summary>
    public static MermaidRenderResult Render(string source, int width, MermaidGlyphSet glyphs)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(width);

        string[] lines = SplitLines(source);
        (int Number, string Text)? first = FirstStatement(lines);
        if (first is null)
        {
            return Fallback(lines, "the fence has no diagram");
        }

        string verb = Verb(first.Value.Text);
        MermaidDrawResult result = verb switch
        {
            "flowchart" or "graph" => FlowchartParser.Draw(lines, verb, glyphs),
            "sequenceDiagram" => SequenceParser.Draw(lines, glyphs),
            _ => MermaidDrawResult.Unsupported(verb),
        };
        if (result.UnsupportedType is { } unsupported)
        {
            return Fallback(lines, $"unsupported diagram type '{unsupported}'");
        }

        if (result.Failure is { } failure)
        {
            return Fallback(lines, failure);
        }

        if (!Fits(result.Width, width))
        {
            return Fallback(lines, WidthReason(result.Width, width));
        }

        return new MermaidDiagramResult(result.Lines!, result.Width);
    }

    private static bool Fits(int drawingWidth, int terminalWidth) =>
        terminalWidth == int.MaxValue
        || (terminalWidth >= MinimumWidth && drawingWidth <= terminalWidth);

    private static string WidthReason(int drawingWidth, int terminalWidth) =>
        terminalWidth < MinimumWidth
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"this terminal has {terminalWidth} columns, {MinimumWidth} is the minimum"
            )
            : string.Create(
                CultureInfo.InvariantCulture,
                $"diagram is {drawingWidth} columns wide, this terminal has {terminalWidth}"
            );

    private static MermaidRenderResult Fallback(IReadOnlyList<string> source, string reason) =>
        new MermaidFallbackResult(source, reason);

    private static string[] SplitLines(string source)
    {
        string normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        normalized = normalized.Replace('\r', '\n');
        if (normalized.Length == 0)
        {
            return [];
        }

        string[] lines = normalized.Split('\n');
        if (lines.Length > 1 && lines[^1].Length == 0)
        {
            return lines[..^1];
        }

        return lines;
    }

    private static (int Number, string Text)? FirstStatement(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            string text = lines[index].Trim();
            if (text.Length > 0)
            {
                return (index + 1, text);
            }
        }

        return null;
    }

    private static string Verb(string firstLine)
    {
        int end = firstLine.IndexOf(' ', StringComparison.Ordinal);
        return end < 0 ? firstLine : firstLine[..end];
    }
}

/// <summary>What the parser/layout half of one diagram hands back: lines, a reason or a type.</summary>
internal sealed record MermaidDrawResult(
    IReadOnlyList<MermaidLine>? Lines,
    int Width,
    string? Failure,
    string? UnsupportedType
)
{
    internal static MermaidDrawResult Ok(Drawing drawing) => Ok(drawing.ToLines());

    internal static MermaidDrawResult Ok(IReadOnlyList<MermaidLine> lines) =>
        new(
            lines,
            lines.Count == 0 ? 0 : lines.Max(line => DisplayWidth.Of(line.Text)),
            null,
            null
        );

    internal static MermaidDrawResult Fail(string reason) => new(null, 0, reason, null);

    internal static MermaidDrawResult Unsupported(string type) => new(null, 0, null, type);
}
