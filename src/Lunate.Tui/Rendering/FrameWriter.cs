using System.Globalization;
using System.Text;

namespace Lunate.Tui;

internal sealed class FrameWriter
{
    private readonly IConsoleIO _console;
    private int _height;
    private int _cursorRow;

    public FrameWriter(IConsoleIO console) => _console = console;

    public void Paint(RenderedFrame frame)
    {
        var builder = new StringBuilder();

        // The cursor rests on the frame's cursor row (the input line), not below the frame:
        // moving up by the frame height would overshoot by (height - cursorRow) rows, drifting
        // the area upward and leaving unerased ghost rows behind on every repaint.
        if (_cursorRow > 0)
        {
            builder.Append(Escape(_cursorRow)).Append('A');
        }

        int width = Math.Max(1, _console.Size.Columns);
        int[] lineRows = [.. frame.Lines.Select(line => PhysicalRows(line, width))];
        int frameRows = lineRows.Sum();
        int rows = Math.Max(_height, frameRows);
        for (var i = 0; i < rows; i++)
        {
            builder.Append('\r').Append("\u001b[2K");
            if (i < frame.Lines.Count)
            {
                builder.Append(frame.Lines[i]);
            }

            builder.Append('\n');
            // A wrapped logical line occupies extra physical rows: emit one newline per
            // physical row so the next logical line starts below the whole wrapped line and
            // the cursor bookkeeping below matches what the terminal actually did.
            int extra = i < lineRows.Length ? lineRows[i] - 1 : 0;
            for (var wrapped = 0; wrapped < extra; wrapped++)
            {
                builder.Append('\n');
            }
        }

        _height = rows;

        int logicalCursor = Math.Clamp(frame.CursorRow, 0, Math.Max(0, frame.Lines.Count - 1));
        int cursorRow = 0;
        for (var i = 0; i < logicalCursor; i++)
        {
            cursorRow += lineRows[i];
        }

        if (frame.Lines.Count > 0 && frame.CursorRow >= 0)
        {
            cursorRow += Math.Min(Math.Max(0, frame.CursorColumn), Math.Max(0, width - 1)) / width;
        }

        cursorRow = Math.Clamp(cursorRow, 0, Math.Max(0, rows - 1));
        int up = rows - cursorRow;
        if (up > 0)
        {
            builder.Append(Escape(up)).Append('A');
        }

        builder.Append('\r');
        if (frame.CursorColumn > 0)
        {
            builder.Append(Escape(frame.CursorColumn)).Append('C');
        }

        _cursorRow = cursorRow;
        _console.Write(builder.ToString());
    }

    public void Clear()
    {
        if (_height == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        if (_cursorRow > 0)
        {
            builder.Append(Escape(_cursorRow)).Append('A');
        }

        for (var i = 0; i < _height; i++)
        {
            builder.Append('\r').Append("\u001b[2K");
            if (i < _height - 1)
            {
                builder.Append('\n');
            }
        }

        if (_height > 1)
        {
            builder.Append(Escape(_height - 1)).Append('A');
        }

        _cursorRow = 0;
        _height = 0;
        _console.Write(builder.ToString());
    }

    /// <summary>
    /// Forgets the painted region without erasing it: the buffer those rows were painted on is
    /// being discarded wholesale (the alternate screen the full-screen mode leaves), so erasing on
    /// the current buffer would damage whatever stands there now.
    /// </summary>
    public void Forget()
    {
        _cursorRow = 0;
        _height = 0;
    }

    /// <summary>The physical rows a logical frame line occupies at the terminal width.</summary>
    private static int PhysicalRows(string line, int width)
    {
        int cells = CellText.Width(AnsiFree(line));
        return Math.Max(1, (cells + width - 1) / width);
    }

    /// <summary>Removes SGR/CSI sequences before measuring a frame line; the cursor and erase
    /// bytes are not cells and must not turn a 40-column line into a 60-column wrap.</summary>
    private static string AnsiFree(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\u001b')
            {
                builder.Append(text[i]);
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == '[')
            {
                i += 2;
                while (i < text.Length && !char.IsLetter(text[i]))
                {
                    i++;
                }

                continue;
            }

            i++;
        }

        return builder.ToString();
    }

    private static string Escape(int value) =>
        "\u001b[" + value.ToString(CultureInfo.InvariantCulture);
}
