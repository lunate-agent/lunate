namespace Mermaid.Terminal;

/// <summary>Hands a parsed flowchart to the ported layout and returns its drawing.</summary>
internal static class FlowchartDraw
{
    internal static Drawing Draw(FlowchartModel model, MermaidGlyphSet glyphs) =>
        new GraphLayout(model) { Unicode = glyphs == MermaidGlyphSet.Unicode }.Draw();
}
