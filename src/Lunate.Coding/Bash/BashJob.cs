using System.Globalization;
using System.Text;

namespace Lunate.Coding;

/// <summary>
/// One background command: the detached process, its two bounded output pumps, its timeout and the
/// state <c>bash_output</c> and <c>bash_kill</c> read. A natural exit, a timeout and a kill all end
/// in the same completion, so the process is always reaped and the final output is drainable.
/// </summary>
internal sealed partial class BashJob
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(5);

    internal static readonly string TruncationMarker = string.Create(
        CultureInfo.InvariantCulture,
        $"... [unread output exceeded {BashTool.CaptureCap} bytes; the oldest output was discarded]"
    );

    private readonly IExecutionProcess _process;
    private readonly Func<Task>? _afterKill;
    private readonly TimeSpan _timeout;
    private readonly BashJobCapture _stdout = new(BashTool.CaptureCap);
    private readonly BashJobCapture _stderr = new(BashTool.CaptureCap);
    private readonly CancellationTokenSource _timerCancellation = new();
    private readonly TaskCompletionSource _finished = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly Lock _gate = new();
    private bool _processExited;
    private int? _exitCode;
    private bool _timedOut;
    private bool _killed;

    private BashJob(string id, string command, StartedBackgroundProcess started, TimeSpan timeout)
    {
        Id = id;
        Command = command;
        _process = started.Process;
        _afterKill = started.AfterKill;
        _timeout = timeout;
    }

    public string Id { get; }

    public string Command { get; }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return !_processExited && !_timedOut && !_killed;
            }
        }
    }

    public static BashJob Start(
        string id,
        string command,
        StartedBackgroundProcess started,
        TimeSpan timeout
    )
    {
        var job = new BashJob(id, command, started, timeout);
        _ = job.RunAsync();
        if (timeout > TimeSpan.Zero)
        {
            _ = job.WatchTimeoutAsync();
        }

        return job;
    }

    public async Task<BashJobRead> ReadAsync()
    {
        await SettleAsync().ConfigureAwait(false);
        (string stdout, bool stdoutDropped) = _stdout.Drain();
        (string stderr, bool stderrDropped) = _stderr.Drain();

        bool timedOut;
        bool killed;
        int? exitCode;
        bool running;
        lock (_gate)
        {
            timedOut = _timedOut;
            killed = _killed;
            exitCode = _exitCode;
            running = !_processExited && !_timedOut && !_killed;
        }

        string footer =
            timedOut
                ? $"job {Id}: timed out after {_timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)}s; process tree killed"
            : killed ? $"job {Id}: killed; process tree stopped"
            : exitCode is { } code
                ? $"job {Id}: exit code: {code.ToString(CultureInfo.InvariantCulture)}"
            : $"job {Id}: running";

        var text = new StringBuilder();
        if (stdoutDropped || stderrDropped)
        {
            text.Append(TruncationMarker).Append('\n');
        }

        text.Append(stdout);
        if (stderr.Length > 0)
        {
            if (text.Length > 0 && text[^1] != '\n')
            {
                text.Append('\n');
            }

            text.Append("stderr:\n").Append(stderr);
        }

        if (text.Length > 0 && text[^1] != '\n')
        {
            text.Append('\n');
        }

        text.Append(footer);
        return new BashJobRead(
            text.ToString(),
            timedOut || (exitCode is { } exited && exited != 0),
            exitCode,
            running,
            timedOut,
            killed
        );
    }

    /// <summary>
    /// Ends the job through the environment's tree kill. A job that has already ended is a no-op,
    /// so <c>bash_kill</c> can report the natural exit code instead of inventing a kill.
    /// </summary>
    public async Task KillAsync()
    {
        bool shouldKill;
        lock (_gate)
        {
            shouldKill = !_processExited && !_timedOut && !_killed;
            if (shouldKill)
            {
                _killed = true;
            }
        }

        if (!shouldKill)
        {
            return;
        }

        await KillProcessAsync().ConfigureAwait(false);
    }

    private async Task WatchTimeoutAsync()
    {
        try
        {
            await Task.Delay(_timeout, _timerCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        bool shouldKill;
        lock (_gate)
        {
            shouldKill = !_processExited && !_timedOut && !_killed;
            if (shouldKill)
            {
                _timedOut = true;
            }
        }

        if (shouldKill)
        {
            await KillProcessAsync().ConfigureAwait(false);
        }
    }

    private async Task RunAsync()
    {
        try
        {
            _process.StandardInput?.Close();
        }
        catch (Exception exception) when (LocalProcessStreams.IsPipeClosed(exception))
        {
            // The child closed stdin before the job started pumping; nothing to close.
        }

        Task stdoutPump = PumpAsync(_process.StandardOutput.BaseStream, _stdout);
        Task stderrPump = PumpAsync(_process.StandardError.BaseStream, _stderr);
        int exitCode;
        try
        {
            exitCode = await _process
                .WaitForExitAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            exitCode = -1;
        }

        lock (_gate)
        {
            _processExited = true;
            _exitCode = exitCode;
        }

        try
        {
            await Task.WhenAll(stdoutPump, stderrPump)
                .WaitAsync(SettleTimeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A descendant can still hold the pipe after the command itself exited. The job's
            // command has ended, so its exit code is already recorded; the pumps are best effort.
        }

        _timerCancellation.Cancel();
        _finished.TrySetResult();
        _process.Dispose();
    }

    private async Task KillProcessAsync()
    {
        try
        {
            await _process.CancelAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The process may have exited between the state check and the kill; the completion
            // below still settles the job, and the final state is what the model reads.
        }

        if (_afterKill is { } afterKill)
        {
            try
            {
                await afterKill().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The host tree is already gone; a container-side step that cannot prove the
                // group gone resets its own container, so there is no second action here.
            }
        }

        try
        {
            await _finished.Task.WaitAsync(SettleTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // The kill itself was awaited; the pumps are bounded by the stream closing.
        }
    }

    private async Task SettleAsync()
    {
        bool wait;
        lock (_gate)
        {
            wait = !IsRunningLocked() && !_finished.Task.IsCompleted;
        }

        if (!wait)
        {
            return;
        }

        try
        {
            await _finished.Task.WaitAsync(SettleTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Read what was captured; the state footer still says the job has ended.
        }
    }

    private bool IsRunningLocked() => !_processExited && !_timedOut && !_killed;
}
