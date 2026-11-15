namespace Mermaid.Terminal;

/// <summary>
/// The flowchart layout, ported from mermaid-ascii: every node reserves a 3×3 block on a grid,
/// roots are placed at level 0, children four grid cells further along the drawing axis, and the
/// column/row sizes are grown to the widest node in each line. Edges are routed through the free
/// grid cells afterwards, so an edge that bends is a path the drawing can render.
/// </summary>
internal sealed partial class GraphLayout
{
    private const int Scale = GraphMetrics.LevelPitch;

    private readonly List<GraphNode> _nodes;
    private readonly List<GraphEdge> _edges;
    private readonly Dictionary<GridCoord, GraphNode> _grid = [];
    private readonly Dictionary<(int From, int To), int> _edgeCounts = [];
    private readonly Dictionary<int, int> _columnWidth = [];
    private readonly Dictionary<int, int> _rowHeight = [];
    private Drawing _drawing = new(0, 0);
    private int _offsetX;

    internal GraphLayout(FlowchartModel model)
    {
        bool horizontal = model.Horizontal;
        _nodes = [];
        var byId = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        foreach (FlowchartNode node in model.Nodes)
        {
            var mapped = new GraphNode(node.Id, node.LabelLines, _nodes.Count);
            _nodes.Add(mapped);
            byId[node.Id] = mapped;
        }

        _edges = [];
        foreach (FlowchartEdge edge in model.Edges)
        {
            _edges.Add(new GraphEdge(byId[edge.From], byId[edge.To], edge));
        }

        Horizontal = horizontal;
    }

    internal bool Horizontal { get; }

    internal bool Unicode { get; init; } = true;

    internal bool Ascii => !Unicode;

    internal Drawing DrawingBlankCopy() => _drawing.BlankCopy();

    internal Drawing Draw()
    {
        CreateMapping();
        _drawing = new Drawing(0, 0);
        foreach (GraphNode node in _nodes)
        {
            if (!node.Drawn)
            {
                _drawing = JunctionMerge.Merge(
                    _drawing,
                    node.DrawingCoord!.Value,
                    Unicode,
                    GraphDraw.DrawBox(node, this)
                );
                node.Drawn = true;
            }
        }

        var lineDrawings = new List<Drawing>();
        var cornerDrawings = new List<Drawing>();
        var headDrawings = new List<Drawing>();
        var boxStartDrawings = new List<Drawing>();
        var labelDrawings = new List<Drawing>();
        foreach (GraphEdge edge in _edges)
        {
            (Drawing line, Drawing boxStart, Drawing head, Drawing corners, Drawing label) =
                GraphDraw.DrawArrow(this, edge);
            lineDrawings.Add(line);
            cornerDrawings.Add(corners);
            headDrawings.Add(head);
            boxStartDrawings.Add(boxStart);
            labelDrawings.Add(label);
        }

        _drawing = JunctionMerge.Merge(
            _drawing,
            new DrawingCoord(0, 0),
            Unicode,
            [.. lineDrawings]
        );
        _drawing = JunctionMerge.Merge(
            _drawing,
            new DrawingCoord(0, 0),
            Unicode,
            [.. cornerDrawings]
        );
        _drawing = JunctionMerge.Merge(
            _drawing,
            new DrawingCoord(0, 0),
            Unicode,
            [.. headDrawings]
        );
        _drawing = JunctionMerge.Merge(
            _drawing,
            new DrawingCoord(0, 0),
            Unicode,
            [.. boxStartDrawings]
        );
        _drawing = JunctionMerge.Merge(
            _drawing,
            new DrawingCoord(0, 0),
            Unicode,
            [.. labelDrawings]
        );
        return _drawing;
    }

    private void CreateMapping()
    {
        var highestPositionPerLevel = new Dictionary<int, int>();
        var found = new HashSet<string>(StringComparer.Ordinal);
        var rootNodes = new List<GraphNode>();
        foreach (GraphNode node in _nodes)
        {
            if (!found.Contains(node.Name))
            {
                rootNodes.Add(node);
            }

            found.Add(node.Name);
            foreach (GraphNode child in Children(node))
            {
                found.Add(child.Name);
            }
        }

        foreach (GraphNode node in rootNodes)
        {
            GridCoord requested = Horizontal
                ? new GridCoord(0, Highest(highestPositionPerLevel, 0))
                : new GridCoord(Highest(highestPositionPerLevel, 0), 0);
            ReserveSpotInGrid(node, requested);
            highestPositionPerLevel[0] = Highest(highestPositionPerLevel, 0) + Scale;
        }

        foreach (GraphNode node in _nodes)
        {
            int childLevel = Horizontal
                ? node.GridCoord!.Value.X + Scale
                : node.GridCoord!.Value.Y + Scale;
            int highestPosition = Highest(highestPositionPerLevel, childLevel);
            foreach (GraphNode child in Children(node))
            {
                if (child.GridCoord is not null)
                {
                    continue;
                }

                GridCoord requested = Horizontal
                    ? new GridCoord(childLevel, highestPosition)
                    : new GridCoord(highestPosition, childLevel);
                GridCoord mapped = ReserveSpotInGrid(child, requested);
                int position = highestPosition + Scale;
                position = Horizontal
                    ? Geometry.Max(position, mapped.Y + Scale)
                    : Geometry.Max(position, mapped.X + Scale);
                highestPositionPerLevel[childLevel] = position;
            }
        }

        foreach (GraphNode node in _nodes)
        {
            SetColumnWidth(node);
        }

        LayoutFanouts();

        foreach (GraphEdge edge in _edges)
        {
            if (edge.Fanout)
            {
                continue;
            }

            DeterminePath(edge);
            IncreaseGridSizeForPath(edge.Path);
            DetermineLabelLine(edge);
        }

        foreach (GraphNode node in _nodes)
        {
            node.DrawingCoord = GridToDrawingCoord(node.GridCoord!.Value);
            node.Drawing = GraphDraw.DrawBox(node, this);
        }

        SetDrawingSizeToGridConstraints();
    }

    private static int Highest(Dictionary<int, int> positions, int level) =>
        positions.TryGetValue(level, out int value) ? value : 0;

    private IEnumerable<GraphNode> Children(GraphNode node) =>
        _edges.Where(edge => edge.From.Name == node.Name).Select(edge => edge.To);

    private void SetColumnWidth(GraphNode node)
    {
        GridCoord at = node.GridCoord!.Value;
        int[] columns = [1, (2 * GraphMetrics.BoxBorderPadding) + node.Label.Width, 1];
        int[] rows = [1, node.Label.ContentHeight + (2 * GraphMetrics.BoxBorderPadding), 1];
        for (var index = 0; index < columns.Length; index++)
        {
            int x = at.X + index;
            _columnWidth[x] = Geometry.Max(Get(_columnWidth, x), columns[index]);
        }

        for (var index = 0; index < rows.Length; index++)
        {
            int y = at.Y + index;
            _rowHeight[y] = Geometry.Max(Get(_rowHeight, y), rows[index]);
        }

        if (at.X > 0)
        {
            _columnWidth[at.X - 1] = GraphMetrics.PaddingX;
        }

        if (at.Y > 0)
        {
            _rowHeight[at.Y - 1] = Geometry.Max(Get(_rowHeight, at.Y - 1), GraphMetrics.PaddingY);
        }
    }

    private static int Get(Dictionary<int, int> values, int key) =>
        values.TryGetValue(key, out int value) ? value : 0;

    private GridCoord ReserveSpotInGrid(GraphNode node, GridCoord requested)
    {
        if (_grid.ContainsKey(requested))
        {
            GridCoord next = Horizontal
                ? new GridCoord(requested.X, requested.Y + Scale)
                : new GridCoord(requested.X + Scale, requested.Y);
            return ReserveSpotInGrid(node, next);
        }

        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 3; y++)
            {
                _grid[new GridCoord(requested.X + x, requested.Y + y)] = node;
            }
        }

        node.GridCoord = requested;
        return requested;
    }

    private void IncreaseGridSizeForPath(IReadOnlyList<GridCoord> path)
    {
        foreach (GridCoord coord in path)
        {
            _columnWidth.TryAdd(coord.X, GraphMetrics.PaddingX / 2);
            _rowHeight.TryAdd(coord.Y, GraphMetrics.PaddingY / 2);
        }
    }

    private void SetDrawingSizeToGridConstraints()
    {
        int maxX = _columnWidth.Values.Sum();
        int maxY = _rowHeight.Values.Sum();
        _drawing.IncreaseSize(maxX - 1, maxY - 1);
    }

    internal DrawingCoord GridToDrawingCoord(GridCoord coord)
    {
        var x = 0;
        var y = 0;
        for (var column = 0; column < coord.X; column++)
        {
            x += Get(_columnWidth, column);
        }

        for (var row = 0; row < coord.Y; row++)
        {
            y += Get(_rowHeight, row);
        }

        return new DrawingCoord(
            x + (Get(_columnWidth, coord.X) / 2) + _offsetX,
            y + (Get(_rowHeight, coord.Y) / 2)
        );
    }

    internal int ColumnWidth(int x) => Get(_columnWidth, x);

    internal int RowHeight(int y) => Get(_rowHeight, y);
}
