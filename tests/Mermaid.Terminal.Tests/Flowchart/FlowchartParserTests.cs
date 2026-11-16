namespace Mermaid.Terminal.Tests;

/// <summary>
/// The flowchart half of the grammar: what <see cref="FlowchartParser"/> accepts, the model it
/// keeps (nodes in first-appearance order, edges in statement order, one layer per node), and the
/// failures it reports so the fence falls back with a readable reason.
/// </summary>
public sealed class FlowchartParserTests
{
    [Fact]
    public void The_parser_keeps_nodes_edges_and_layers()
    {
        string[] source =
        [
            "flowchart TD",
            "A[One] --> B[Two]",
            "A -->|pick| C[Three]",
            "B -.-> D[Four];",
        ];

        Assert.True(
            FlowchartParser.TryParse(source, "flowchart", out var model, out string? failure)
        );
        Assert.Null(failure);
        Assert.NotNull(model);

        Assert.False(model!.Horizontal);
        Assert.Equal(["A", "B", "C", "D"], model.Nodes.Select(static node => node.Id));
        Assert.Equal(
            [
                ["One"],
                ["Two"],
                ["Three"],
                ["Four"],
            ],
            model.Nodes.Select(static node => node.LabelLines)
        );
        Assert.Equal([0, 1, 1, 2], model.Layers);

        Assert.Collection(
            model.Edges,
            edge => Assert.Equal(("A", "B", null, false, true), Details(edge)),
            edge => Assert.Equal(("A", "C", "pick", false, true), Details(edge)),
            edge => Assert.Equal(("B", "D", null, true, true), Details(edge))
        );
    }

    [Fact]
    public void A_quoted_label_keeps_its_line_breaks_and_the_direction_sets_the_axis()
    {
        string[] source = ["flowchart LR", "A[\"One<br/>two\"] --> B"];

        Assert.True(
            FlowchartParser.TryParse(source, "flowchart", out var model, out string? failure)
        );
        Assert.Null(failure);
        Assert.True(model!.Horizontal);
        Assert.Equal(
            [
                ["One", "two"],
                ["B"],
            ],
            model.Nodes.Select(static node => node.LabelLines)
        );
    }

    [Fact]
    public void A_cycle_is_a_parse_failure()
    {
        string[] source = ["flowchart TD", "A --> B", "B --> A"];

        Assert.False(FlowchartParser.TryParse(source, "flowchart", out _, out string? failure));
        Assert.Equal("the diagram has a cycle", failure);
    }

    [Fact]
    public void An_unsupported_statement_names_its_line()
    {
        string[] source = ["flowchart TD", "subgraph one"];

        Assert.False(FlowchartParser.TryParse(source, "flowchart", out _, out string? failure));
        Assert.Equal("line 2: unsupported statement 'subgraph'", failure);
    }

    [Fact]
    public void A_direction_is_required()
    {
        string[] source = ["flowchart"];

        Assert.False(FlowchartParser.TryParse(source, "flowchart", out _, out string? failure));
        Assert.Equal("line 1: the diagram needs a direction (TD, TB or LR)", failure);
    }

    private static (string From, string To, string? Label, bool Dashed, bool Arrowhead) Details(
        FlowchartEdge edge
    ) => (edge.From, edge.To, edge.Label, edge.Dashed, edge.Arrowhead);
}
