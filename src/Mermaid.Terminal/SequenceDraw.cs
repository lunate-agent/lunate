namespace Mermaid.Terminal;

/// <summary>
/// The sequence drawing, ported from mermaid-ascii: participant boxes on one row, lifelines below
/// them, each message on a lifeline row with its label above the arrow, and notes as a three-line
/// box spanning the participants they cover.
/// </summary>
internal static class SequenceDraw
{
    private const int ParticipantSpacing = 5;
    private const int BoxPadding = 2;
    private const int MinBoxWidth = 3;
    private const int BoxBorder = 2;
    private const int LabelLeftMargin = 2;
    private const int LabelBufferSpace = 10;

    internal static IReadOnlyList<MermaidLine> Draw(SequenceModel model, MermaidGlyphSet glyphs)
    {
        bool unicode = glyphs == MermaidGlyphSet.Unicode;
        Layout layout = Layout.Create(model);
        var lines = new List<SeqLine>
        {
            Header(layout, unicode, HeaderKind.Top),
            Header(layout, unicode, HeaderKind.Label),
            Header(layout, unicode, HeaderKind.Bottom),
        };
        foreach (SequenceStatement statement in model.Statements)
        {
            lines.Add(Lifeline(layout, unicode));
            switch (statement)
            {
                case SequenceMessage message:
                    lines.AddRange(MessageLines(layout, message, unicode));
                    break;
                case SequenceNote note:
                    lines.AddRange(NoteLines(layout, note, unicode));
                    break;
            }
        }

        lines.Add(Lifeline(layout, unicode));
        return lines.Select(line => line.ToLine()).ToList();
    }

    private sealed record Layout(
        IReadOnlyList<SequenceParticipant> Participants,
        int[] Widths,
        int[] Centers,
        int TotalWidth
    )
    {
        internal static Layout Create(SequenceModel model)
        {
            int count = model.Participants.Count;
            var widths = new int[count];
            for (var index = 0; index < count; index++)
            {
                widths[index] = Math.Max(
                    MinBoxWidth,
                    DisplayWidth.Of(model.Participants[index].Name) + BoxPadding
                );
            }

            var centers = new int[count];
            var currentX = 0;
            for (var index = 0; index < count; index++)
            {
                int boxWidth = widths[index] + BoxBorder;
                if (index == 0)
                {
                    centers[index] = boxWidth / 2;
                    currentX = boxWidth;
                }
                else
                {
                    currentX += ParticipantSpacing;
                    centers[index] = currentX + (boxWidth / 2);
                    currentX += boxWidth;
                }
            }

            int totalWidth = count == 0 ? 0 : centers[^1] + ((widths[^1] + BoxBorder) / 2);
            return new(model.Participants, widths, centers, totalWidth);
        }
    }

    private enum HeaderKind
    {
        Top,
        Label,
        Bottom,
    }

    private static SeqLine Header(Layout layout, bool unicode, HeaderKind kind)
    {
        var line = SeqLine.Blank(0);
        for (var index = 0; index < layout.Participants.Count; index++)
        {
            int boxWidth = layout.Widths[index] + BoxBorder;
            int left = layout.Centers[index] - (boxWidth / 2);
            line.EnsureWidth(left);
            int width = layout.Widths[index];
            if (kind == HeaderKind.Top)
            {
                line.Set(left, unicode ? '┌' : '+');
                for (var column = 1; column <= width; column++)
                {
                    line.Set(left + column, unicode ? '─' : '-');
                }

                line.Set(left + width + 1, unicode ? '┐' : '+');
            }
            else if (kind == HeaderKind.Bottom)
            {
                line.Set(left, unicode ? '└' : '+');
                for (var column = 1; column <= width / 2; column++)
                {
                    line.Set(left + column, unicode ? '─' : '-');
                }

                line.Set(left + (width / 2) + 1, unicode ? '┬' : '+');
                for (var column = width / 2 + 2; column <= width; column++)
                {
                    line.Set(left + column, unicode ? '─' : '-');
                }

                line.Set(left + width + 1, unicode ? '┘' : '+');
            }
            else
            {
                string name = layout.Participants[index].Name;
                int labelWidth = DisplayWidth.Of(name);
                int pad = (width - labelWidth) / 2;
                line.Set(left, unicode ? '│' : '|');
                line.PutText(left + 1 + pad, name);
                line.Set(left + width + 1, unicode ? '│' : '|');
            }
        }

        return line;
    }

    private static SeqLine Lifeline(Layout layout, bool unicode)
    {
        SeqLine line = SeqLine.Blank(layout.TotalWidth + 1);
        foreach (int center in layout.Centers)
        {
            line.Set(center, unicode ? '│' : '|');
        }

        return line;
    }

    private static IEnumerable<SeqLine> MessageLines(
        Layout layout,
        SequenceMessage message,
        bool unicode
    )
    {
        int from = layout.Centers[message.From];
        int to = layout.Centers[message.To];
        if (message.Content.Length > 0)
        {
            int start = Math.Min(from, to) + LabelLeftMargin;
            int labelWidth = DisplayWidth.Of(message.Content);
            int width = Math.Max(layout.TotalWidth, start + labelWidth) + LabelBufferSpace;
            SeqLine label = Lifeline(layout, unicode);
            label.EnsureWidth(width);
            label.PutText(start, message.Content);
            yield return label;
        }

        SeqLine arrow = Lifeline(layout, unicode);
        arrow.EnsureWidth(layout.TotalWidth + 1);
        char style = message.Dotted ? (unicode ? '┈' : '.') : (unicode ? '─' : '-');
        char head = unicode ? '►' : '>';
        char back = unicode ? '◄' : '<';
        if (from < to)
        {
            arrow.Set(from, unicode ? '├' : '+');
            for (var column = from + 1; column < to; column++)
            {
                arrow.Set(column, style);
            }

            arrow.Set(to - 1, message.Head ? head : style);
            arrow.Set(to, unicode ? '│' : '|');
        }
        else
        {
            arrow.Set(to, unicode ? '│' : '|');
            arrow.Set(to + 1, message.Head ? back : style);
            for (var column = to + 2; column < from; column++)
            {
                arrow.Set(column, style);
            }

            arrow.Set(from, unicode ? '┤' : '+');
        }

        yield return arrow;
    }

    private static IEnumerable<SeqLine> NoteLines(Layout layout, SequenceNote note, bool unicode)
    {
        string text = NoteText(note.Content);
        (int left, int right) = NoteBox(layout, note, text);
        left = Math.Max(0, left);
        yield return NoteBorder(layout, unicode, left, right, top: true);
        yield return NoteMiddle(layout, unicode, left, right, text);
        yield return NoteBorder(layout, unicode, left, right, top: false);
    }

    private static (int Left, int Right) NoteBox(Layout layout, SequenceNote note, string text)
    {
        int boxWidth = DisplayWidth.Of(text) + 4;
        int first = layout.Centers[note.Over[0]];
        int last = layout.Centers[note.Over[^1]];
        int low = Math.Min(first, last);
        int high = Math.Max(first, last);
        int left = low - 1;
        int right = high + 1;
        int extra = boxWidth - (right - left + 1);
        if (extra > 0)
        {
            left -= extra / 2;
            right += extra - (extra / 2);
        }

        return (left, right);
    }

    private static SeqLine NoteBorder(Layout layout, bool unicode, int left, int right, bool top)
    {
        SeqLine line = Lifeline(layout, unicode);
        line.EnsureWidth(right + 1);
        line.Set(left, top ? (unicode ? '┌' : '+') : (unicode ? '└' : '+'));
        for (var column = left + 1; column < right; column++)
        {
            line.Set(column, unicode ? '─' : '-');
        }

        line.Set(right, top ? (unicode ? '┐' : '+') : (unicode ? '┘' : '+'));
        return line;
    }

    private static SeqLine NoteMiddle(Layout layout, bool unicode, int left, int right, string text)
    {
        SeqLine line = Lifeline(layout, unicode);
        line.EnsureWidth(right + 1);
        for (var column = left; column <= right; column++)
        {
            line.Set(column, ' ');
        }

        line.Set(left, unicode ? '│' : '|');
        line.Set(right, unicode ? '│' : '|');
        int inner = right - left - 1;
        int start = left + 1 + Math.Max(0, (inner - DisplayWidth.Of(text)) / 2);
        line.PutText(start, text);
        return line;
    }

    private static string NoteText(string content) =>
        content
            .Replace("<br/>", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", " ", StringComparison.OrdinalIgnoreCase);

    /// <summary>One rendered line: a growable run of cells that marks the text written into it.</summary>
    private sealed class SeqLine
    {
        private readonly List<Cell> _cells;

        private SeqLine(List<Cell> cells) => _cells = cells;

        internal Cell this[int index] => _cells[index];

        internal static SeqLine Blank(int width)
        {
            var line = new SeqLine([]);
            line.EnsureWidth(width);
            return line;
        }

        internal void EnsureWidth(int width)
        {
            while (_cells.Count < width)
            {
                _cells.Add(Cell.Blank);
            }
        }

        internal void Set(int index, char glyph)
        {
            EnsureWidth(index + 1);
            _cells[index] = Cell.Frame(glyph.ToString());
        }

        internal void PutText(int index, string text)
        {
            EnsureWidth(index + text.Length);
            foreach (Cell cell in Drawing.Cells(text))
            {
                if (cell.Continuation)
                {
                    _cells[index] = Cell.ContinuationCell;
                    index++;
                    continue;
                }

                if (cell.Content.Length == 0)
                {
                    continue;
                }

                _cells[index] = Cell.Label(cell.Content);
                int width = Math.Max(1, DisplayWidth.Of(cell.Content));
                for (var offset = 1; offset < width; offset++)
                {
                    if (index + offset < _cells.Count)
                    {
                        _cells[index + offset] = Cell.ContinuationCell;
                    }
                }

                index += width;
            }
        }

        internal int TrimmedLength()
        {
            int end = _cells.Count;
            while (
                end > 0
                && !_cells[end - 1].Textual
                && !_cells[end - 1].Continuation
                && _cells[end - 1].Content == " "
            )
            {
                end--;
            }

            return end;
        }

        internal MermaidLine ToLine()
        {
            int end = TrimmedLength();
            var spans = new List<MermaidSpan>();
            var run = new System.Text.StringBuilder();
            var textual = false;
            void Flush()
            {
                if (run.Length == 0)
                {
                    return;
                }

                spans.Add(
                    new MermaidSpan(
                        run.ToString(),
                        textual ? MermaidSpanKind.Label : MermaidSpanKind.Frame
                    )
                );
                run.Clear();
            }

            for (var index = 0; index < end; index++)
            {
                Cell cell = _cells[index];
                if (cell.Continuation)
                {
                    continue;
                }

                if (cell.Textual != textual && run.Length > 0)
                {
                    Flush();
                }

                textual = cell.Textual;
                run.Append(cell.Content);
            }

            Flush();
            return new MermaidLine(spans);
        }
    }
}
