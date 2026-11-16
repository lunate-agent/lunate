namespace Mermaid.Terminal.Tests;

/// <summary>
/// The layout half of a flowchart: <see cref="GraphLayout"/> places one level of nodes per pitch
/// along the drawing axis, <see cref="Geometry"/> names the grid direction between two cells, and
/// <see cref="GraphLabel"/> measures a label by its widest line.
/// </summary>
public sealed class GraphLayoutTests
{
    [Fact]
    public void A_vertical_diagram_lays_out_down_the_axis()
    {
        Drawing shortChain = Layout(["flowchart TD", "A[One] --> B[Two]"]);
        Drawing longChain = Layout(["flowchart TD", "A[One] --> B[Two] --> C[Six]"]);

        Assert.True(longChain.Height > shortChain.Height);
        Assert.Equal(shortChain.Width, longChain.Width);
    }

    [Fact]
    public void A_horizontal_diagram_lays_out_along_the_axis()
    {
        Drawing shortChain = Layout(["flowchart LR", "A[One] --> B[Two]"]);
        Drawing longChain = Layout(["flowchart LR", "A[One] --> B[Two] --> C[Six]"]);

        Assert.True(longChain.Width > shortChain.Width);
        Assert.Equal(shortChain.Height, longChain.Height);
    }

    [Fact]
    public void The_grid_direction_between_two_cells_is_the_axis_that_joins_them()
    {
        var origin = new GridCoord(1, 1);

        Assert.Equal(Direction.Down, Geometry.DetermineDirection(origin, new GridCoord(1, 2)));
        Assert.Equal(Direction.Up, Geometry.DetermineDirection(origin, new GridCoord(1, 0)));
        Assert.Equal(Direction.Right, Geometry.DetermineDirection(origin, new GridCoord(2, 1)));
        Assert.Equal(Direction.Left, Geometry.DetermineDirection(origin, new GridCoord(0, 1)));
        Assert.Equal(
            Direction.LowerRight,
            Geometry.DetermineDirection(origin, new GridCoord(2, 2))
        );
        Assert.Equal(Direction.UpperLeft, Geometry.DetermineDirection(origin, new GridCoord(0, 0)));
        Assert.Equal(Direction.LowerLeft, Geometry.DetermineDirection(origin, new GridCoord(0, 2)));
        Assert.Equal(
            Direction.UpperRight,
            Geometry.DetermineDirection(origin, new GridCoord(2, 0))
        );
        Assert.Equal(Direction.Down, Direction.Up.Opposite());
        Assert.Equal(2, Geometry.CeilDiv(3, 2));
    }

    [Fact]
    public void A_label_is_measured_by_its_widest_line()
    {
        var label = new GraphLabel("one\nthree");

        Assert.Equal(["one", "three"], label.Lines);
        Assert.Equal(5, label.Width);
        Assert.Equal(3, label.ContentHeight);
    }

    [Fact]
    public void A_wide_rune_counts_as_two_columns()
    {
        Assert.Equal(2, DisplayWidth.Of("界"));
        Assert.Equal(3, DisplayWidth.Of("a界"));
    }

    private static Drawing Layout(IEnumerable<string> source)
    {
        Assert.True(
            FlowchartParser.TryParse(
                [.. source],
                "flowchart",
                out FlowchartModel? model,
                out string? failure
            ),
            failure
        );
        return new GraphLayout(model!).Draw();
    }
}
