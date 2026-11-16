using System.Globalization;
using System.Text;

namespace Mermaid.Terminal.Tests;

/// <summary>
/// The glyph rules the card carries: every frame glyph is one column wide in a common terminal,
/// the ASCII set draws its frame with ASCII characters only, and the two sets share one geometry.
/// Label text is the diagram's own and is not a glyph, so only frame runs are inspected.
/// </summary>
public sealed class GlyphTests
{
    public static TheoryData<string> Diagrams => GoldenTests.Diagrams;

    [Theory]
    [MemberData(nameof(Diagrams))]
    public void Every_unicode_frame_glyph_is_one_column_wide(string diagram)
    {
        MermaidFixture fixture = MermaidFixture.Load($"{diagram}.unicode.txt");
        MermaidDiagramResult result = Render(fixture.Source, MermaidGlyphSet.Unicode);

        foreach (Rune rune in FrameRunes(result))
        {
            Assert.Equal(1, DisplayWidth.RuneWidth(rune));
        }
    }

    [Theory]
    [MemberData(nameof(Diagrams))]
    public void The_ascii_frame_is_ascii_only(string diagram)
    {
        MermaidFixture fixture = MermaidFixture.Load($"{diagram}.ascii.txt");
        MermaidDiagramResult result = Render(fixture.Source, MermaidGlyphSet.Ascii);

        foreach (Rune rune in FrameRunes(result))
        {
            Assert.True(
                rune.IsAscii,
                $"{diagram} draws U+{rune.Value.ToString("X4", CultureInfo.InvariantCulture)}"
            );
        }
    }

    [Theory]
    [MemberData(nameof(Diagrams))]
    public void The_two_glyph_sets_share_one_geometry(string diagram)
    {
        MermaidFixture unicodeFixture = MermaidFixture.Load($"{diagram}.unicode.txt");
        MermaidFixture asciiFixture = MermaidFixture.Load($"{diagram}.ascii.txt");
        MermaidDiagramResult unicode = Render(unicodeFixture.Source, MermaidGlyphSet.Unicode);
        MermaidDiagramResult ascii = Render(asciiFixture.Source, MermaidGlyphSet.Ascii);

        Assert.Equal(unicode.Lines.Count, ascii.Lines.Count);
        for (var index = 0; index < unicode.Lines.Count; index++)
        {
            Assert.Equal(
                DisplayWidth.Of(unicode.Lines[index].Text),
                DisplayWidth.Of(ascii.Lines[index].Text)
            );
        }
    }

    private static IEnumerable<Rune> FrameRunes(MermaidDiagramResult result) =>
        result
            .Lines.SelectMany(static line => line.Spans)
            .Where(static span => span.Kind == MermaidSpanKind.Frame)
            .SelectMany(static span => span.Text.EnumerateRunes());

    private static MermaidDiagramResult Render(string source, MermaidGlyphSet glyphs) =>
        Assert.IsType<MermaidDiagramResult>(MermaidRenderer.Render(source, int.MaxValue, glyphs));
}
