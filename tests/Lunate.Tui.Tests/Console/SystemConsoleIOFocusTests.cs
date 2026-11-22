using Microsoft.Reactive.Testing;

namespace Lunate.Tui.Tests;

/// <summary>
/// Focus reporting is requested only while the session owns the terminal: on raw-mode entry, off
/// around a child handover, on again after the child returns and off when raw mode ends.
/// </summary>
public sealed class SystemConsoleIOFocusTests
{
    [Fact]
    public void Focus_reporting_follows_raw_mode_and_the_child_handover()
    {
        var writes = new List<string>();
        var mode = new FakeTerminalMode();
        var console = new SystemConsoleIO(
            new TestScheduler(),
            isInteractive: true,
            () => mode,
            writes.Add
        );

        using (console.EnterRawMode())
        {
            using (console.SuspendTerminal())
            {
                Assert.Equal(["off"], mode.Transitions);
            }

            Assert.Equal(["off", "on"], mode.Transitions);
        }

        Assert.Equal(["off", "on", "off"], mode.Transitions);
        Assert.Equal("\u001b[?1004h", writes[0]);
        Assert.Equal("\u001b[?1004l", writes[1]);
        Assert.Equal("\u001b[?1004h", writes[2]);
        Assert.Equal("\u001b[?1004l", writes[3]);
    }

    [Fact]
    public void Focus_starts_unknown_before_the_terminal_reports()
    {
        var console = new SystemConsoleIO(
            new TestScheduler(),
            isInteractive: true,
            () => new FakeTerminalMode(),
            _ => { }
        );

        Assert.Null(console.IsFocused);
    }

    private sealed class FakeTerminalMode : ITerminalMode
    {
        public List<string> Transitions { get; } = [];

        public void Suspend() => Transitions.Add("off");

        public void Resume() => Transitions.Add("on");

        public void Dispose() => Transitions.Add("off");
    }
}
