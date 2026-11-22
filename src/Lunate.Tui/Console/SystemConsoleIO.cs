using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Lunate.Tui;

internal sealed class SystemConsoleIO : IConsoleIO, ITerminalFocus
{
    internal static readonly TimeSpan ResizePollInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>The xterm request to report focus in and out as <c>CSI I</c> and <c>CSI O</c>.</summary>
    private const string FocusReportingOn = "\u001b[?1004h";

    /// <summary>The matching request to stop focus reporting.</summary>
    private const string FocusReportingOff = "\u001b[?1004l";

    /// <summary>0 is unknown, 1 focused, -1 unfocused; written by the key reader's focus callback.</summary>
    private int _focusState;

    private readonly IScheduler _scheduler;
    private readonly Func<ITerminalMode> _enterRawMode;
    private readonly Action<string> _write;
    private readonly Channel<KeyEvent> _keys = Channel.CreateUnbounded<KeyEvent>(
        new UnboundedChannelOptions { SingleReader = true }
    );

    /// <summary>Held by the pump across one readiness wait and its read, so a suspend can bar reads.</summary>
    private readonly SemaphoreSlim _readGate = new(1, 1);

    /// <summary>Set while the terminal belongs to a child; the pump parks on it instead of reading.</summary>
    private readonly ManualResetEventSlim _resumed = new(initialState: true);

    private volatile bool _suspended;
    private KeyReader? _reader;
    private ITerminalMode? _terminalMode;

    public SystemConsoleIO(IScheduler scheduler)
        : this(
            scheduler,
            ComputeIsInteractive(
                Console.IsInputRedirected,
                Console.IsOutputRedirected,
                Environment.GetEnvironmentVariable("TERM"),
                OperatingSystem.IsWindows(),
                OperatingSystem.IsWindows() && WindowsConsoleMode.IsConsoleAttached()
            ),
            EnterPlatformRawMode,
            WriteToConsole
        ) { }

    /// <summary>The test seam: a scripted terminal mode and write sink, with interactivity pinned.</summary>
    internal SystemConsoleIO(
        IScheduler scheduler,
        bool isInteractive,
        Func<ITerminalMode> enterRawMode,
        Action<string> write
    )
    {
        _scheduler = scheduler;
        IsInteractive = isInteractive;
        _enterRawMode = enterRawMode;
        _write = write;
    }

    public bool IsInteractive { get; }

    /// <summary>
    /// The last focus report, or null until the terminal sends one. A terminal without focus
    /// reporting keeps this null, so no notification can mistake silence for an unfocused window.
    /// </summary>
    public bool? IsFocused =>
        Volatile.Read(ref _focusState) switch
        {
            1 => true,
            -1 => false,
            _ => null,
        };

    public ConsoleSize Size => new(Console.WindowWidth, Console.WindowHeight);

    public IObservable<ConsoleSize> Resized => PollSize(() => Size, ResizePollInterval, _scheduler);

    public void Write(string text) => _write(text);

    private static void WriteToConsole(string text)
    {
        Console.Out.Write(text);
        Console.Out.Flush();
    }

    private static ITerminalMode EnterPlatformRawMode() =>
        OperatingSystem.IsWindows() ? WindowsConsoleMode.Enter() : UnixRawMode.Enter();

    public async IAsyncEnumerable<KeyEvent> ReadKeysAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        using var reader = new KeyReader(
            _scheduler,
            key => _keys.Writer.TryWrite(key),
            onFocus: SetFocused
        );
        _reader = reader;
        var pump = PumpBytesAsync(reader, cancellationToken);
        try
        {
            await foreach (var key in _keys.Reader.ReadAllAsync(cancellationToken))
            {
                yield return key;
            }
        }
        finally
        {
            _reader = null;
            await pump.ConfigureAwait(false);
        }
    }

    public IDisposable EnterRawMode()
    {
        if (!IsInteractive)
        {
            return NoopDisposable.Instance;
        }

        ITerminalMode mode = _enterRawMode();
        try
        {
            WriteFocusReporting(enabled: true);
        }
        catch
        {
            mode.Dispose();
            throw;
        }

        _terminalMode = mode;
        return new RawModeScope(this, mode);
    }

    /// <summary>
    /// Quiesces the pump, returns the terminal to the mode it had before raw mode, and drops a
    /// partial sequence the decoder still held. The key channel stays open: one read loop, one
    /// lifetime, with only its blocking read set aside.
    /// </summary>
    public IDisposable SuspendTerminal()
    {
        if (!IsInteractive || _terminalMode is not { } mode)
        {
            return NoopDisposable.Instance;
        }

        // Taking the gate waits for at most one in-flight read; setting the flag under it
        // guarantees no later read starts.
        _resumed.Reset();
        _readGate.Wait();
        try
        {
            _suspended = true;
        }
        finally
        {
            _readGate.Release();
        }

        WriteFocusReporting(enabled: false);
        ResetFocus();
        try
        {
            mode.Suspend();
        }
        catch
        {
            ResumeTerminal();
            throw;
        }

        _reader?.FlushPartial();
        return new Suspension(this);
    }

    private void ResumeTerminal()
    {
        _reader?.FlushPartial();
        _terminalMode?.Resume();
        WriteFocusReporting(enabled: true);
        _readGate.Wait();
        try
        {
            _suspended = false;
        }
        finally
        {
            _readGate.Release();
        }

        _resumed.Set();
    }

    private void SetFocused(bool focused) => Volatile.Write(ref _focusState, focused ? 1 : -1);

    private void ResetFocus() => Volatile.Write(ref _focusState, 0);

    /// <summary>
    /// Requests or stops focus reporting. The bytes are best effort: a terminal whose output
    /// stream fails must not turn an optional presentation request into a failed session.
    /// </summary>
    private void WriteFocusReporting(bool enabled)
    {
        try
        {
            Write(enabled ? FocusReportingOn : FocusReportingOff);
        }
        catch (Exception exception)
            when (exception is IOException or ObjectDisposedException or InvalidOperationException)
        { }
    }

    private sealed class RawModeScope(SystemConsoleIO console, ITerminalMode mode)
        : IDisposable,
            ISignalTerminalRestore
    {
        /// <summary>
        /// How long a signal exit waits for the best-effort focus-report-off request. The restore
        /// itself has already happened; this only bounds the optional terminal write.
        /// </summary>
        private static readonly TimeSpan SignalFocusWriteBudget = TimeSpan.FromMilliseconds(100);

        private bool _left;

        public void Dispose()
        {
            if (!TryLeave())
            {
                return;
            }

            console.WriteFocusReporting(enabled: false);
            RestoreMode();
        }

        /// <summary>
        /// The signal path: restore the mode first, then ask the terminal to stop focus reporting
        /// without ever gating the exit on that write. A signal can arrive while the UI thread
        /// holds the console writer blocked on a full output buffer, so a synchronous request
        /// would deadlock the process instead of letting the signal end it.
        /// </summary>
        public void RestoreForSignal()
        {
            if (!TryLeave())
            {
                return;
            }

            RestoreMode();
            Task write = Task.Run(() =>
            {
                try
                {
                    console.WriteFocusReporting(enabled: false);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // Best effort: the process is exiting on the signal, and the terminal mode
                    // restore above is the part that must survive.
                }
            });
            try
            {
                write.Wait(SignalFocusWriteBudget);
            }
            catch (AggregateException)
            {
                // The worker already swallowed its own failure; a wait error cannot delay exit.
            }
        }

        private bool TryLeave()
        {
            if (_left)
            {
                return false;
            }

            _left = true;
            return true;
        }

        private void RestoreMode()
        {
            console.ResetFocus();
            mode.Dispose();
            if (ReferenceEquals(console._terminalMode, mode))
            {
                console._terminalMode = null;
            }
        }
    }

    private sealed class Suspension(SystemConsoleIO console) : IDisposable
    {
        private bool _resumed;

        public void Dispose()
        {
            if (_resumed)
            {
                return;
            }

            _resumed = true;
            console.ResumeTerminal();
        }
    }

    /// <summary>Enters the alternate screen; disposing the scope restores the primary screen.</summary>
    public IDisposable EnterAlternateScreen() => AlternateScreen.Enter(Write);

    internal static bool ComputeIsInteractive(
        bool inputRedirected,
        bool outputRedirected,
        string? term,
        bool isWindows,
        bool windowsConsoleAttached
    ) =>
        !inputRedirected
        && !outputRedirected
        && !string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase)
        && (!isWindows || windowsConsoleAttached);

    internal static IObservable<ConsoleSize> PollSize(
        Func<ConsoleSize> read,
        TimeSpan interval,
        IScheduler scheduler
    ) => Observable.Interval(interval, scheduler).Select(_ => read()).DistinctUntilChanged();

    private const int PollIntervalMs = 200;

    /// <summary>
    /// Reads stdin into the key reader, one readiness-checked read at a time, and parks while the
    /// terminal belongs to a child. Raw descriptor reads, never the runtime's console stream: on
    /// Unix the console stack re-applies its own terminal mode around console operations, which
    /// silently overwrites the session's raw mode after it is entered (Enter then arrives as LF and
    /// can never submit).
    /// </summary>
    private async Task PumpBytesAsync(KeyReader reader, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var input = Console.OpenStandardInput();
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!AwaitInput())
                    {
                        continue;
                    }

                    await _readGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (_suspended || cancellationToken.IsCancellationRequested)
                        {
                            continue;
                        }

                        int read = await input
                            .ReadAsync(buffer, cancellationToken)
                            .ConfigureAwait(false);
                        if (read == 0)
                        {
                            break;
                        }

                        reader.Feed(buffer.AsSpan(0, read));
                    }
                    finally
                    {
                        _readGate.Release();
                    }
                }
            }
            else
            {
                await Task.Run(
                        () =>
                        {
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                if (!AwaitInput())
                                {
                                    continue;
                                }

                                _readGate.Wait();
                                try
                                {
                                    if (_suspended || cancellationToken.IsCancellationRequested)
                                    {
                                        continue;
                                    }

                                    nint read = PosixRead(0, buffer, (nuint)buffer.Length);
                                    if (read <= 0)
                                    {
                                        return;
                                    }
                                    else
                                    {
                                        reader.Feed(buffer.AsSpan(0, (int)read));
                                    }
                                }
                                finally
                                {
                                    _readGate.Release();
                                }
                            }
                        },
                        CancellationToken.None
                    )
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _keys.Writer.TryComplete();
        }
    }

    /// <summary>True when stdin has bytes; parks while suspended and reports false on a timeout.</summary>
    private bool AwaitInput()
    {
        if (_suspended)
        {
            _resumed.Wait(PollIntervalMs);
            return false;
        }

        return ConsoleInputReadiness.IsReady(PollIntervalMs);
    }

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    private static extern nint PosixRead(int fd, byte[] buffer, nuint count);
}
