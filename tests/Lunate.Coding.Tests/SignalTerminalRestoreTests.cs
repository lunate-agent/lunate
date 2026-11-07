namespace Lunate.Coding.Tests;

/// <summary>
/// Signal exits restore the terminal (issue #636): a session killed with SIGTERM or SIGHUP
/// mid-session — or mid-turn, with a running tool call — leaves the pty exactly as the captured
/// baseline describes. The baseline is captured on the pty by the fixture shell before the
/// session execs, and the assertion compares whole <c>stty -a</c> output against it, never a
/// recited expectation. The tui unit tests cover the serialized restore seam; these drive the
/// real platform through the built binary on a real pty.
/// </summary>
public sealed class SignalTerminalRestoreTests
{
    // The input box is the spawned binary becoming usable, the run block is a turn landing, and
    // the reap is a killed child observed gone: each inherits the budget for that condition.
    private static readonly TimeSpan BoxBudget = WaitBudgets.SpawnWindow;
    private static readonly TimeSpan TurnBudget = WaitBudgets.CrossTurn;
    private static readonly TimeSpan ExitBudget = WaitBudgets.ProcessExit;

    [PosixFact]
    public async Task SIGTERM_mid_session_restores_the_pty_baseline()
    {
        await RunSignalTest(TerminalRestorePty.SigTerm, midTurn: false);
    }

    [PosixFact]
    public async Task SIGHUP_mid_session_restores_the_pty_baseline()
    {
        await RunSignalTest(TerminalRestorePty.SigHup, midTurn: false);
    }

    [PosixFact]
    public async Task SIGTERM_mid_turn_restores_the_pty_baseline()
    {
        await RunSignalTest(TerminalRestorePty.SigTerm, midTurn: true);
    }

    [PosixFact]
    public async Task SIGHUP_mid_turn_restores_the_pty_baseline()
    {
        await RunSignalTest(TerminalRestorePty.SigHup, midTurn: true);
    }

    private static async Task RunSignalTest(int signal, bool midTurn)
    {
        string binary = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "lunate.exe" : "lunate"
        );
        Assert.SkipUnless(
            File.Exists(binary),
            $"the built lunate binary was not found at {binary}; run a full build first"
        );

        using var scratch = new TempDirectory();
        using TerminalRestorePty pty = TerminalRestorePty.Launch(binary, scratch.Root);
        pty.WaitForScrollback("Ask anything", BoxBudget);
        string baseline = await pty.ReadBaselineAsync(TurnBudget);

        if (midTurn)
        {
            SubmitSleepUnderApproval(pty);
        }

        string atKill = TerminalRestorePty.Normalize(pty.ReadStty());
        pty.Kill(signal);
        Assert.True(pty.WaitForExit(ExitBudget), "the session did not exit after the signal");

        string after = TerminalRestorePty.Normalize(pty.ReadStty());
        string captured = TerminalRestorePty.Normalize(baseline);
        Assert.True(
            string.Equals(captured, after, StringComparison.Ordinal),
            $"the pty's attributes after the signal diverge from the launch baseline.{Environment.NewLine}"
                + $"baseline: {captured}{Environment.NewLine}"
                + $"after:    {after}{Environment.NewLine}"
                + $"at kill:  {atKill}{Environment.NewLine}"
                + $"scrollback tail: {Tail(pty.Scrollback, 600)}"
        );
    }

    /// <summary>
    /// A live turn without a model: the bash-mode submission runs <c>sleep 30</c> through the
    /// tool's executor, so the signal lands while a tool child is running. The submission needs
    /// the approval panel, and the prompt only decides on a key read at least 300 ms after it
    /// rendered (spec: arming), which the delay before the <c>y</c> respects.
    /// </summary>
    private static void SubmitSleepUnderApproval(TerminalRestorePty pty)
    {
        // A fresh home opens the setup wizard once composition lands and ordinary input is
        // inert until then. The wizard writes nothing and ends silently on Esc, so the test
        // waits for it, dismisses it, waits for the input line to return, then submits.
        pty.WaitForScrollback("Select provider", BoxBudget);
        Thread.Sleep(500); // the picker's arming windows are long gone by the Esc
        pty.WritePty("\x1b");
        int closed = pty.ScrollbackLength;
        pty.WaitForScrollbackAfter("Ask anything", closed, BoxBudget);
        Thread.Sleep(500);
        pty.WritePty("!sleep 30\r");
        int before = pty.ScrollbackLength;
        Thread.Sleep(2000); // beyond the 300 ms arming window, whatever the render pace is
        pty.WritePty("y");

        string run = pty.WaitForScrollbackAfter("sleep", before, TurnBudget);
        // Honesty about the mid-turn claim: the command must still be running — no finished
        // run block header in the output since the approval.
        Assert.DoesNotContain("exit", run, StringComparison.Ordinal);
        Thread.Sleep(500);
    }

    private static string Tail(string text, int characters) =>
        text.Length <= characters ? text : text[^characters..];
}
