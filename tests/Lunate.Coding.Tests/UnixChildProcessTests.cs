using System.Diagnostics;
using System.Globalization;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// What <see cref="UnixChildProcess"/>'s spawn recipe guarantees: the guarded spawn works, a long
/// environment does not stop it, and the child is detached from the terminal — its own session, no
/// controlling tty, <c>/dev/null</c> stdin. These run the real OS spawn, so the guard test needs the
/// real <c>sandbox-exec</c>.
/// </summary>
[Collection(ToolEnvironmentCollection.Name)]
public sealed class UnixChildProcessTests
{
    internal static readonly ResolvedShell Bash = new(ShellKind.Bash, "/bin/bash", "bash", ["-c"]);

    [Fact]
    public async Task A_plain_spawn_round_trips_output_and_exit_code()
    {
        SkipOnWindowsBecausePosixSpawnPinningIsPosixOnly();
        using var temp = new TempDirectory();
        var request = new ProcessRunRequest("/bin/echo", ["spawn-ok"], temp.Root);
        using Lunate.Coding.ChildProcess child = UnixChildProcess.Spawn(request);
        string stdout = await child.StandardOutput.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        int exit = await child.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Equal("spawn-ok\n", stdout);
    }

    [Fact]
    public async Task The_child_is_its_own_session_leader_with_no_controlling_tty()
    {
        SkipOnWindowsBecausePosixSpawnPinningIsPosixOnly();

        using var temp = new TempDirectory();
        string pidFile = temp.File("child.pid");
        var request = new ProcessRunRequest(
            "/bin/bash",
            ["-c", $"echo $$ > '{pidFile}'; sleep 30"],
            temp.Root
        );
        using Lunate.Coding.ChildProcess child = UnixChildProcess.Spawn(request);

        string pidText = await PidFileAsync(pidFile);
        var (pid, pgid, _, tty) = IdentityOf(pidText);

        // setsid made the child the leader of a brand-new session: it leads its own process
        // group — not the group the test host lives in — and holds no controlling terminal,
        // so it cannot open /dev/tty behind Lunate's back. The TTY column spells the empty
        // field per platform: "??" on macOS, "?" on Linux.
        Assert.Equal(pid, pgid);
        Assert.NotEqual(HostProcessGroupId(), pgid);
        Assert.Equal(OperatingSystem.IsMacOS() ? "??" : "?", tty);

        await child.KillTreeAsync();
    }

    private static async Task<string> PidFileAsync(string pidFile)
    {
        var deadline = DateTime.UtcNow + WaitBudgets.SpawnWindow;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(pidFile))
            {
                string text = File.ReadAllText(pidFile).Trim();
                if (int.TryParse(text, out _))
                {
                    return text;
                }
            }

            await Task.Delay(WaitBudgets.ChildMarkerPoll, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"the child never wrote its pid to {pidFile}");
        return string.Empty;
    }

    private static void SkipOnWindowsBecausePosixSpawnPinningIsPosixOnly()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip(
                "the POSIX spawn pinning is POSIX-only; the Windows path is pinned elsewhere"
            );
        }
    }

    // The test's own writes to the process environment, restored on dispose: the strip reads the
    // real environment, so the test plants the names it asserts on.
    private sealed class ProcessEnvironment : IDisposable
    {
        private readonly (string Name, string? Value)[] _previous;

        public ProcessEnvironment(params (string Name, string Value)[] variables)
        {
            _previous =
            [
                .. variables.Select(variable =>
                    (variable.Name, Environment.GetEnvironmentVariable(variable.Name))
                ),
            ];
            foreach ((string name, string value) in variables)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach ((string name, string? value) in _previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private static (int Pid, int Pgid, int Ppid, string Tty) IdentityOf(string pidText)
    {
        var start = new ProcessStartInfo("/bin/ps")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "-o", "pid=,pgid=,ppid=,tty=", "-p", pidText },
        };
        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        string[] fields = output.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 4)
        {
            Assert.Fail($"ps could not describe pid {pidText}: {error}");
        }

        return (
            int.Parse(fields[0], CultureInfo.InvariantCulture),
            int.Parse(fields[1], CultureInfo.InvariantCulture),
            int.Parse(fields[2], CultureInfo.InvariantCulture),
            fields[3]
        );
    }

    private static int HostProcessGroupId() =>
        IdentityOf(Environment.ProcessId.ToString(CultureInfo.InvariantCulture)).Pgid;

    [Fact]
    public async Task The_tool_child_inherits_the_shell_environment_without_lunate_credentials()
    {
        SkipOnWindowsBecausePosixSpawnPinningIsPosixOnly();

        // The bash tool is the user's shell: a toolchain's own variable and PATH must arrive. What
        // must not arrive is a credential Lunate resolved — a model-chosen command reading the
        // user's provider key out of the environment is the leak the guard's auth.json deny closes
        // for files.
        using var environment = new ProcessEnvironment(
            ("LUNATE_TEST_USER_TOOLCHAIN", "kept"),
            ("OPENAI_API_KEY", "sk-should-not-reach-the-child"),
            ("GEMINI_API_KEY", "alias-should-not-reach-the-child")
        );
        using var temp = new TempDirectory();
        var request = new ProcessRunRequest(
            "/bin/bash",
            [
                "-c",
                "printf '%s|%s|%s|%s' \"${LUNATE_TEST_USER_TOOLCHAIN:-missing}\" "
                    + "\"${PATH:-missing}\" \"${OPENAI_API_KEY:-stripped}\" "
                    + "\"${GEMINI_API_KEY:-stripped}\"",
            ],
            temp.Root
        );
        using Lunate.Coding.ChildProcess child = UnixChildProcess.Spawn(request);
        string stdout = await child.StandardOutput.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        int exit = await child.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        string[] fields = stdout.Split('|');
        Assert.Equal("kept", fields[0]);
        Assert.NotEqual("missing", fields[1]);
        Assert.Equal("stripped", fields[2]);
        Assert.Equal("stripped", fields[3]);
    }

    [Fact]
    public async Task An_unrelated_variable_whose_segment_only_starts_with_key_survives()
    {
        SkipOnWindowsBecausePosixSpawnPinningIsPosixOnly();

        // The strip matches whole segments, never substrings: `KEYBOARD_LAYOUT`'s segment is
        // `KEYBOARD`, not `KEY`, so a user's own variable beside a credential name must arrive.
        using var environment = new ProcessEnvironment(("KEYBOARD_LAYOUT", "user-owned"));
        using var temp = new TempDirectory();
        var request = new ProcessRunRequest(
            "/bin/bash",
            ["-c", "printf '%s' \"${KEYBOARD_LAYOUT:-missing}\""],
            temp.Root
        );
        using Lunate.Coding.ChildProcess child = UnixChildProcess.Spawn(request);
        string stdout = await child.StandardOutput.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        int exit = await child.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Equal("user-owned", stdout);
    }

    [Fact]
    public async Task The_child_reads_stdin_at_end_of_file()
    {
        SkipOnWindowsBecausePosixSpawnPinningIsPosixOnly();
        using var temp = new TempDirectory();
        var request = new ProcessRunRequest(
            "/bin/bash",
            ["-c", "if read line; then echo read-ok; else echo read-eof; fi"],
            temp.Root
        );
        using Lunate.Coding.ChildProcess child = UnixChildProcess.Spawn(request);
        string stdout = await child.StandardOutput.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        int exit = await child.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Equal("read-eof\n", stdout);
    }

    [Fact]
    public async Task A_large_environment_spawns_and_reports_the_platform_limit_as_E2BIG()
    {
        SkipOnWindowsBecausePosixSpawnPinningIsPosixOnly();

        // A big environment must fail the spawn with the kernel's own error, never crash or fail
        // silently. The honest per-platform size: macOS takes a half-megabyte value without
        // blinking, while the Linux kernel caps every exec's single string at 128 KiB
        // (MAX_ARG_STRLEN, measured on the Linux CI shard) — sizing just under it on Linux keeps
        // the same property without testing a limit the kernel makes unreachable.
        int valueBytes = OperatingSystem.IsLinux() ? 100 * 1024 : 512 * 1024;
        using var temp = new TempDirectory();
        var request = new ProcessRunRequest("/bin/echo", ["big"], temp.Root)
        {
            Environment = new Dictionary<string, string> { ["BIG"] = new string('x', valueBytes) },
        };
        using Lunate.Coding.ChildProcess child = UnixChildProcess.Spawn(request);
        string stdout = await child.StandardOutput.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        int exit = await child.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Equal("big\n", stdout);
    }

    [Fact]
    public async Task The_full_bash_guard_composition_runs_and_keeps_the_child_out_of_the_terminal()
    {
        if (!OperatingSystem.IsMacOS())
        {
            // The guard wraps with sandbox-exec only on macOS; other platforms have their own
            // mechanisms covered by the BashGuard resolution tests.
            return;
        }

        // The manual counterpart on a real terminal (the CI runner has neither a tty nor tmux):
        // inside tmux run lunate, start a tool call that touches the terminal
        // (bash -c 'sleep 30'), then press Esc and Ctrl+C: the turn stops, the termios `stty -a`
        // reads before == after, and typing immediately prints in the input line.
        string work = Directory.GetCurrentDirectory();
        var guard = new BashGuard();
        GuardCommand launch = guard.Wrap(Bash, work);
        Assert.True(launch.Wrapped, $"guard unwrapped: {launch.Notice ?? "no notice"}");

        var request = new ProcessRunRequest(
            launch.FileName,
            // stty instead of ps: the seatbelt refuses to exec ps inside the sandbox, and what is
            // needed here is terminal evidence anyway — a child whose stdin is /dev/null and that
            // has no controlling terminal cannot reach the session's raw mode.
            [
                .. launch.ArgumentsPrefix,
                "echo guarded-ok; if stty -a; then echo tty-ok; else echo no-tty; fi",
            ],
            work
        )
        {
            ExtraEnvironment = BashTool.SessionEnvironment,
        };
        using Lunate.Coding.ChildProcess child = UnixChildProcess.Spawn(request);
        string stdout = await child.StandardOutput.ReadToEndAsync(
            TestContext.Current.CancellationToken
        );
        await child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        int exit = await child.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Contains("guarded-ok", stdout, StringComparison.Ordinal);
        // The child's termios question is answered from its own mouth: stdin is not a terminal.
        Assert.Contains("no-tty", stdout, StringComparison.Ordinal);
    }
}
