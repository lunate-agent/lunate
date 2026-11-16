using System.Text;

namespace Mermaid.Terminal;

/// <summary>One character cell of a drawing, tagged so label text can be painted separately.</summary>
internal readonly record struct Cell(string Content, bool Textual, bool Continuation)
{
    internal static readonly Cell Blank = new(" ", false, false);

    internal static Cell Frame(string content) => new(content, false, false);

    internal static Cell Label(string content) => new(content, true, false);

    internal static readonly Cell ContinuationCell = new(string.Empty, false, true);
}

/// <summary>
/// The character grid a diagram is drawn on, indexed column first like mermaid-ascii's drawing.
/// Cell content is a string because a combining mark belongs to the cell it modifies. Coordinates
/// outside the allocated grid are grown on demand; a write of a blank leaves the cell untouched.
/// </summary>
internal sealed class Drawing
{
    private readonly List<Cell[]> _columns;

    internal Drawing(int maxX, int maxY)
    {
        _columns = new List<Cell[]>(maxX + 1);
        for (var x = 0; x <= maxX; x++)
        {
            var column = new Cell[maxY + 1];
            Array.Fill(column, Cell.Blank);
            _columns.Add(column);
        }
    }

    internal int MaxX => _columns.Count - 1;

    internal int MaxY => _columns[0].Length - 1;

    internal int Width => MaxX + 1;

    internal int Height => MaxY + 1;

    internal Cell this[int x, int y]
    {
        get => _columns[x][y];
        set => _columns[x][y] = value;
    }

    internal Drawing BlankCopy() => new(MaxX, MaxY);

    internal void IncreaseSize(int x, int y)
    {
        int maxX = Geometry.Max(x, MaxX);
        int maxY = Geometry.Max(y, MaxY);
        var grown = new Drawing(maxX, maxY);
        for (var column = 0; column <= MaxX; column++)
        {
            for (var row = 0; row <= MaxY; row++)
            {
                grown[column, row] = this[column, row];
            }
        }

        _columns.Clear();
        _columns.AddRange(grown._columns);
    }

    /// <summary>Write text at a cell, growing the grid; every written cell is label text.</summary>
    internal void DrawText(int x, int y, string text)
    {
        int width = DisplayWidth.Of(text);
        IncreaseSize(x + width, y);
        DrawTextCells(this, new DrawingCoord(x, y), text);
    }

    /// <summary>Write text into another drawing, keeping combining marks on their base cell.</summary>
    internal static void DrawTextCells(Drawing drawing, DrawingCoord start, string text)
    {
        int textX = start.X;
        int lastCellX = start.X;
        foreach (Cell cell in Cells(text))
        {
            if (cell.Continuation)
            {
                drawing[textX, start.Y] = Cell.ContinuationCell;
                textX++;
                continue;
            }

            if (DisplayWidth.Of(cell.Content) == 0)
            {
                if (lastCellX >= 0 && lastCellX <= drawing.MaxX)
                {
                    Cell previous = drawing[lastCellX, start.Y];
                    drawing[lastCellX, start.Y] = new Cell(
                        previous.Content + cell.Content,
                        true,
                        false
                    );
                }

                continue;
            }

            if (textX < 0 || textX > drawing.MaxX || start.Y < 0 || start.Y > drawing.MaxY)
            {
                break;
            }

            drawing[textX, start.Y] = Cell.Label(cell.Content);
            int cellWidth = DisplayWidth.Of(cell.Content);
            for (var offset = 1; offset < cellWidth && textX + offset <= drawing.MaxX; offset++)
            {
                drawing[textX + offset, start.Y] = Cell.ContinuationCell;
            }

            lastCellX = textX;
            textX += cellWidth;
        }
    }

    /// <summary>Split text into cells: a wide rune owns its second cell, a mark owns none.</summary>
    internal static List<Cell> Cells(string text)
    {
        var cells = new List<Cell>(text.Length);
        var pending = new StringBuilder();
        foreach (Rune rune in text.EnumerateRunes())
        {
            int width = DisplayWidth.RuneWidth(rune);
            if (width == 0)
            {
                if (cells.Count > 0)
                {
                    int index = cells.Count - 1;
                    while (index >= 0 && cells[index].Continuation)
                    {
                        index--;
                    }

                    if (index >= 0 && cells[index].Textual)
                    {
                        cells[index] = new Cell(
                            cells[index].Content + rune.ToString(),
                            true,
                            false
                        );
                        continue;
                    }
                }

                pending.Append(rune.ToString());
                continue;
            }

            string content = pending.ToString() + rune.ToString();
            pending.Clear();
            bool textual = rune.Value != ' ' && !IsStructural(rune);
            cells.Add(new Cell(content, textual, false));
            for (var offset = 1; offset < width; offset++)
            {
                cells.Add(Cell.ContinuationCell);
            }
        }

        if (pending.Length > 0)
        {
            cells.Add(Cell.Label(pending.ToString()));
        }

        return cells;
    }

    private static bool IsStructural(Rune rune) =>
        rune.Value
            is >= 0x2500
                and <= 0x257F
                or '►'
                or '◄'
                or '×'
                or '+'
                or '-'
                or '|'
                or '.'
                or '<'
                or '>'
                or '#'
                or '^'
                or 'v';

    /// <summary>
    /// The grid as lines of runs: label runs are their own span, frame runs (and the blanks
    /// between them) another. Every line is padded to the drawing's full width.
    /// </summary>
    internal List<MermaidLine> ToLines()
    {
        var lines = new List<MermaidLine>(Height);
        for (var y = 0; y <= MaxY; y++)
        {
            var spans = new List<MermaidSpan>();
            var run = new StringBuilder();
            var runTextual = false;
            void Flush()
            {
                if (run.Length == 0)
                {
                    return;
                }

                spans.Add(
                    new MermaidSpan(
                        run.ToString(),
                        runTextual ? MermaidSpanKind.Label : MermaidSpanKind.Frame
                    )
                );
                run.Clear();
            }

            for (var x = 0; x <= MaxX; x++)
            {
                Cell cell = this[x, y];
                if (cell.Continuation)
                {
                    continue;
                }

                if (cell.Textual != runTextual && run.Length > 0)
                {
                    Flush();
                }

                runTextual = cell.Textual;
                run.Append(cell.Content);
            }

            Flush();
            lines.Add(new MermaidLine(spans));
        }

        return lines;
    }
}
