namespace Mermaid.Terminal;

/// <summary>A node label split into its lines, with the width and height the box needs.</summary>
internal sealed class GraphLabel
{
    private const int LineGap = 1;

    internal GraphLabel(string raw)
    {
        string normalized = raw.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lineList = new List<string>();
        int cursor = 0;
        while (cursor <= normalized.Length)
        {
            int at = normalized.IndexOf('\n', cursor);
            if (at < 0)
            {
                lineList.Add(normalized[cursor..]);
                break;
            }

            lineList.Add(normalized[cursor..at]);
            cursor = at + 1;
        }

        if (lineList.Count == 0)
        {
            lineList.Add(string.Empty);
        }

        Lines = lineList;
        Width = lineList.Count == 0 ? 0 : lineList.Max(DisplayWidth.Of);
    }

    internal IReadOnlyList<string> Lines { get; }

    internal int Width { get; }

    internal int ContentHeight =>
        Lines.Count == 0 ? 0 : Lines.Count + ((Lines.Count - 1) * LineGap);

    internal static int Gap => LineGap;
}

internal sealed class GraphNode(string name, IReadOnlyList<string> labelLines, int index)
{
    internal string Name { get; } = name;

    internal GraphLabel Label { get; } = new(string.Join('\n', labelLines));

    internal int Index { get; } = index;

    internal Drawing? Drawing { get; set; }

    internal DrawingCoord? DrawingCoord { get; set; }

    internal GridCoord? GridCoord { get; set; }

    internal bool Drawn { get; set; }
}

internal sealed class GraphEdge(GraphNode from, GraphNode to, FlowchartEdge source)
{
    internal GraphNode From { get; } = from;

    internal GraphNode To { get; } = to;

    internal string Text { get; } = source.Label ?? string.Empty;

    internal bool IsBidirectional { get; }

    internal EdgeStroke Stroke { get; } = source.Dashed ? EdgeStroke.Dotted : EdgeStroke.Solid;

    internal EdgeHead Head { get; } = source.Arrowhead ? EdgeHead.Arrow : EdgeHead.None;

    internal List<GridCoord> Path { get; set; } = [];

    internal List<GridCoord> LabelLine { get; set; } = [];

    internal bool Fanout { get; set; }

    internal Direction StartDir { get; set; }

    internal Direction EndDir { get; set; }
}

/// <summary>The layout grid's pitch: three cells per node, one cell of padding between nodes.</summary>
internal static class GraphMetrics
{
    internal const int BoxBorderPadding = 1;
    internal const int PaddingX = 5;
    internal const int PaddingY = 5;
    internal const int LevelPitch = 4;
}
