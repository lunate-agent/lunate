using System.Text;

namespace Mermaid.Terminal;

/// <summary>A terminal column/row coordinate in the layout grid or on the drawing.</summary>
internal readonly record struct GridCoord(int X, int Y)
{
    public static GridCoord operator +(GridCoord left, Direction right) =>
        new(left.X + right.X, left.Y + right.Y);
}

/// <summary>A coordinate on the character grid that is finally printed.</summary>
internal readonly record struct DrawingCoord(int X, int Y);

/// <summary>The eight grid directions plus the collapsed middle, coded as unit offsets.</summary>
internal readonly record struct Direction(int X, int Y)
{
    public static readonly Direction Up = new(1, 0);
    public static readonly Direction Down = new(1, 2);
    public static readonly Direction Left = new(0, 1);
    public static readonly Direction Right = new(2, 1);
    public static readonly Direction UpperRight = new(2, 0);
    public static readonly Direction UpperLeft = new(0, 0);
    public static readonly Direction LowerRight = new(2, 2);
    public static readonly Direction LowerLeft = new(0, 2);
    public static readonly Direction Middle = new(1, 1);

    public Direction Opposite() =>
        this == Up ? Down
        : this == Down ? Up
        : this == Left ? Right
        : this == Right ? Left
        : this == UpperRight ? LowerLeft
        : this == UpperLeft ? LowerRight
        : this == LowerRight ? UpperLeft
        : this == LowerLeft ? UpperRight
        : Middle;
}

internal enum EdgeStroke
{
    Solid,
    Dotted,
    Thick,
}

internal enum EdgeHead
{
    Arrow,
    Circle,
    Cross,
    None,
}

internal static class Geometry
{
    internal static Direction DetermineDirection(GridCoord from, GridCoord to) =>
        from.X == to.X
            ? from.Y < to.Y
                ? Direction.Down
                : Direction.Up
            : from.Y == to.Y
                ? from.X < to.X
                    ? Direction.Right
                    : Direction.Left
                : from.X < to.X
                    ? from.Y < to.Y
                        ? Direction.LowerRight
                        : Direction.UpperRight
                    : from.Y < to.Y
                        ? Direction.LowerLeft
                        : Direction.UpperLeft;

    internal static (int Dx, int Dy) Step(Direction direction) =>
        direction == Direction.Up ? (0, -1)
        : direction == Direction.Down ? (0, 1)
        : direction == Direction.Left ? (-1, 0)
        : direction == Direction.Right ? (1, 0)
        : (0, 0);

    internal static int Min(int x, int y) => x < y ? x : y;

    internal static int Max(int x, int y) => x > y ? x : y;

    internal static int Abs(int value) => value < 0 ? -value : value;

    internal static int CeilDiv(int x, int y) => x % y == 0 ? x / y : (x / y) + 1;
}

internal static class DisplayWidth
{
    /// <summary>The columns a rune occupies: 0 for a combining mark, 2 for a wide East Asian cell.</summary>
    internal static int RuneWidth(Rune rune)
    {
        if (Rune.IsControl(rune) || IsZeroWidth(rune.Value))
        {
            return 0;
        }

        int value = rune.Value;
        return IsWide(value) ? 2 : 1;
    }

    internal static int Of(string text)
    {
        var width = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            width += RuneWidth(rune);
        }

        return width;
    }

    private static bool IsZeroWidth(int value) =>
        value
            is >= 0x0300
                and <= 0x036F
                or >= 0x1AB0
                and <= 0x1AFF
                or >= 0x1DC0
                and <= 0x1DFF
                or >= 0x20D0
                and <= 0x20FF
                or >= 0xFE00
                and <= 0xFE0F
                or >= 0xFE20
                and <= 0xFE2F
                or 0x200B
                or 0x200C
                or 0x200D
                or 0xFEFF;

    private static bool IsWide(int value) =>
        value
            is >= 0x1100
                and <= 0x115F
                or 0x2329
                or 0x232A
                or >= 0x2E80
                and <= 0x303E
                or >= 0x3041
                and <= 0x33FF
                or >= 0x3400
                and <= 0x4DBF
                or >= 0x4E00
                and <= 0x9FFF
                or >= 0xA000
                and <= 0xA4CF
                or >= 0xA960
                and <= 0xA97F
                or >= 0xAC00
                and <= 0xD7A3
                or >= 0xF900
                and <= 0xFAFF
                or >= 0xFE10
                and <= 0xFE19
                or >= 0xFE30
                and <= 0xFE6F
                or >= 0xFF00
                and <= 0xFF60
                or >= 0xFFE0
                and <= 0xFFE6
                or >= 0x1F300
                and <= 0x1F64F
                or >= 0x1F900
                and <= 0x1F9FF
                or >= 0x20000
                and <= 0x3FFFD;
}
