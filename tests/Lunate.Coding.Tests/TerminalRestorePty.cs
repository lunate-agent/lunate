using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace Lunate.Coding.Tests;

/// <summary>
/// One pty session under test: the harness allocates a pseudo-terminal, launches the built lunate
/// binary against it (an <c>/bin/sh -c "cd … && exec lunate"</c> wrapper execs itself away, so the
/// pid handed back is always lunate's), lets the test drive it from the master side, and reads the
/// pty's <c>stty -a</c> before and after — the assertion compares the whole attribute output
/// against that captured baseline, never a recited expectation (issue #636's acceptance rule).
/// </summary>
internal sealed class TerminalRestorePty : IDisposable
{
    internal const int SigTerm = 15;
    internal const int SigHup = 1;

    private readonly int _master;
    private readonly int _heldSlave;
    private readonly string _slave;
    private readonly string _sttyDeviceFlag;
    private readonly StringBuilder _scrollback = new();

    private TerminalRestorePty(
        int master,
        string slave,
        string sttyDeviceFlag,
        int pid,
        string home,
        string baselinePath,
        int heldSlave
    )
    {
        _master = master;
        _heldSlave = heldSlave;
        _slave = slave;
        _sttyDeviceFlag = sttyDeviceFlag;
        Pid = pid;
        Home = home;
        BaselinePath = baselinePath;
    }

    internal int Pid { get; }
    internal string Home { get; }
    internal string BaselinePath { get; }

    /// <summary>The fixture's own <c>stty -a</c> capture, written by the shell before the exec.</summary>
    internal async Task<string> ReadBaselineAsync(TimeSpan budget) =>
        await ReadFileAsync(BaselinePath).ConfigureAwait(false);

    /// <summary>Everything the session has painted so far, for failure messages.</summary>
    internal string Scrollback => _scrollback.ToString();

    /// <summary>The scrollback's length now, for the after-a-point marker search.</summary>
    internal int ScrollbackLength => _scrollback.Length;

    /// <summary>Waits for <paramref name="marker"/> to appear after a captured offset and returns
    /// the output seen since that offset — callers can assert on what went by, not what predates
    /// the waiting step.</summary>
    internal string WaitForScrollbackAfter(string marker, int after, TimeSpan budget)
    {
        var buffer = new byte[8192];
        DateTime deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            if (PollOne(_master, 200) > 0)
            {
                int read = native_read(_master, buffer, (nuint)buffer.Length);
                if (read > 0)
                {
                    _scrollback.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    string fresh = _scrollback.ToString()[after..];
                    if (fresh.Contains(marker, StringComparison.Ordinal))
                    {
                        return fresh;
                    }
                }
            }
        }

        throw new TimeoutException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"the pty session never showed '{marker}' after offset {after.ToString(CultureInfo.InvariantCulture)} within {budget.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s. Scrollback:{Environment.NewLine}{Scrollback}"
            )
        );
    }

    /// <summary>Launches lunate in a new pty: a 24x80 window against a scratch home.</summary>
    internal static TerminalRestorePty Launch(string binaryPath, string home)
    {
        // The workspace lives under the scratch home: the session file, history and everything
        // the test leaves behind stay tmp-scoped and per-test.
        string workspace = Path.Combine(home, "workspace");
        Directory.CreateDirectory(workspace);
        int master = posix_openpt(O_RDWR);
        if (master < 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "posix_openpt failed");
        }

        // grantpt is what makes the slave reachable: without it the device mode stays
        // root/tty-strict and every later open (the session's, stty's) is denied.
        if (grantpt(master) != 0 || unlockpt(master) != 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "grantpt/unlockpt failed");
        }

        try
        {
            // The child must not inherit the master, or the master never reaches a quiescent end.
            if (fcntl(master, F_SETFD, FD_CLOEXEC) != 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "fcntl(CLOEXEC) on the pty master failed"
                );
            }

            string slave = SlaveName(master);

            // The baseline is captured on the pty by the fixture shell itself: this process (the
            // test host) turns out to be restricted from tty ioctls on its own created ptys, so
            // the sizing and the capture both happen in the child's context, before the session
            // execs — the terminal as lunate finds it, not a value recited from memory.
            string baselinePath = Path.Combine(home, "baseline.txt");

            int pid = Spawn(binaryPath, workspace, slave, home, baselinePath);
            // Hold the slave open for the pty's lifetime: when the last slave fd closes the
            // pty is reclaimed and its window size dropped, which would fake a divergence.
            int held = open(slave, O_RDWR);
            if (held < 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "opening the pty slave failed"
                );
            }

            // The device flag is not portable: BSD/macOS stty takes -f, GNU stty takes -F, and
            // GNU reads a lowercase -f as a mode setting and refuses the output style.
            string sttyDeviceFlag = OperatingSystem.IsMacOS() ? "-f" : "-F";
            return new TerminalRestorePty(
                master,
                slave,
                sttyDeviceFlag,
                pid,
                home,
                baselinePath,
                held
            );
        }
        catch
        {
            close(master);
            throw;
        }
    }

    private static async Task<string> ReadFileAsync(string path)
    {
        // The fixture shell writes the baseline before it execs, so this only covers the file
        // landing after the input box that proves the exec happened.
        using var deadline = new System.Threading.CancellationTokenSource(WaitBudgets.SpawnWindow);
        try
        {
            while (!deadline.IsCancellationRequested && !File.Exists(path))
            {
                await Task.Delay(100, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }

        return await File.ReadAllTextAsync(path).ConfigureAwait(false);
    }

    /// <summary>Reads from the master until <paramref name="marker"/> appears or the budget runs out.</summary>
    internal void WaitForScrollback(string marker, TimeSpan budget)
    {
        var buffer = new byte[8192];
        DateTime deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            if (PollOne(_master, 200) > 0)
            {
                int read = native_read(_master, buffer, (nuint)buffer.Length);
                if (read > 0)
                {
                    _scrollback.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    if (_scrollback.ToString().Contains(marker, StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }
        }

        string sttyNow;
        try
        {
            sttyNow = ReadStty(_slave, _sttyDeviceFlag).AsSpan(0, 60).ToString();
        }
        catch
        {
            sttyNow = "unavailable";
        }

        throw new TimeoutException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"the pty session never showed '{marker}' within {budget.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s. pty: {sttyNow}. Scrollback:{Environment.NewLine}{Scrollback}"
            )
        );
    }

    /// <summary>Writes bytes to the pty, e.g. submitted input or a prompt decision.</summary>
    internal void WritePty(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(bytes.Length, native_write(_master, bytes, (nuint)bytes.Length));
    }

    internal void Kill(int signal)
    {
        if (Pid != 0)
        {
            Assert.Equal(0, kill(Pid, signal));
        }
    }

    /// <summary>Reaps the child; false when it outlived the budget (the caller may kill it hard).</summary>
    internal bool WaitForExit(TimeSpan budget)
    {
        DateTime deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            int wait = waitpid(Pid, out _, WNOHANG);
            if (wait == Pid)
            {
                return true;
            }

            if (wait < 0)
            {
                if (Marshal.GetLastWin32Error() == 10)
                {
                    return true; // ECHILD: someone already reaped it
                }

                throw new InvalidOperationException("waitpid failed");
            }

            Thread.Sleep(50);
        }

        return false;
    }

    internal string ReadStty() => ReadStty(_slave, _sttyDeviceFlag);

    /// <summary>The comparison form: stty wraps its output to the terminal, characters differ.</summary>
    internal static string Normalize(string stty) =>
        string.Join(" ", stty.Split(Whitespace(), StringSplitOptions.RemoveEmptyEntries));

    private static char[] Whitespace() => [' ', '\n', '\t', '\r'];

    private static int Spawn(
        string binaryPath,
        string workspace,
        string slave,
        string home,
        string baselinePath
    )
    {
        // `/bin/sh` execs itself away, so the pid handed back is lunate's pid and a signal below
        // reaches the session, never a wrapper.
        string inner =
            $"cd '{workspace}' "
            + $"&& stty rows 24 cols 80 "
            + $"&& stty -a > '{baselinePath}' "
            + $"&& exec '{binaryPath}'";
        string[] env = BuildEnv(home); // hoisted: the finally releases the marshalled arrays
        nint actions = 0;
        nint argv = 0;
        nint envp = 0;
        try
        {
            // The file-actions type is an opaque struct of dozens of bytes, so it lives in a
            // cleared buffer, not in an 8-byte nint that init would overflow.
            actions = Marshal.AllocHGlobal(ActionsBufferSize);
            Clear(actions);
            if (posix_spawn_file_actions_init(actions) != 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "posix_spawn_file_actions_init failed"
                );
            }

            if (
                posix_spawn_file_actions_addopen(actions, 0, slave, O_RDWR, 0) != 0
                || posix_spawn_file_actions_adddup2(actions, 0, 1) != 0
                || posix_spawn_file_actions_adddup2(actions, 0, 2) != 0
            )
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "posix_spawn file actions failed"
                );
            }

            argv = PtrArray(["sh", "-c", inner]);
            envp = PtrArray(env);
            if (posix_spawnp(out int pid, "/bin/sh", actions, 0, argv, envp) != 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"posix_spawnp failed (errno {Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture)})"
                );
            }

            return pid;
        }
        finally
        {
            if (actions != 0)
            {
                posix_spawn_file_actions_destroy(actions);
                Marshal.FreeHGlobal(actions);
            }

            FreeArray(argv, 3); // sh, -c, inner
            FreeArray(envp, env.Length);
        }
    }

    /// <summary>
    /// The file-actions struct is opaque and platform-sized; Darwin's is the larger one. The
    /// buffer is double that, and zeroed beyond it so the API sees no stale bytes.
    /// </summary>
    private const int ActionsBufferSize = 256;

    private static void Clear(nint buffer)
    {
        var zeroes = new byte[ActionsBufferSize];
        Marshal.Copy(zeroes, 0, buffer, ActionsBufferSize);
    }

    /// <summary>
    /// The child's environment: the test's own environment minus every LUNATE_* variable and the
    /// CI marker the runner may carry, plus the scratch home and a plain terminal.
    /// </summary>
    internal static string[] BuildEnv(
        string home,
        IEnumerable<KeyValuePair<string, string>>? parent = null
    )
    {
        // The parent's environment is inherited, not rebuilt: the apphost finds its runtime
        // through DOTNET_ROOT and the installer's defaults, and a short allow-list dropped those
        // on the macOS CI legs ("You must install .NET to run this application"). The scratch
        // home, a plain terminal and the absence of the runner's own switches are imposed, each
        // replacing an inherited entry rather than sitting beside it — the child takes the first
        // match for a name.
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in parent ?? ParentEnvironment())
        {
            string name = entry.Key;
            // CI is dropped with the LUNATE_* switches: the setup wizard's own trigger skips a CI
            // environment, and the harness drives an ordinary terminal where the first-run walk is
            // part of the flow. Its CI gate is covered by the session tests; here it would make the
            // driven flow differ between a laptop and a runner.
            if (
                name.StartsWith("LUNATE_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "CI", StringComparison.Ordinal)
            )
            {
                continue;
            }

            env[name] = entry.Value;
        }

        env["HOME"] = home;
        env["USERPROFILE"] = home;
        env["TERM"] = "xterm";
        env["LANG"] = "en_US.UTF-8";
        env["__CF_USER_TEXT_ENCODING"] = "0x1F5:0x0:0x0";
        return [.. env.Select(pair => $"{pair.Key}={pair.Value}")];
    }

    /// <summary>The test process's own environment, as the parent side of <see cref="BuildEnv"/>.</summary>
    private static IEnumerable<KeyValuePair<string, string>> ParentEnvironment()
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            yield return new KeyValuePair<string, string>(
                (string)entry.Key,
                entry.Value?.ToString() ?? string.Empty
            );
        }
    }

    private static nint PtrArray(string[] values)
    {
        nint array = Marshal.AllocHGlobal((nint)((values.Length + 1) * nint.Size));
        for (int index = 0; index < values.Length; index++)
        {
            Marshal.WriteIntPtr(array, index * nint.Size, StringPtr(values[index]));
        }

        Marshal.WriteIntPtr(array, values.Length * nint.Size, 0);
        return array;
    }

    private static nint StringPtr(string value)
    {
        byte[] bytes = new byte[Encoding.UTF8.GetByteCount(value) + 1];
        Encoding.UTF8.GetBytes(value, bytes);
        nint ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return ptr;
    }

    private static void FreeArray(nint array, int pointerCount)
    {
        if (array == 0)
        {
            return;
        }

        for (int index = 0; index < pointerCount; index++)
        {
            nint element = Marshal.ReadIntPtr(array, index * nint.Size);
            if (element != 0)
            {
                Marshal.FreeHGlobal(element);
            }
        }

        Marshal.FreeHGlobal(array);
    }

    private static string SlaveName(int master)
    {
        var buffer = new byte[256];
        if (ptsname_r(master, buffer, (nuint)buffer.Length) != 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "ptsname_r failed");
        }

        int length = Array.IndexOf(buffer, (byte)0);
        Assert.True(length > 0);
        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    private static int PollOne(int fd, int timeoutMilliseconds)
    {
        var fds = new PollFd[1];
        fds[0].Fd = fd;
        fds[0].Events = 1; // POLLIN
        int ready = poll(fds, (nuint)fds.Length, timeoutMilliseconds);
        if (ready < 0 && Marshal.GetLastWin32Error() == Interrupted)
        {
            return 0; // EINTR: a signal of this process's own, not the pty's answer
        }

        if (ready < 0)
        {
            int errno = Marshal.GetLastWin32Error();
            throw new Win32Exception(
                errno,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"poll on the pty master failed (errno {errno.ToString(CultureInfo.InvariantCulture)}, fd {fd.ToString(CultureInfo.InvariantCulture)})"
                )
            );
        }

        return ready;
    }

    internal static string ReadStty(string slave, string deviceFlag)
    {
        var stty = new System.Diagnostics.ProcessStartInfo("stty", $"-a {deviceFlag} {slave}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = System.Diagnostics.Process.Start(stty)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"stty could not read the pty ({slave}): {process.StandardError.ReadToEnd()}"
            );
        }

        return output;
    }

    public void Dispose()
    {
        if (Pid != 0) // pid 0 would signal the whole process group; only tests that spawned own a pid
        {
            kill(Pid, 9); // SIGKILL: the test owns this child; leave nothing behind
        }

        WaitForExit(WaitBudgets.ProcessExit);
        close(_master);
        close(_heldSlave);
    }

    private struct PollFd
    {
#pragma warning disable CS0649 // revents is written by poll, not by this code
        public int Fd;
        public short Events;
        public short Revents;
#pragma warning restore CS0649
    }

    private const int Interrupted = 4; // EINTR on both Darwin and Linux
    private const int O_RDWR = 2;
    private const int F_SETFD = 2;
    private const int FD_CLOEXEC = 1;
    private const int WNOHANG = 1;

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_openpt(int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int grantpt(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int unlockpt(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int ptsname_r(int fd, byte[] buffer, nuint length);

    [DllImport("libc", SetLastError = true)]
    private static extern int fcntl(int fd, int cmd, int arg);

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string path, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signalNumber);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    private static extern int native_read(int fd, byte[] buffer, nuint count);

    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    private static extern int native_write(int fd, byte[] buffer, nuint count);

    [DllImport("libc", SetLastError = true)]
    private static extern int poll(PollFd[] fds, nuint count, int timeoutMilliseconds);

    [DllImport("libc", SetLastError = true)]
    private static extern int waitpid(int pid, out int status, int options);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn_file_actions_init(nint actions);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn_file_actions_addopen(
        nint actions,
        int fd,
        string path,
        int flags,
        int mode
    );

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn_file_actions_adddup2(nint actions, int fd, int newFd);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn_file_actions_destroy(nint actions);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawnp(
        out int pid,
        string path,
        nint actions,
        nint attrp,
        nint argv,
        nint envp
    );
}
