using System.Text;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// A minimal terminal emulator for acceptance tests: it interprets the live area's ANSI writes the
/// way a real terminal does, so a test can assert on physical rows and scrollback instead of raw
/// frame text. It tracks one screen grid, a scrollback of rows that scrolled off the top, and a
/// cursor; it handles carriage return, line feed, cursor movement and clear control sequences
/// (CUU/CUD/CUF/CUB/ED/EL), SGR and other CSI it ignores, and C0 it drops. Text is written with
/// cell-width-aware auto-wrap, so a line wider than the terminal occupies more than one physical
/// row exactly as <see cref="CellText.Width"/> counts it. The live area writes each line at column
/// zero (after a carriage return), so rows carry their runes in order without padding.
/// </summary>
internal sealed class TerminalScreen
{
    private readonly int _width;
    private readonly List<StringBuilder> _rows = [new()];
    private readonly List<string> _scrollback = [];
    private int _row;
    private int _col;

    public TerminalScreen(int width)
    {
        _width = Math.Max(1, width);
    }

    /// <summary>Every physical row the terminal now shows: the scrollback above the visible screen
    /// followed by the screen itself, trailing whitespace trimmed and empty rows dropped.</summary>
    public IReadOnlyList<string> Lines
    {
        get
        {
            var lines = new List<string>(_scrollback.Count + _rows.Count);
            foreach (string line in _scrollback)
            {
                if (line.TrimEnd().Length > 0)
                {
                    lines.Add(line.TrimEnd());
                }
            }

            foreach (StringBuilder row in _rows)
            {
                string line = row.ToString().TrimEnd();
                if (line.Length > 0)
                {
                    lines.Add(line);
                }
            }

            return lines;
        }
    }

    /// <summary>Interprets one ANSI write and updates the screen and scrollback.</summary>
    public void Feed(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                case '\r':
                    _col = 0;
                    continue;
                case '\n':
                    Newline();
                    continue;
                case '\u001b' when i + 1 < text.Length && text[i + 1] == '[':
                    i = Csi(text, i + 2);
                    continue;
                case '\u001b':
                    // A lone ESC or a non-CSI escape is not a cell and not a cursor move.
                    i++;
                    continue;
            }

            if (char.IsControl(c))
            {
                continue;
            }

            WriteRune(text, ref i);
        }
    }

    /// <summary>Parses a CSI sequence starting just past <c>ESC [</c>; returns the index of its
    /// final byte so the caller resumes after it.</summary>
    private int Csi(string text, int index)
    {
        var start = index;
        while (index < text.Length && text[index] is >= '0' and <= '9' or ';' or '?' or ':' or ' ')
        {
            index++;
        }

        if (index >= text.Length)
        {
            return index;
        }

        char final = text[index];
        string parameters = text[start..index];
        int amount = 1;
        if (parameters.Length > 0 && int.TryParse(parameters.TrimStart('?'), out int parsed))
        {
            amount = parsed;
        }

        switch (final)
        {
            case 'A': // Cursor Up
                _row = Math.Max(0, _row - amount);
                break;
            case 'B': // Cursor Down
                _row += amount;
                break;
            case 'C': // Cursor Forward
                _col = Math.Min(_width, _col + amount);
                break;
            case 'D': // Cursor Back
                _col = Math.Max(0, _col - amount);
                break;
            case 'K': // Erase in Line: the live area's clear is \r ESC [ 2 K (erase whole line).
                ClearRow();
                break;
            case 'J': // Erase in Display: a clear below the cursor clears every row from here down.
                ClearFromCursor();
                break;
            default:
                // SGR (m), mode switches (h/l), and anything else the live area never sends: no
                // cursor movement and no visible cells.
                break;
        }

        return index;
    }

    private void WriteRune(string text, ref int index)
    {
        Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out int consumed);
        index += consumed - 1;
        int width = CellText.Width(rune.ToString());
        if (width == 0)
        {
            return;
        }

        // A character that would pass the right margin wraps: the cursor moves to the next row's
        // first column before the character is written.
        if (_col + width > _width)
        {
            Newline();
        }

        while (_rows.Count <= _row)
        {
            _rows.Add(new StringBuilder());
        }

        _rows[_row].Append(rune);
        _col += width;
    }

    /// <summary>Moves to the start of the next row.</summary>
    private void Newline()
    {
        _row++;
        _col = 0;
        while (_rows.Count <= _row)
        {
            _rows.Add(new StringBuilder());
        }
    }

    private void ClearRow()
    {
        while (_rows.Count <= _row)
        {
            _rows.Add(new StringBuilder());
        }

        // EL2 erases the line in place; the cursor keeps its column (the live area clears as
        // \r ESC [ 2 K, so the carriage return already parked the cursor at column zero).
        _rows[_row].Clear();
    }

    private void ClearFromCursor()
    {
        while (_rows.Count <= _row)
        {
            _rows.Add(new StringBuilder());
        }

        _rows[_row].Clear();
        for (var r = _row + 1; r < _rows.Count; r++)
        {
            _rows[r].Clear();
        }
    }
}
