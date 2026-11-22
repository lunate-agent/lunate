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
    public void A_signal_restore_restores_the_mode_without_waiting_on_a_stalled_write()
    {
        using var stalled = new ManualResetEventSlim();
        var writes = new List<string>();
        var mode = new FakeTerminalMode();
        var console = new SystemConsoleIO(
            new TestScheduler(),
            isInteractive: true,
            () => mode,
            text =>
            {
                writes.Add(text);
                if (text == "\u001b[?1004l")
                {
                    stalled.Wait(TimeSpan.FromSeconds(10));
                }
            }
        );

        using IDisposable rawMode = console.EnterRawMode();
        var signalRestore = Assert.IsAssignableFrom<ISignalTerminalRestore>(rawMode);

        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        signalRestore.RestoreForSignal();
        elapsed.Stop();

        Assert.Equal(["off"], mode.Transitions);
        Assert.True(
            elapsed.Elapsed < TimeSpan.FromSeconds(2),
            $"the signal restore waited {elapsed.Elapsed} for a stalled terminal write"
        );

        // The focus request is best effort: the blocked worker finishes once the terminal drains.
        stalled.Set();
        Assert.True(SpinWait.SpinUntil(() => writes.Count == 2, TimeSpan.FromSeconds(2)));
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
