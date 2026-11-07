using Lunate.Tui.Platform;

namespace Lunate.Tui.Tests;

/// <summary>
/// Signal exits restore the terminal and must not race the session's own terminal transitions
/// (the /quit scope exit): the serialised mode instance proves the transitions' contract, and the
/// install proves it only takes over when raw mode was actually entered on a signal-bearing
/// platform. The restore itself — the handler disposing the session's raw-mode instance — is
/// covered end to end by the pseudo-terminal tests in <c>Lunate.Coding.Tests</c>.
/// </summary>
public sealed class TerminalSignalRestoreTests
{
    [Fact]
    public void Dispose_restores_once_and_later_transitions_are_no_ops()
    {
        var transitions = new List<string>();
        using var mode = new SerializedTerminalMode(
            suspend: () =>
            {
                transitions.Add("suspend");
                return true;
            },
            resume: () =>
            {
                transitions.Add("resume");
                return true;
            },
            restore: () => transitions.Add("restore")
        );

        mode.Suspend();
        // A signal restore during a child's suspended terminal and a /quit dispose both land
        // here: restore wins and nothing re-applies anything after it.
        mode.Dispose();
        mode.Suspend();
        mode.Resume();
        mode.Dispose();

        Assert.Equal(["suspend", "restore"], transitions);
    }

    [Fact]
    public void Resume_of_a_live_session_reapplies_raw_mode()
    {
        var transitions = new List<string>();
        using var mode = new SerializedTerminalMode(
            suspend: () =>
            {
                transitions.Add("suspend");
                return true;
            },
            resume: () =>
            {
                transitions.Add("resume");
                return true;
            },
            restore: () => transitions.Add("restore")
        );

        mode.Suspend();
        mode.Resume();

        Assert.Equal(["suspend", "resume"], transitions);
    }

    [Fact]
    public void A_failed_transition_leaves_the_state_unchanged()
    {
        var transitions = new List<string>();
        var calls = 0;
        using var mode = new SerializedTerminalMode(
            suspend: () =>
            {
                transitions.Add("suspend");
                return calls++ > 0; // the first call fails, as a busy handover failure does
            },
            resume: () =>
            {
                transitions.Add("resume");
                return true;
            },
            restore: () => transitions.Add("restore")
        );

        mode.Suspend(); // fails: the state stays unsuspended
        mode.Suspend(); // retry succeeds now

        Assert.Equal(["suspend", "suspend"], transitions);
        mode.Resume();
        Assert.Equal(["suspend", "suspend", "resume"], transitions);
    }

    [Fact]
    public void A_session_that_never_entered_raw_mode_installs_nothing()
    {
        IDisposable installed = TerminalSignalRestore.Install(NoopDisposable.Instance, false);

        Assert.Same(NoopDisposable.Instance, installed);
    }

    [Fact]
    public void Windows_install_is_a_no_op()
    {
        using var terminalMode = new SerializedTerminalMode(
            suspend: () => true,
            resume: () => true,
            restore: () => { }
        );

        IDisposable installed = TerminalSignalRestore.Install(terminalMode, true);

        Assert.Same(NoopDisposable.Instance, installed);
    }

    [Fact]
    public void A_raw_session_on_a_signal_platform_installs_handlers()
    {
        // Real registrations in the test process: none of the test signals the process (the
        // default disposition would kill it), and disposing removes them again. On Windows
        // SIGHUP has no Console-side delivery, which the no-op install above pins instead.
        Assert.SkipUnless(
            !OperatingSystem.IsWindows(),
            "the install registers POSIX signals; Windows never installs."
        );

        using var terminalMode = new SerializedTerminalMode(
            suspend: () => true,
            resume: () => true,
            restore: () => { }
        );

        using IDisposable installed = TerminalSignalRestore.Install(terminalMode, false);

        Assert.NotSame(NoopDisposable.Instance, installed);
    }
}
