namespace Mermaid.Terminal;

/// <summary>
/// mermaid-ascii's junction table: when two drawings overlap, a box-drawing character meeting
/// another becomes the glyph that joins both strokes instead of replacing one with the other.
/// </summary>
internal static class JunctionMerge
{
    private static readonly HashSet<string> JunctionChars =
    [
        "─",
        "│",
        "┌",
        "┐",
        "└",
        "┘",
        "├",
        "┤",
        "┬",
        "┴",
        "┼",
        "╴",
        "╵",
        "╶",
        "╷",
    ];

    private static readonly Dictionary<string, Dictionary<string, string>> Map = new()
    {
        ["─"] = Pairs(
            ("│", "┼"),
            ("┌", "┬"),
            ("┐", "┬"),
            ("└", "┴"),
            ("┘", "┴"),
            ("├", "┼"),
            ("┤", "┼"),
            ("┬", "┬"),
            ("┴", "┴")
        ),
        ["│"] = Pairs(
            ("─", "┼"),
            ("┌", "├"),
            ("┐", "┤"),
            ("└", "├"),
            ("┘", "┤"),
            ("├", "├"),
            ("┤", "┤"),
            ("┬", "┼"),
            ("┴", "┼")
        ),
        ["┌"] = Pairs(
            ("─", "┬"),
            ("│", "├"),
            ("┐", "┬"),
            ("└", "├"),
            ("┘", "┼"),
            ("├", "├"),
            ("┤", "┼"),
            ("┬", "┬"),
            ("┴", "┼")
        ),
        ["┐"] = Pairs(
            ("─", "┬"),
            ("│", "┤"),
            ("┌", "┬"),
            ("└", "┼"),
            ("┘", "┤"),
            ("├", "┼"),
            ("┤", "┤"),
            ("┬", "┬"),
            ("┴", "┼")
        ),
        ["└"] = Pairs(
            ("─", "┴"),
            ("│", "├"),
            ("┌", "├"),
            ("┐", "┼"),
            ("┘", "┴"),
            ("├", "├"),
            ("┤", "┼"),
            ("┬", "┼"),
            ("┴", "┴")
        ),
        ["┘"] = Pairs(
            ("─", "┴"),
            ("│", "┤"),
            ("┌", "┼"),
            ("┐", "┤"),
            ("└", "┴"),
            ("├", "┼"),
            ("┤", "┤"),
            ("┬", "┼"),
            ("┴", "┴")
        ),
        ["├"] = Pairs(
            ("─", "┼"),
            ("│", "├"),
            ("┌", "├"),
            ("┐", "┼"),
            ("└", "├"),
            ("┘", "┼"),
            ("┤", "┼"),
            ("┬", "┼"),
            ("┴", "┼")
        ),
        ["┤"] = Pairs(
            ("─", "┼"),
            ("│", "┤"),
            ("┌", "┼"),
            ("┐", "┤"),
            ("└", "┼"),
            ("┘", "┤"),
            ("├", "┼"),
            ("┬", "┼"),
            ("┴", "┼")
        ),
        ["┬"] = Pairs(
            ("─", "┬"),
            ("│", "┼"),
            ("┌", "┬"),
            ("┐", "┬"),
            ("└", "┼"),
            ("┘", "┼"),
            ("├", "┼"),
            ("┤", "┼"),
            ("┴", "┼")
        ),
        ["┴"] = Pairs(
            ("─", "┴"),
            ("│", "┼"),
            ("┌", "┼"),
            ("┐", "┼"),
            ("└", "┴"),
            ("┘", "┴"),
            ("├", "┼"),
            ("┤", "┼"),
            ("┬", "┼")
        ),
    };

    private static Dictionary<string, string> Pairs(params (string Other, string Merged)[] pairs)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string other, string merged) in pairs)
        {
            map[other] = merged;
        }

        return map;
    }

    internal static bool IsJunction(string glyph) => JunctionChars.Contains(glyph);

    internal static string Merge(string first, string second) =>
        Map.TryGetValue(first, out Dictionary<string, string>? merges)
        && merges.TryGetValue(second, out string? merged)
            ? merged
            : first;

    /// <summary>
    /// Copy <paramref name="baseDrawing"/> and overlay every other drawing at
    /// <paramref name="at"/>. A non-blank cell of an overlay wins, except that two junction
    /// characters combine into the glyph joining both strokes.
    /// </summary>
    internal static Drawing Merge(
        Drawing baseDrawing,
        DrawingCoord at,
        bool unicode,
        params Drawing[] overlays
    )
    {
        int maxX = baseDrawing.MaxX;
        int maxY = baseDrawing.MaxY;
        foreach (Drawing overlay in overlays)
        {
            maxX = Geometry.Max(maxX, overlay.MaxX + at.X);
            maxY = Geometry.Max(maxY, overlay.MaxY + at.Y);
        }

        var merged = new Drawing(maxX, maxY);
        for (var x = 0; x <= baseDrawing.MaxX; x++)
        {
            for (var y = 0; y <= baseDrawing.MaxY; y++)
            {
                merged[x, y] = baseDrawing[x, y];
            }
        }

        foreach (Drawing overlay in overlays)
        {
            for (var x = 0; x <= overlay.MaxX; x++)
            {
                for (var y = 0; y <= overlay.MaxY; y++)
                {
                    Cell cell = overlay[x, y];
                    if (cell.Content == " ")
                    {
                        continue;
                    }

                    Cell current = merged[x + at.X, y + at.Y];
                    merged[x + at.X, y + at.Y] =
                        unicode
                        && !cell.Textual
                        && !current.Textual
                        && IsJunction(cell.Content)
                        && IsJunction(current.Content)
                            ? Cell.Frame(Merge(current.Content, cell.Content))
                            : cell;
                }
            }
        }

        return merged;
    }
}
