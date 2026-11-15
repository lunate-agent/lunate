namespace Mermaid.Terminal;

/// <summary>The glyph set a drawing is rendered with.</summary>
public enum MermaidGlyphSet
{
    /// <summary>Box drawing with the Unicode light set and filled arrowheads.</summary>
    Unicode = 0,

    /// <summary>The portable stand-ins: <c>+ - |</c> and <c>&gt; &lt; ^ v</c>.</summary>
    Ascii = 1,
}

/// <summary>What a run of a rendered line is: drawing frame or a node/edge label.</summary>
public enum MermaidSpanKind
{
    /// <summary>Box borders, lines, corners and arrowheads.</summary>
    Frame = 0,

    /// <summary>Text the source supplied: node labels, edge labels, participant names.</summary>
    Label = 1,
}

/// <summary>One run of a rendered line, tagged with what it belongs to.</summary>
public sealed class MermaidSpan
{
    public MermaidSpan(string text, MermaidSpanKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text;
        Kind = kind;
    }

    /// <summary>The characters of this run.</summary>
    public string Text { get; }

    /// <summary>Whether this run is frame or label text.</summary>
    public MermaidSpanKind Kind { get; }
}

/// <summary>One rendered line, as runs a host can paint separately.</summary>
public sealed class MermaidLine
{
    private readonly MermaidSpan[] _spans;

    public MermaidLine(IReadOnlyList<MermaidSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);
        _spans = [.. spans];
        Text = string.Concat(_spans.Select(static span => span.Text));
    }

    /// <summary>The runs of the line, in drawing order.</summary>
    public IReadOnlyList<MermaidSpan> Spans => _spans;

    /// <summary>The line's text, the concatenation of its runs.</summary>
    public string Text { get; }
}

/// <summary>Either a drawing or the reason the source was not drawn; never both.</summary>
public abstract class MermaidRenderResult
{
    private protected MermaidRenderResult() { }
}

/// <summary>A rendered diagram: its lines and the width the drawing occupies.</summary>
public sealed class MermaidDiagramResult : MermaidRenderResult
{
    private readonly MermaidLine[] _lines;

    public MermaidDiagramResult(IReadOnlyList<MermaidLine> lines, int width)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        _lines = [.. lines];
        Width = width;
    }

    /// <summary>The drawing's lines, top to bottom.</summary>
    public IReadOnlyList<MermaidLine> Lines => _lines;

    /// <summary>The width in columns of the drawing, trailing blanks included.</summary>
    public int Width { get; }
}

/// <summary>The source and one reason it was not drawn.</summary>
public sealed class MermaidFallbackResult : MermaidRenderResult
{
    private readonly string[] _source;

    public MermaidFallbackResult(IReadOnlyList<string> source, string reason)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reason);
        _source = [.. source];
        Reason = reason;
    }

    /// <summary>The fence's own lines, unchanged apart from line endings.</summary>
    public IReadOnlyList<string> Source => _source;

    /// <summary>
    /// Why the source was not drawn, without the host's <c>mermaid:</c> prefix or its
    /// "showing the source" tail.
    /// </summary>
    public string Reason { get; }
}
