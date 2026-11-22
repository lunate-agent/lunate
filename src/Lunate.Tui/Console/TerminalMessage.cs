namespace Lunate.Tui;

/// <summary>
/// The one wrapping rule for fatal messages rendered outside a live frame: the text is sanitized,
/// wrapped to the terminal's cell width and never clipped or elided, so both the pre-session
/// renderer and the live area's scrollback show every line of a reason at any width.
/// </summary>
internal static class TerminalMessage
{
    /// <summary>
    /// The sanitized physical lines of a fatal message at <paramref name="columns"/> cells. A
    /// non-positive width or <see cref="int.MaxValue"/> means no wrap (one line), which keeps
    /// redirected output and a terminal without a reported width readable.
    /// </summary>
    internal static IReadOnlyList<string> Lines(string text, int columns) =>
        columns > 0 && columns != int.MaxValue
            ? CellText.Wrap(text, columns)
            : [CellText.Sanitize(text)];

    /// <summary>
    /// Writes each physical line of a fatal message to <paramref name="writer"/>, one line at a
    /// time, under the same wrapping rule as <see cref="Lines"/>.
    /// </summary>
    internal static void Write(TextWriter writer, string text, int columns)
    {
        ArgumentNullException.ThrowIfNull(writer);
        foreach (string line in Lines(text, columns))
        {
            writer.WriteLine(line);
        }
    }
}
