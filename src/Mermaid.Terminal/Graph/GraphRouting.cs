using System.Text;

namespace Mermaid.Terminal;

internal sealed partial class GraphLayout
{
    private void LayoutFanouts()
    {
        if (Horizontal)
        {
            return;
        }

        foreach (GraphNode node in _nodes)
        {
            List<GraphEdge> branches = FanoutBranches(node);
            if (branches.Count < 2)
            {
                continue;
            }

            int row = node.GridCoord!.Value.Y + 3;
            _rowHeight[row] = Geometry.Max(Get(_rowHeight, row), 6);
            GridCoord from = node.GridCoord.Value + Direction.Down;
            foreach (GraphEdge edge in branches)
            {
                GridCoord to = edge.To.GridCoord!.Value + Direction.Up;
                var branch = new GridCoord(to.X, row);
                edge.Path =
                    from.X == to.X ? [from, to] : [from, new GridCoord(from.X, row), branch, to];
                edge.StartDir = Direction.Down;
                edge.EndDir = Direction.Up;
                edge.LabelLine = [branch, to];
                edge.Fanout = true;
            }

            branches.Sort(
                (left, right) => left.To.GridCoord!.Value.X.CompareTo(right.To.GridCoord!.Value.X)
            );
            for (var index = 0; index < branches.Count; index++)
            {
                GraphEdge edge = branches[index];
                (int start, _, _) = FanoutLabelBounds(edge);
                if (index == 0)
                {
                    _offsetX += Geometry.Max(0, -start);
                    continue;
                }

                (_, int previousEnd, _) = FanoutLabelBounds(branches[index - 1]);
                int column = edge.To.GridCoord!.Value.X - 1;
                _columnWidth[column] =
                    Get(_columnWidth, column) + Geometry.Max(0, previousEnd + 3 - start);
            }
        }
    }

    private List<GraphEdge> FanoutBranches(GraphNode node)
    {
        var branches = new List<GraphEdge>();
        var labeled = false;
        var seen = new HashSet<GraphNode>();
        foreach (GraphEdge edge in _edges)
        {
            if (!ReferenceEquals(edge.From, node))
            {
                continue;
            }

            if (
                edge.IsBidirectional
                || edge.Stroke != EdgeStroke.Solid
                || edge.Head != EdgeHead.Arrow
                || edge.To.GridCoord!.Value.Y != node.GridCoord!.Value.Y + GraphMetrics.LevelPitch
                || !seen.Add(edge.To)
            )
            {
                return [];
            }

            labeled |= edge.Text.Length > 0;
            branches.Add(edge);
        }

        if (!labeled)
        {
            return [];
        }

        int row = node.GridCoord!.Value.Y + 3;
        foreach (GraphEdge edge in _edges)
        {
            if (ReferenceEquals(edge.From, node))
            {
                continue;
            }

            int fromY = edge.From.GridCoord!.Value.Y;
            int toY = edge.To.GridCoord!.Value.Y;
            if (
                RangesOverlap(fromY, toY, row, row)
                || (fromY == toY && RangesOverlap(fromY - 1, fromY + 3, row, row))
            )
            {
                return [];
            }
        }

        return branches;
    }

    internal DrawingCoord EdgeDrawingCoord(GraphEdge edge, GridCoord coord)
    {
        DrawingCoord position = GridToDrawingCoord(coord);
        if (edge.Fanout && coord.Y == edge.From.GridCoord!.Value.Y + 3)
        {
            position = position with
            {
                Y = GridToDrawingCoord(edge.From.GridCoord.Value + Direction.Down).Y + 2,
            };
        }

        return position;
    }

    internal (int Start, int End, int Y) FanoutLabelBounds(GraphEdge edge)
    {
        DrawingCoord from = EdgeDrawingCoord(edge, edge.LabelLine[0]);
        DrawingCoord to = EdgeDrawingCoord(edge, edge.LabelLine[1]);
        int width = Geometry.Max(1, DisplayWidth.Of(edge.Text));
        int start = to.X - (width / 2);
        return (start, start + width - 1, from.Y + ((to.Y - from.Y) / 2));
    }

    private void DeterminePath(GraphEdge edge)
    {
        (int From, int To) key = EdgeKey(edge);
        int duplicateIndex = _edgeCounts.GetValueOrDefault(key);
        if (ParallelDirections(edge, duplicateIndex, out Direction start, out Direction end))
        {
            GridCoord from = edge.From.GridCoord!.Value + start;
            GridCoord to = edge.To.GridCoord!.Value + end;
            List<GridCoord>? path = GetPath(from, to);
            if (path is not null)
            {
                edge.StartDir = start;
                edge.EndDir = end;
                edge.Path = MergePath(path);
                _edgeCounts[key] = duplicateIndex + 1;
                return;
            }
        }

        (
            Direction preferredDir,
            Direction preferredOpposite,
            Direction alternativeDir,
            Direction alternativeOpposite
        ) = DetermineStartAndEndDir(edge);

        GridCoord preferredFrom = edge.From.GridCoord!.Value + preferredDir;
        GridCoord preferredTo = edge.To.GridCoord!.Value + preferredOpposite;
        List<GridCoord>? preferredPath = GetPath(preferredFrom, preferredTo);
        if (preferredPath is null)
        {
            edge.StartDir = alternativeDir;
            edge.EndDir = alternativeOpposite;
            edge.Path = [];
            return;
        }

        preferredPath = MergePath(preferredPath);
        GridCoord alternativeFrom = edge.From.GridCoord!.Value + alternativeDir;
        GridCoord alternativeTo = edge.To.GridCoord!.Value + alternativeOpposite;
        List<GridCoord>? alternativePath = GetPath(alternativeFrom, alternativeTo);
        if (alternativePath is null)
        {
            edge.StartDir = preferredDir;
            edge.EndDir = preferredOpposite;
            edge.Path = preferredPath;
            _edgeCounts[key] = duplicateIndex + 1;
            return;
        }

        alternativePath = MergePath(alternativePath);
        if (preferredPath.Count <= alternativePath.Count)
        {
            edge.StartDir = preferredDir;
            edge.EndDir = preferredOpposite;
            edge.Path = preferredPath;
        }
        else
        {
            edge.StartDir = alternativeDir;
            edge.EndDir = alternativeOpposite;
            edge.Path = alternativePath;
        }

        _edgeCounts[key] = duplicateIndex + 1;
    }

    private static (int From, int To) EdgeKey(GraphEdge edge) =>
        edge.From.Index < edge.To.Index
            ? (edge.From.Index, edge.To.Index)
            : (edge.To.Index, edge.From.Index);

    private bool ParallelDirections(
        GraphEdge edge,
        int duplicateIndex,
        out Direction start,
        out Direction end
    )
    {
        start = Direction.Middle;
        end = Direction.Middle;
        if (duplicateIndex == 0)
        {
            return false;
        }

        Direction direction = Geometry.DetermineDirection(
            edge.From.GridCoord!.Value,
            edge.To.GridCoord!.Value
        );
        if (Horizontal && (direction == Direction.Right || direction == Direction.Left))
        {
            (Direction, Direction)[] options =
            [
                (Direction.Down, Direction.Down),
                (Direction.Up, Direction.Up),
            ];
            if (duplicateIndex - 1 < options.Length)
            {
                (start, end) = options[duplicateIndex - 1];
                return true;
            }
        }

        if (!Horizontal && (direction == Direction.Down || direction == Direction.Up))
        {
            (Direction, Direction)[] options =
            [
                (Direction.Right, Direction.Right),
                (Direction.Left, Direction.Left),
            ];
            if (duplicateIndex - 1 < options.Length)
            {
                (start, end) = options[duplicateIndex - 1];
                return true;
            }
        }

        return false;
    }

    private (
        Direction Preferred,
        Direction PreferredOpposite,
        Direction Alternative,
        Direction AlternativeOpposite
    ) DetermineStartAndEndDir(GraphEdge edge)
    {
        if (ReferenceEquals(edge.From, edge.To))
        {
            return Horizontal
                ? (Direction.Right, Direction.Down, Direction.Down, Direction.Right)
                : (Direction.Down, Direction.Right, Direction.Right, Direction.Down);
        }

        Direction direction = Geometry.DetermineDirection(
            edge.From.GridCoord!.Value,
            edge.To.GridCoord!.Value
        );
        bool backwards = Horizontal
            ? direction == Direction.Left
                || direction == Direction.UpperLeft
                || direction == Direction.LowerLeft
            : direction == Direction.Up
                || direction == Direction.UpperLeft
                || direction == Direction.UpperRight;

        if (direction == Direction.LowerRight)
        {
            return Horizontal
                ? (Direction.Down, Direction.Left, Direction.Right, Direction.Up)
                : (Direction.Right, Direction.Up, Direction.Down, Direction.Left);
        }

        if (direction == Direction.UpperRight)
        {
            return Horizontal
                ? (Direction.Up, Direction.Left, Direction.Right, Direction.Down)
                : (Direction.Right, Direction.Down, Direction.Up, Direction.Left);
        }

        if (direction == Direction.LowerLeft)
        {
            return Horizontal
                ? (Direction.Down, Direction.Down, Direction.Left, Direction.Up)
                : (Direction.Left, Direction.Up, Direction.Down, Direction.Right);
        }

        if (direction == Direction.UpperLeft)
        {
            return Horizontal
                ? (Direction.Down, Direction.Down, Direction.Left, Direction.Down)
                : (Direction.Right, Direction.Right, Direction.Up, Direction.Right);
        }

        if (backwards)
        {
            if (Horizontal && direction == Direction.Left)
            {
                return (Direction.Down, Direction.Down, Direction.Left, Direction.Right);
            }

            if (!Horizontal && direction == Direction.Up)
            {
                return (Direction.Right, Direction.Right, Direction.Up, Direction.Down);
            }
        }

        return (direction, direction.Opposite(), direction, direction.Opposite());
    }

    private List<GridCoord>? GetPath(GridCoord from, GridCoord to)
    {
        var queue = new GridHeap();
        queue.Push(new HeapItem(from, 0));
        var costSoFar = new Dictionary<GridCoord, int> { [from] = 0 };
        var cameFrom = new Dictionary<GridCoord, GridCoord?> { [from] = null };
        GridCoord[] directions = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

        while (queue.Count > 0)
        {
            GridCoord current = queue.Pop().Coord;
            if (current == to)
            {
                var path = new List<GridCoord>();
                for (GridCoord? cursor = current; cursor is not null; )
                {
                    path.Insert(0, cursor.Value);
                    cursor = cameFrom.TryGetValue(cursor.Value, out GridCoord? previous)
                        ? previous
                        : null;
                }

                return path;
            }

            foreach (GridCoord direction in directions)
            {
                var next = new GridCoord(current.X + direction.X, current.Y + direction.Y);
                if (!IsFreeInGrid(next) && next != to)
                {
                    continue;
                }

                int newCost = costSoFar[current] + 1;
                if (!costSoFar.TryGetValue(next, out int cost) || newCost < cost)
                {
                    costSoFar[next] = newCost;
                    queue.Push(new HeapItem(next, newCost + Heuristic(next, to)));
                    cameFrom[next] = current;
                }
            }
        }

        return null;
    }

    private bool IsFreeInGrid(GridCoord coord) =>
        coord.X >= 0 && coord.Y >= 0 && !_grid.ContainsKey(coord);

    private static int Heuristic(GridCoord from, GridCoord to)
    {
        int absX = Geometry.Abs(from.X - to.X);
        int absY = Geometry.Abs(from.Y - to.Y);
        return absX == 0 || absY == 0 ? absX + absY : absX + absY + 1;
    }

    private static List<GridCoord> MergePath(IReadOnlyList<GridCoord> path)
    {
        if (path.Count <= 2)
        {
            return [.. path];
        }

        var remove = new HashSet<int>();
        GridCoord previous = path[0];
        GridCoord current = path[1];
        for (var index = 2; index < path.Count; index++)
        {
            GridCoord next = path[index];
            Direction previousDirection = Geometry.DetermineDirection(previous, current);
            Direction direction = Geometry.DetermineDirection(current, next);
            if (previousDirection == direction)
            {
                remove.Add(index - 1);
            }

            previous = current;
            current = next;
        }

        return path.Where((_, index) => !remove.Contains(index)).ToList();
    }

    private void DetermineLabelLine(GraphEdge edge)
    {
        int labelLength = Encoding.UTF8.GetByteCount(edge.Text);
        if (labelLength == 0)
        {
            return;
        }

        GridCoord previous = edge.Path[0];
        List<GridCoord>? largestLine = null;
        int largestLineSize = 0;
        List<GridCoord>? fallbackLine = null;
        int fallbackLineSize = 0;
        foreach (GridCoord step in edge.Path.Skip(1))
        {
            List<GridCoord> line = [previous, step];
            previous = step;
            int lineWidth = CalculateLineWidth(line);
            if (IsNodeColumn(LabelMiddleX(line)))
            {
                if (LabelBoundsAvailable(edge, line) && lineWidth > fallbackLineSize)
                {
                    fallbackLineSize = lineWidth;
                    fallbackLine = line;
                }

                continue;
            }

            if (LabelLineAvailable(edge, line) && LabelBoundsAvailable(edge, line))
            {
                if (lineWidth >= labelLength)
                {
                    largestLine = line;
                    break;
                }

                if (lineWidth > largestLineSize)
                {
                    largestLineSize = lineWidth;
                    largestLine = line;
                }
            }
        }

        if (largestLine is null)
        {
            foreach (List<GridCoord> line in UnusedLabelSubsegments(edge))
            {
                int lineWidth = CalculateLineWidth(line);
                if (lineWidth >= labelLength)
                {
                    largestLine = line;
                    break;
                }

                if (lineWidth > largestLineSize)
                {
                    largestLineSize = lineWidth;
                    largestLine = line;
                }
            }
        }

        largestLine ??= fallbackLine;
        largestLine ??= edge.Path.Count >= 2 ? [edge.Path[0], edge.Path[1]] : edge.Path;
        if (largestLine.Count < 2)
        {
            return;
        }

        int middleX = LabelMiddleX(largestLine);
        int labelPadding = edge.IsBidirectional ? 4 : 3;
        _columnWidth[middleX] = Geometry.Max(
            Get(_columnWidth, middleX),
            labelLength + labelPadding
        );
        EnsureLabelLineClear(edge, largestLine);
        edge.LabelLine = largestLine;
    }

    private static int LabelMiddleX(IReadOnlyList<GridCoord> line) =>
        Geometry.Min(line[0].X, line[1].X)
        + ((Geometry.Max(line[0].X, line[1].X) - Geometry.Min(line[0].X, line[1].X)) / 2);

    private bool LabelLineAvailable(GraphEdge edge, IReadOnlyList<GridCoord> line)
    {
        foreach (GraphEdge other in _edges)
        {
            if (ReferenceEquals(other, edge) || other.LabelLine.Count == 0)
            {
                continue;
            }

            if (LabelsOverlap(edge, line, other))
            {
                return false;
            }
        }

        return true;
    }

    private bool LabelBoundsAvailable(GraphEdge edge, IReadOnlyList<GridCoord> line)
    {
        List<DrawingCoord> drawingLine = LineToDrawing(line);
        drawingLine = edge.IsBidirectional
            ? InsetLine(drawingLine, 2, 2)
            : InsetLine(drawingLine, 1, 2);
        (int start, int end, int y) = LabelBounds(drawingLine, edge.Text);
        foreach (GraphEdge other in _edges)
        {
            if (other.Path.Count < 2 || other.Head == EdgeHead.None)
            {
                continue;
            }

            DrawingCoord arrow = ArrowheadPosition(other);
            if (arrow.Y == y && RangesOverlap(start, end, arrow.X, arrow.X))
            {
                return false;
            }

            if (other.IsBidirectional)
            {
                arrow = ArrowheadPositionFromStart(other);
                if (arrow.Y == y && RangesOverlap(start, end, arrow.X, arrow.X))
                {
                    return false;
                }
            }
        }

        foreach (GraphNode node in _nodes)
        {
            if (LabelOverlapsNodeBorder(node, start, end, y))
            {
                return false;
            }
        }

        return true;
    }

    private DrawingCoord ArrowheadPosition(GraphEdge edge)
    {
        DrawingCoord last = GridToDrawingCoord(edge.Path[^1]);
        if (edge.Path.Count < 2)
        {
            return last;
        }

        DrawingCoord previous = GridToDrawingCoord(edge.Path[^2]);
        Direction direction = Geometry.DetermineDirection(
            new GridCoord(previous.X, previous.Y),
            new GridCoord(last.X, last.Y)
        );
        (int dx, int dy) = Geometry.Step(direction);
        return new DrawingCoord(last.X - dx, last.Y - dy);
    }

    private DrawingCoord ArrowheadPositionFromStart(GraphEdge edge)
    {
        DrawingCoord first = GridToDrawingCoord(edge.Path[0]);
        if (edge.Path.Count < 2)
        {
            return first;
        }

        DrawingCoord next = GridToDrawingCoord(edge.Path[1]);
        Direction direction = Geometry.DetermineDirection(
            new GridCoord(next.X, next.Y),
            new GridCoord(first.X, first.Y)
        );
        (int dx, int dy) = Geometry.Step(direction);
        return new DrawingCoord(first.X - dx, first.Y - dy);
    }

    private bool LabelOverlapsNodeBorder(GraphNode node, int start, int end, int y)
    {
        if (node.GridCoord is not { } coord)
        {
            return false;
        }

        DrawingCoord from = GridToDrawingCoord(coord);
        var to = new DrawingCoord(
            from.X + ColumnWidth(coord.X) + ColumnWidth(coord.X + 1),
            from.Y + RowHeight(coord.Y) + RowHeight(coord.Y + 1)
        );
        if (y != from.Y && y != to.Y)
        {
            return false;
        }

        return RangesOverlap(start, end, from.X, to.X);
    }

    private void EnsureLabelLineClear(GraphEdge edge, IReadOnlyList<GridCoord> line)
    {
        for (var attempt = 0; attempt < 8 && !LabelBoundsAvailable(edge, line); attempt++)
        {
            Direction direction = Geometry.DetermineDirection(line[0], line[1]);
            if (direction == Direction.Down)
            {
                _rowHeight[line[0].Y + 1] = Get(_rowHeight, line[0].Y + 1) + 2;
            }
            else if (direction == Direction.Up)
            {
                _rowHeight[line[0].Y - 1] = Get(_rowHeight, line[0].Y - 1) + 2;
            }
            else if (direction == Direction.Right)
            {
                _columnWidth[line[0].X + 1] = Get(_columnWidth, line[0].X + 1) + 2;
            }
            else if (direction == Direction.Left)
            {
                _columnWidth[line[0].X - 1] = Get(_columnWidth, line[0].X - 1) + 2;
            }
            else
            {
                return;
            }
        }
    }

    private bool LabelsOverlap(
        GraphEdge firstEdge,
        IReadOnlyList<GridCoord> firstLine,
        GraphEdge secondEdge
    )
    {
        List<DrawingCoord> firstDrawing = LineToDrawing(firstLine);
        List<DrawingCoord> secondDrawing = LineToDrawing(secondEdge.LabelLine);
        firstDrawing = firstEdge.IsBidirectional
            ? InsetLine(firstDrawing, 2, 2)
            : InsetLine(firstDrawing, 1, 2);
        secondDrawing = secondEdge.IsBidirectional
            ? InsetLine(secondDrawing, 2, 2)
            : InsetLine(secondDrawing, 1, 2);
        (int firstStart, int firstEnd, int firstY) = LabelBounds(firstDrawing, firstEdge.Text);
        (int secondStart, int secondEnd, int secondY) = LabelBounds(secondDrawing, secondEdge.Text);
        return firstY == secondY && RangesOverlap(firstStart, firstEnd, secondStart, secondEnd);
    }

    private static (int Start, int End, int Y) LabelBounds(
        IReadOnlyList<DrawingCoord> line,
        string label
    )
    {
        int length = Encoding.UTF8.GetByteCount(label);
        int middleX = line[0].X + ((line[1].X - line[0].X) / 2);
        int middleY = line[0].Y + ((line[1].Y - line[0].Y) / 2);
        int start = middleX - (length / 2);
        return (start, start + length - 1, middleY);
    }

    private static bool RangesOverlap(int firstStart, int firstEnd, int secondStart, int secondEnd)
    {
        int firstMin = Geometry.Min(firstStart, firstEnd);
        int firstMax = Geometry.Max(firstStart, firstEnd);
        int secondMin = Geometry.Min(secondStart, secondEnd);
        int secondMax = Geometry.Max(secondStart, secondEnd);
        return firstMin <= secondMax && secondMin <= firstMax;
    }

    private IEnumerable<List<GridCoord>> UnusedLabelSubsegments(GraphEdge edge)
    {
        for (var index = 0; index < edge.Path.Count - 1; index++)
        {
            GridCoord start = edge.Path[index];
            GridCoord end = edge.Path[index + 1];
            if (start.Y != end.Y || start.X == end.X)
            {
                continue;
            }

            int minX = Geometry.Min(start.X, end.X);
            int maxX = Geometry.Max(start.X, end.X);
            for (int middleX = minX + 1; middleX < maxX; middleX++)
            {
                if (IsNodeColumn(middleX))
                {
                    continue;
                }

                List<GridCoord> line = [new(middleX - 1, start.Y), new(middleX + 1, start.Y)];
                if (LabelLineAvailable(edge, line) && LabelBoundsAvailable(edge, line))
                {
                    yield return line;
                }
            }
        }
    }

    private bool IsNodeColumn(int x)
    {
        foreach (GraphNode node in _nodes)
        {
            if (node.GridCoord is { } coord && x >= coord.X && x <= coord.X + 2)
            {
                return true;
            }
        }

        return false;
    }

    private int CalculateLineWidth(IReadOnlyList<GridCoord> line) =>
        line.Sum(coord => ColumnWidth(coord.X));

    internal List<DrawingCoord> LineToDrawing(IReadOnlyList<GridCoord> line) =>
        line.Select(GridToDrawingCoord).ToList();

    private static List<DrawingCoord> InsetLine(
        List<DrawingCoord> line,
        int insetStart,
        int insetEnd
    )
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

    private sealed record HeapItem(GridCoord Coord, int Priority);

    /// <summary>
    /// Go's <c>container/heap</c> with a priority-only Less, reproduced so an equal-cost choice of
    /// path comes out in the same order as the renderer this is ported from.
    /// </summary>
    private sealed class GridHeap
    {
        private readonly List<HeapItem> _items = [];

        internal int Count => _items.Count;

        internal void Push(HeapItem item)
        {
            _items.Add(item);
            Up(_items.Count - 1);
        }

        internal HeapItem Pop()
        {
            int last = _items.Count - 1;
            Swap(0, last);
            Down(0, last);
            HeapItem item = _items[^1];
            _items.RemoveAt(last);
            return item;
        }

        private void Up(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!Less(index, parent))
                {
                    return;
                }

                Swap(index, parent);
                index = parent;
            }
        }

        private void Down(int index, int count)
        {
            while (true)
            {
                int child = (2 * index) + 1;
                if (child >= count || child < 0)
                {
                    return;
                }

                if (child + 1 < count && Less(child + 1, child))
                {
                    child++;
                }

                if (!Less(child, index))
                {
                    return;
                }

                Swap(index, child);
                index = child;
            }
        }

        private bool Less(int first, int second) =>
            _items[first].Priority < _items[second].Priority;

        private void Swap(int first, int second) =>
            (_items[first], _items[second]) = (_items[second], _items[first]);
    }
}
