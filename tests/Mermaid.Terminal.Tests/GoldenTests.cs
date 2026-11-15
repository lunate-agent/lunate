namespace Mermaid.Terminal.Tests;

/// <summary>
/// The acceptance's golden pairs: for each diagram kind the original mermaid-ascii suite's own
/// input and expected output are rendered by this port and compared byte for byte, in both glyph
/// sets. The fixtures are copied from the upstream repository, not regenerated here.
/// </summary>
public sealed class GoldenTests
{
    public static TheoryData<string> Diagrams =>
        new()
        {
            "flowchart-td",
            "flowchart-tb-fanout",
            "flowchart-lr",
            "flowchart-lr-shared",
            "sequence",
            "sequence-note",
        };

    [Theory]
    [MemberData(nameof(Diagrams))]
    public void Unicode_output_matches_mermaid_ascii(string diagram)
    {
        MermaidFixture unicode = MermaidFixture.Load($"{diagram}.unicode.txt");
        MermaidFixture ascii = MermaidFixture.Load($"{diagram}.ascii.txt");

        Assert.Equal(unicode.Source, ascii.Source);

        Assert.Equal(unicode.Expected, Render(unicode.Source, MermaidGlyphSet.Unicode));
    }

    [Theory]
    [MemberData(nameof(Diagrams))]
    public void Ascii_output_matches_mermaid_ascii(string diagram)
    {
        MermaidFixture ascii = MermaidFixture.Load($"{diagram}.ascii.txt");

        Assert.Equal(ascii.Expected, Render(ascii.Source, MermaidGlyphSet.Ascii));
    }

    internal static string Render(string source, MermaidGlyphSet glyphs)
    {
        MermaidDiagramResult result = Assert.IsType<MermaidDiagramResult>(
            MermaidRenderer.Render(source, int.MaxValue, glyphs)
        );
        return string.Join("\n", result.Lines.Select(static line => line.Text));
    }
}
