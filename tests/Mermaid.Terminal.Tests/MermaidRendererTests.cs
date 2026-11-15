using System.Reflection;

namespace Mermaid.Terminal.Tests;

public sealed class MermaidRendererTests
{
    private const string Flowchart = """
        flowchart TD
            A[One] --> B[Two]
        """;

    [Fact]
    public void A_flowchart_renders_boxed_labels_and_arrows()
    {
        MermaidDiagramResult result = Diagram(Flowchart);

        string drawing = string.Concat(result.Lines.Select(static line => line.Text));
        Assert.Contains("One", drawing);
        Assert.Contains("Two", drawing);
        Assert.Contains("▼", drawing);
        Assert.Contains("┌", drawing);
        Assert.DoesNotContain("mermaid:", drawing);
    }

    [Fact]
    public void Every_line_is_the_concatenation_of_its_runs()
    {
        MermaidDiagramResult result = Diagram(Flowchart);

        foreach (MermaidLine line in result.Lines)
        {
            Assert.Equal(line.Text, string.Concat(line.Spans.Select(static span => span.Text)));
        }

        Assert.Contains(
            result.Lines.SelectMany(static line => line.Spans),
            static span => span.Kind == MermaidSpanKind.Label && span.Text.Contains("One")
        );
    }

    [Fact]
    public void Ascii_uses_the_same_geometry_with_the_portable_glyphs()
    {
        MermaidDiagramResult unicode = Diagram(Flowchart);
        MermaidDiagramResult ascii = Diagram(Flowchart, MermaidGlyphSet.Ascii);

        Assert.Equal(unicode.Lines.Count, ascii.Lines.Count);
        for (var index = 0; index < unicode.Lines.Count; index++)
        {
            Assert.Equal(unicode.Lines[index].Text.Length, ascii.Lines[index].Text.Length);
        }

        string drawing = string.Concat(ascii.Lines.Select(static line => line.Text));
        Assert.Contains('+', drawing);
        Assert.Contains('|', drawing);
        Assert.Contains('v', drawing);
        Assert.DoesNotContain('┌', drawing);
    }

    [Fact]
    public void An_unsupported_diagram_type_falls_back_to_its_source()
    {
        MermaidFallbackResult result = Fallback("stateDiagram-v2\n    [*] --> q0");

        Assert.Equal("unsupported diagram type 'stateDiagram-v2'", result.Reason);
        Assert.Equal(["stateDiagram-v2", "    [*] --> q0"], result.Source);
    }

    [Fact]
    public void A_parse_failure_carries_the_parser_line()
    {
        MermaidFallbackResult result = Fallback(
            "sequenceDiagram\n    participant A as\n    A->>B: hi"
        );

        Assert.Equal("line 2: a participant needs a display name after 'as'", result.Reason);
    }

    [Fact]
    public void A_drawing_wider_than_the_terminal_names_both_widths()
    {
        string source = "flowchart TD\n    A[XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX] --> B[Two]";
        MermaidFallbackResult result = Assert.IsType<MermaidFallbackResult>(
            MermaidRenderer.Render(source, 42)
        );

        Assert.Contains("columns wide, this terminal has 42", result.Reason);
    }

    [Fact]
    public void A_terminal_below_the_floor_never_draws()
    {
        MermaidFallbackResult result = Assert.IsType<MermaidFallbackResult>(
            MermaidRenderer.Render(Flowchart, MermaidRenderer.MinimumWidth - 1)
        );

        Assert.Equal("this terminal has 39 columns, 40 is the minimum", result.Reason);
    }

    [Fact]
    public void An_empty_fence_has_no_diagram()
    {
        MermaidFallbackResult result = Fallback("");

        Assert.Equal("the fence has no diagram", result.Reason);
        Assert.Empty(result.Source);
    }

    [Fact]
    public void The_assembly_references_no_lunate_project()
    {
        Assembly library = typeof(MermaidRenderer).Assembly;

        Assert.DoesNotContain(
            library.GetReferencedAssemblies(),
            static reference =>
                reference.Name?.StartsWith("Lunate", StringComparison.Ordinal) == true
        );

        string project = File.ReadAllText(
            Path.Combine(
                MermaidFixture.RepositoryRoot(),
                "src",
                "Mermaid.Terminal",
                "Mermaid.Terminal.csproj"
            )
        );
        Assert.DoesNotContain(
            project.Split('\n'),
            static line =>
                line.Contains("<ProjectReference", StringComparison.Ordinal)
                && line.Contains("Lunate.", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void The_readme_ascii_example_is_the_rendered_output()
    {
        string readme = File.ReadAllText(
            Path.Combine(MermaidFixture.RepositoryRoot(), "src", "Mermaid.Terminal", "README.md")
        );
        string source = CodeBlock(readme, "mermaid");
        string expected = CodeBlock(readme, "text").TrimEnd('\n');

        string actual = GoldenTests.Render(source, MermaidGlyphSet.Ascii);
        Assert.Equal(TrimLineEnds(expected), TrimLineEnds(actual));

        // The Unicode drawing stays an image; only the ASCII glyph set keeps a text block.
        Assert.Contains("/media/mermaid/flowchart-lr.png", readme, StringComparison.Ordinal);
    }

    private static string TrimLineEnds(string text) =>
        string.Join('\n', text.Split('\n').Select(static line => line.TrimEnd(' ')));

    private static MermaidDiagramResult Diagram(
        string source,
        MermaidGlyphSet glyphs = MermaidGlyphSet.Unicode
    ) => Assert.IsType<MermaidDiagramResult>(MermaidRenderer.Render(source, int.MaxValue, glyphs));

    private static MermaidFallbackResult Fallback(string source) =>
        Assert.IsType<MermaidFallbackResult>(MermaidRenderer.Render(source, int.MaxValue));

    private static string CodeBlock(string markdown, string language)
    {
        string opener = $"```{language}\n";
        int start = markdown.IndexOf(opener, StringComparison.Ordinal);
        Assert.True(start >= 0, $"README has no {language} code block");
        start += opener.Length;
        int end = markdown.IndexOf("```", start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"README's {language} code block is not closed");
        return markdown[start..end];
    }
}
