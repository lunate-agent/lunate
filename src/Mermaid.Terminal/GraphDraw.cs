namespace Mermaid.Terminal;

/// <summary>
/// The drawing half of the flowchart port: boxes, routed lines, arrowheads, corners and labels,
/// merged in mermaid-ascii's order (lines, corners, heads, box joins, labels last).
/// </summary>
internal static class GraphDraw
{
    internal static Drawing DrawBox(GraphNode node, GraphLayout layout)
    {
        // mermaid-ascii sizes a box from the grid columns and rows it starts in, not from its own
        // label: every node sharing a column gets that column's widest label, so chains align.
        GridCoord at = node.GridCoord!.Value;
        int width = layout.ColumnWidth(at.X) + layout.ColumnWidth(at.X + 1);
        int height = layout.RowHeight(at.Y) + layout.RowHeight(at.Y + 1);
        bool unicode = layout.Unicode;
        var box = new Drawing(width, height);
        for (var x = 1; x < width; x++)
        {
            box[x, 0] = Cell.Frame(unicode ? "─" : "-");
            box[x, height] = Cell.Frame(unicode ? "─" : "-");
        }

        for (var y = 1; y < height; y++)
        {
            box[0, y] = Cell.Frame(unicode ? "│" : "|");
            box[width, y] = Cell.Frame(unicode ? "│" : "|");
        }

        box[0, 0] = Cell.Frame(unicode ? "┌" : "+");
        box[width, 0] = Cell.Frame(unicode ? "┐" : "+");
        box[0, height] = Cell.Frame(unicode ? "└" : "+");
        box[width, height] = Cell.Frame(unicode ? "┘" : "+");

        int innerTop = 1;
        int innerHeight = height - 1;
        int contentTop = innerTop + ((innerHeight - node.Label.ContentHeight) / 2);
        for (var index = 0; index < node.Label.Lines.Count; index++)
        {
            string line = node.Label.Lines[index];
            int textY = contentTop + (index * (GraphLabel.Gap + 1));
            int textWidth = DisplayWidth.Of(line);
            int textX = width / 2 - Geometry.CeilDiv(textWidth, 2) + 1;
            if (textWidth == 0)
            {
                textX = 1;
            }

            Drawing.DrawTextCells(box, new DrawingCoord(textX, textY), line);
        }

        return box;
    }

    internal static (
        Drawing Line,
        Drawing BoxStart,
        Drawing Head,
        Drawing Corners,
        Drawing Label
    ) DrawArrow(GraphLayout layout, GraphEdge edge)
    {
        if (edge.Path.Count == 0)
        {
            var blank = new Drawing(0, 0);
            return (blank, blank, blank, blank, blank);
        }

        Drawing label = DrawArrowLabel(layout, edge);
        (Drawing path, List<List<DrawingCoord>> lines, List<Direction> directions) = DrawPath(
            layout,
            edge
        );
        if (lines.Count == 0)
        {
            return (path, new Drawing(0, 0), new Drawing(0, 0), new Drawing(0, 0), label);
        }

        Drawing boxStart = DrawBoxStart(layout, edge, lines[0]);
        bool skipHead = edge.Head == EdgeHead.None;
        if (skipHead)
        {
            Drawing boxEnd = DrawBoxEnd(layout, lines[^1], directions[^1], edge.Stroke);
            boxStart = JunctionMerge.Merge(
                boxStart,
                new DrawingCoord(0, 0),
                layout.Unicode,
                boxEnd
            );
        }

        Drawing head = skipHead
            ? layout.DrawingBlankCopy()
            : DrawArrowHead(layout, lines[^1], directions[^1], edge.Head);
        Drawing corners = DrawCorners(layout, edge);
        return (path, boxStart, head, corners, label);
    }

    private static (
        Drawing Drawing,
        List<List<DrawingCoord>> Lines,
        List<Direction> Directions
    ) DrawPath(GraphLayout layout, GraphEdge edge)
    {
        Drawing drawing = layout.DrawingBlankCopy();
        GridCoord previous = edge.Path[0];
        var lines = new List<List<DrawingCoord>>();
        var directions = new List<Direction>();
        foreach (GridCoord next in edge.Path.Skip(1))
        {
            DrawingCoord previousDrawing = layout.EdgeDrawingCoord(edge, previous);
            DrawingCoord nextDrawing = layout.EdgeDrawingCoord(edge, next);
            if (previousDrawing == nextDrawing)
            {
                continue;
            }

            Direction direction = Geometry.DetermineDirection(previous, next);
            List<DrawingCoord> drawn = DrawLine(
                drawing,
                previousDrawing,
                nextDrawing,
                1,
                -1,
                edge.Stroke,
                layout.Unicode
            );
            if (drawn.Count == 0)
            {
                drawn.Add(previousDrawing);
            }

            lines.Add(drawn);
            directions.Add(direction);
            previous = next;
        }

        return (drawing, lines, directions);
    }

    private static List<DrawingCoord> DrawLine(
        Drawing drawing,
        DrawingCoord from,
        DrawingCoord to,
        int offsetFrom,
        int offsetTo,
        EdgeStroke stroke,
        bool unicode
    )
    {
        Direction direction = Geometry.DetermineDirection(
            new GridCoord(from.X, from.Y),
            new GridCoord(to.X, to.Y)
        );
        var drawn = new List<DrawingCoord>();
        if (unicode)
        {
            string vertical =
                stroke == EdgeStroke.Thick ? "┃"
                : stroke == EdgeStroke.Dotted ? "┆"
                : "│";
            string horizontal =
                stroke == EdgeStroke.Thick ? "━"
                : stroke == EdgeStroke.Dotted ? "┄"
                : "─";
            switch (direction)
            {
                case var d when d == Direction.Up:
                    for (int y = from.Y - offsetFrom; y >= to.Y - offsetTo; y--)
                    {
                        drawn.Add(new DrawingCoord(from.X, y));
                        Set(drawing, from.X, y, vertical);
                    }

                    break;
                case var d when d == Direction.Down:
                    for (int y = from.Y + offsetFrom; y <= to.Y + offsetTo; y++)
                    {
                        drawn.Add(new DrawingCoord(from.X, y));
                        Set(drawing, from.X, y, vertical);
                    }

                    break;
                case var d when d == Direction.Left:
                    for (int x = from.X - offsetFrom; x >= to.X - offsetTo; x--)
                    {
                        drawn.Add(new DrawingCoord(x, from.Y));
                        Set(drawing, x, from.Y, horizontal);
                    }

                    break;
                case var d when d == Direction.Right:
                    for (int x = from.X + offsetFrom; x <= to.X + offsetTo; x++)
                    {
                        drawn.Add(new DrawingCoord(x, from.Y));
                        Set(drawing, x, from.Y, horizontal);
                    }

                    break;
                case var d when d == Direction.UpperLeft:
                    for (
                        int x = from.X, y = from.Y - offsetFrom;
                        x >= to.X - offsetTo && y >= to.Y - offsetTo;
                        x--, y--
                    )
                    {
                        drawn.Add(new DrawingCoord(x, y));
                        Set(drawing, x, y, "╲");
                    }

                    break;
                case var d when d == Direction.UpperRight:
                    for (
                        int x = from.X, y = from.Y - offsetFrom;
                        x <= to.X + offsetTo && y >= to.Y - offsetTo;
                        x++, y--
                    )
                    {
                        drawn.Add(new DrawingCoord(x, y));
                        Set(drawing, x, y, "╱");
                    }

                    break;
                case var d when d == Direction.LowerLeft:
                    for (
                        int x = from.X, y = from.Y + offsetFrom;
                        x >= to.X - offsetTo && y <= to.Y + offsetTo;
                        x--, y++
                    )
                    {
                        drawn.Add(new DrawingCoord(x, y));
                        Set(drawing, x, y, "╱");
                    }

                    break;
                case var d when d == Direction.LowerRight:
                    for (
                        int x = from.X, y = from.Y + offsetFrom;
                        x <= to.X + offsetTo && y <= to.Y + offsetTo;
                        x++, y++
                    )
                    {
                        drawn.Add(new DrawingCoord(x, y));
                        Set(drawing, x, y, "╲");
                    }

                    break;
            }
        }
        else
        {
            string vertical =
                stroke == EdgeStroke.Thick ? "I"
                : stroke == EdgeStroke.Dotted ? ":"
                : "|";
            string horizontal = stroke == EdgeStroke.Thick ? "=" : "-";
            switch (direction)
            {
                case var d when d == Direction.Up:
                    for (int y = from.Y - offsetFrom; y >= to.Y - offsetTo; y--)
                    {
                        drawn.Add(new DrawingCoord(from.X, y));
                        Set(drawing, from.X, y, vertical);
                    }

                    break;
                case var d when d == Direction.Down:
                    for (int y = from.Y + offsetFrom; y <= to.Y + offsetTo; y++)
                    {
                        drawn.Add(new DrawingCoord(from.X, y));
                        Set(drawing, from.X, y, vertical);
                    }

                    break;
                case var d when d == Direction.Left:
                    for (int i = 0, x = from.X - offsetFrom; x >= to.X - offsetTo; i++, x--)
                    {
                        drawn.Add(new DrawingCoord(x, from.Y));
                        if (stroke != EdgeStroke.Dotted || i % 2 == 0)
                        {
                            Set(drawing, x, from.Y, horizontal);
                        }
                    }

                    break;
                case var d when d == Direction.Right:
                    for (int i = 0, x = from.X + offsetFrom; x <= to.X + offsetTo; i++, x++)
                    {
                        drawn.Add(new DrawingCoord(x, from.Y));
                        if (stroke != EdgeStroke.Dotted || i % 2 == 0)
                        {
                            Set(drawing, x, from.Y, horizontal);
                        }
                    }

                    break;
                case var d when d == Direction.UpperLeft:
                    for (
                        int x = from.X, y = from.Y - offsetFrom;
                        x >= to.X - offsetTo && y >= to.Y - offsetTo;
                        x--, y--
                    )
                    {
                        drawn.Add(new DrawingCoord(x, y));
                        Set(drawing, x, y, "\\");
                    }

                    break;
                case var d when d == Direction.UpperRight:
                    for (
                        int x = from.X, y = from.Y - offsetFrom;
                        x <= to.X + offsetTo && y >= to.Y - offsetTo;
                        x++, y--
                    )
                    {
                        drawn.Add(new DrawingCoord(x, y));
                        Set(drawing, x, y, "/");
                    }

                    break;
                case var d when d == Direction.LowerLeft:
                    for (
                        int x = from.X, y = from.Y + offsetFrom;
                        x >= to.X - offsetTo && y <= to.Y + offsetTo;
                        x--, y++
                    )
                    {
                        drawn.Add(new DrawingCoord(x, y));
                        Set(drawing, x, y, "/");
                    }

                    break;
                case var d when d == Direction.LowerRight:
                    for (
                        int x = from.X, y = from.Y + offsetFrom;
                        x <= to.X + offsetTo && y <= to.Y + offsetTo;
                        x++, y++
                    )
                    {
                        drawn.Add(new DrawingCoord(x, y));
                        Set(drawing, x, y, "\\");
                    }

                    break;
            }
        }

        return drawn;
    }

    private static Drawing DrawBoxStart(
        GraphLayout layout,
        GraphEdge edge,
        IReadOnlyList<DrawingCoord> firstLine
    )
    {
        Drawing drawing = layout.DrawingBlankCopy();
        if (firstLine.Count == 0 || layout.Ascii)
        {
            return drawing;
        }

        if (edge.Stroke == EdgeStroke.Thick)
        {
            return drawing;
        }

        DrawingCoord from = firstLine[0];
        Direction direction = Geometry.DetermineDirection(edge.Path[0], edge.Path[1]);
        if (direction == Direction.Up)
        {
            Set(drawing, from.X, from.Y + 1, "┴");
        }
        else if (direction == Direction.Down)
        {
            Set(drawing, from.X, from.Y - 1, "┬");
        }
        else if (direction == Direction.Left)
        {
            Set(drawing, from.X + 1, from.Y, "┤");
        }
        else if (direction == Direction.Right)
        {
            Set(drawing, from.X - 1, from.Y, "├");
        }

        return drawing;
    }

    private static Drawing DrawBoxEnd(
        GraphLayout layout,
        IReadOnlyList<DrawingCoord> lastLine,
        Direction direction,
        EdgeStroke stroke
    )
    {
        Drawing drawing = layout.DrawingBlankCopy();
        if (lastLine.Count == 0 || layout.Ascii || stroke == EdgeStroke.Thick)
        {
            return drawing;
        }

        DrawingCoord to = lastLine[^1];
        if (direction == Direction.Up)
        {
            Set(drawing, to.X, to.Y - 1, "┬");
        }
        else if (direction == Direction.Down)
        {
            Set(drawing, to.X, to.Y + 1, "┴");
        }
        else if (direction == Direction.Left)
        {
            Set(drawing, to.X - 1, to.Y, "├");
        }
        else if (direction == Direction.Right)
        {
            Set(drawing, to.X + 1, to.Y, "┤");
        }

        return drawing;
    }

    private static Drawing DrawArrowHead(
        GraphLayout layout,
        IReadOnlyList<DrawingCoord> line,
        Direction fallback,
        EdgeHead head
    )
    {
        Drawing drawing = layout.DrawingBlankCopy();
        if (line.Count == 0)
        {
            return drawing;
        }

        DrawingCoord from = line[0];
        DrawingCoord last = line[^1];
        Direction direction = Geometry.DetermineDirection(
            new GridCoord(from.X, from.Y),
            new GridCoord(last.X, last.Y)
        );
        if (line.Count == 1 || direction == Direction.Middle)
        {
            direction = fallback;
        }

        string glyph;
        if (head is EdgeHead.Circle or EdgeHead.Cross)
        {
            glyph = head == EdgeHead.Cross ? (layout.Unicode ? "✕" : "x") : "o";
            if (!layout.Unicode)
            {
                glyph = head == EdgeHead.Cross ? "x" : "o";
            }
        }
        else if (layout.Unicode)
        {
            glyph =
                direction == Direction.Up ? "▲"
                : direction == Direction.Down ? "▼"
                : direction == Direction.Left ? "◄"
                : direction == Direction.Right ? "►"
                : direction == Direction.UpperRight ? "◥"
                : direction == Direction.UpperLeft ? "◤"
                : direction == Direction.LowerRight ? "◢"
                : direction == Direction.LowerLeft ? "◣"
                : UnicodeFallback(fallback);
        }
        else
        {
            glyph =
                direction == Direction.Up ? "^"
                : direction == Direction.Down ? "v"
                : direction == Direction.Left ? "<"
                : direction == Direction.Right ? ">"
                : AsciiFallback(fallback);
        }

        Set(drawing, last.X, last.Y, glyph);
        return drawing;
    }

    private static string UnicodeFallback(Direction fallback) =>
        fallback == Direction.Up ? "▲"
        : fallback == Direction.Down ? "▼"
        : fallback == Direction.Left ? "◄"
        : fallback == Direction.Right ? "►"
        : fallback == Direction.UpperRight ? "◥"
        : fallback == Direction.UpperLeft ? "◤"
        : fallback == Direction.LowerRight ? "◢"
        : fallback == Direction.LowerLeft ? "◣"
        : "●";

    private static string AsciiFallback(Direction fallback) =>
        fallback == Direction.Up ? "^"
        : fallback == Direction.Down ? "v"
        : fallback == Direction.Left ? "<"
        : fallback == Direction.Right ? ">"
        : "*";

    private static Drawing DrawCorners(GraphLayout layout, GraphEdge edge)
    {
        Drawing drawing = layout.DrawingBlankCopy();
        bool thick = layout.Unicode && edge.Stroke == EdgeStroke.Thick;
        for (var index = 1; index < edge.Path.Count - 1; index++)
        {
            GridCoord coord = edge.Path[index];
            DrawingCoord at = layout.EdgeDrawingCoord(edge, coord);
            Direction previous = Geometry.DetermineDirection(edge.Path[index - 1], coord);
            Direction next = Geometry.DetermineDirection(coord, edge.Path[index + 1]);
            string corner =
                (previous == Direction.Right && next == Direction.Down)
                || (previous == Direction.Up && next == Direction.Left)
                    ? thick
                        ? "┓"
                        : "┐"
                    : (previous == Direction.Right && next == Direction.Up)
                    || (previous == Direction.Down && next == Direction.Left)
                        ? thick
                            ? "┛"
                            : "┘"
                        : (previous == Direction.Left && next == Direction.Down)
                        || (previous == Direction.Up && next == Direction.Right)
                            ? thick
                                ? "┏"
                                : "┌"
                            : (previous == Direction.Left && next == Direction.Up)
                            || (previous == Direction.Down && next == Direction.Right)
                                ? thick
                                    ? "┗"
                                    : "└"
                                : layout.Unicode
                                    ? "+"
                                    : "+";
            Set(drawing, at.X, at.Y, layout.Unicode ? corner : "+");
        }

        return drawing;
    }

    private static Drawing DrawArrowLabel(GraphLayout layout, GraphEdge edge)
    {
        Drawing drawing = layout.DrawingBlankCopy();
        if (edge.Text.Length == 0)
        {
            return drawing;
        }

        if (edge.Fanout)
        {
            (int x, _, int y) = layout.FanoutLabelBounds(edge);
            drawing.DrawText(x, y, edge.Text);
            return drawing;
        }

        List<DrawingCoord> line = layout.LineToDrawing(edge.LabelLine);
        line = edge.IsBidirectional ? Inset(line, 2, 2) : Inset(line, 1, 2);
        DrawTextOnLine(drawing, line, edge.Text);
        return drawing;
    }

    private static List<DrawingCoord> Inset(List<DrawingCoord> line, int insetStart, int insetEnd)
    {
        if (line.Count < 2 || (insetStart == 0 && insetEnd == 0))
        {
            return line;
        }

        int endInset = insetEnd > 0 ? insetEnd - 1 : 0;
        Direction direction = Geometry.DetermineDirection(
            new GridCoord(line[0].X, line[0].Y),
            new GridCoord(line[1].X, line[1].Y)
        );
        DrawingCoord a = line[0];
        DrawingCoord b = line[1];
        (int dx, int dy) = Geometry.Step(direction);
        a = new DrawingCoord(a.X + (dx * insetStart), a.Y + (dy * insetStart));
        b = new DrawingCoord(b.X - (dx * endInset), b.Y - (dy * endInset));
        return [a, b];
    }

    private static void DrawTextOnLine(
        Drawing drawing,
        IReadOnlyList<DrawingCoord> line,
        string label
    )
    {
        if (line.Count < 2)
        {
            return;
        }

        int minX = Geometry.Min(line[0].X, line[1].X);
        int maxX = Geometry.Max(line[0].X, line[1].X);
        int minY = Geometry.Min(line[0].Y, line[1].Y);
        int maxY = Geometry.Max(line[0].Y, line[1].Y);
        int middleX = minX + ((maxX - minX) / 2);
        int middleY = minY + ((maxY - minY) / 2);
        int length = System.Text.Encoding.UTF8.GetByteCount(label);
        drawing.DrawText(middleX - (length / 2), middleY, label);
    }

    private static void Set(Drawing drawing, int x, int y, string glyph)
    {
        if (x < 0 || y < 0)
        {
            return;
        }

        if (x > drawing.MaxX || y > drawing.MaxY)
        {
            drawing.IncreaseSize(x, y);
        }

        drawing[x, y] = Cell.Frame(glyph);
    }
}
