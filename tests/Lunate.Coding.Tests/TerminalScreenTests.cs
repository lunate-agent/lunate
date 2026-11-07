using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The terminal-screen model's own contract: it counts physical rows the way a terminal does, so
/// the acceptance tests can trust it. A line wider than the terminal wraps onto the next row, an
/// erase clears the row it owns, and control sequences never become visible cells.
/// </summary>
public sealed class TerminalScreenTests
{
    [Fact]
    public void A_wrapped_line_occupies_one_physical_row_per_width_band()
    {
        var screen = new TerminalScreen(10);
        screen.Feed("0123456789ABCDEF"); // 16 cells: two physical rows.

        Assert.Equal(["0123456789", "ABCDEF"], screen.Lines);
    }

    [Fact]
    public void Wide_characters_wrap_by_cell_width_not_code_points()
    {
        var screen = new TerminalScreen(10);
        screen.Feed(new string('\u6f22', 6)); // 12 cells: two rows.

        Assert.Equal([new string('\u6f22', 5), "\u6f22"], screen.Lines);
    }

    [Fact]
    public void An_erase_in_line_clears_the_row_it_owns()
    {
        var screen = new TerminalScreen(10);
        screen.Feed("\r\u001b[2Kabcdef");
        screen.Feed("\r\u001b[2K");

        Assert.Empty(screen.Lines);
    }

    [Fact]
    public void Sgr_and_c0_bytes_are_not_visible_cells()
    {
        var screen = new TerminalScreen(10);
        screen.Feed("\u001b[31mred\u001b[0m\u0007");

        Assert.Equal(["red"], screen.Lines);
    }
}
