using System.ComponentModel;
using System.Globalization;
using System.Threading;
using Lunate.Ai;
using Lunate.Extensibility.Abstractions;
using Lunate.Tui;

namespace Lunate.Coding;

/// <summary>A container environment could not be prepared; the message is the teaching error.</summary>
internal sealed class ContainerEnvironmentException(string message) : Exception(message);

/// <summary>One engine invocation with the internal cancel and first-line observation hooks.</summary>
internal delegate Task<ProcessRunResult> ContainerProcessRun(
    ProcessRunRequest request,
    CancellationToken cancellationToken,
    Func<CancellationToken, Task>? onCancel,
    Action<string>? onFirstStdoutLine
);

/// <summary>
/// The container half of an execution environment: one long-lived engine container per session and
/// workspace root, created lazily before the first command, reused for every later command, and
/// removed when the session ends. Only the workspace is mounted, at the same absolute path on both
/// sides. Cancellation captures the command's container-side process group and kills it inside the
/// container; the host <c>exec</c> client is still killed by <see cref="ProcessRunner"/>.
/// </summary>
internal sealed partial class ContainerEnvironment : IDisposable
{
    private const string PgidPrefix = "LUNATE_PGRP:";
    private const string ContainerPrefix = "lunate-tools-";
    private const int OutputCap = 1_000_000;

    private static readonly TimeSpan EngineBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KillBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan KillGrace = TimeSpan.FromMilliseconds(500);

    private readonly ExecutionEnvironmentSelection _selection;
    private readonly ContainerProcessRun _run;
    private readonly Func<string, string?> _findOnPath;
    private readonly Func<string> _newName;
    private readonly Dictionary<string, string> _containers = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _prepare = new(1, 1);
    private readonly string? _enginePath;
    private readonly string? _unavailable;
    private bool _disposed;

    internal ContainerEnvironment(
        ExecutionEnvironmentSelection selection,
        Func<string, string?>? findOnPath = null,
        ContainerProcessRun? run = null,
        Func<string>? newName = null,
        bool? isWindows = null
    )
    {
        _selection = selection;
        _findOnPath = findOnPath ?? GuardProbe.Current.FindOnPath;
        _run = run ?? DefaultRun;
        _newName = newName ?? (() => ContainerPrefix + Guid.NewGuid().ToString("N")[..8]);
        _enginePath = _findOnPath(selection.Engine);
        _unavailable =
            (isWindows ?? OperatingSystem.IsWindows())
                ? $"container environments are not supported on Windows; use WSL2 with a translated "
                    + $"mount (follow-up) or run without --env ({selection.Display} was selected)"
            : _enginePath is null
                ? $"the {selection.Engine} engine was not found on PATH; install {selection.Engine} "
                    + $"or run without --env ({selection.Display} was selected)"
            : null;
        State = _unavailable is null ? "starting" : "failed: " + _unavailable;
    }

    /// <summary>Why the environment cannot be used; null while it can.</summary>
    internal string? Unavailable => _unavailable;

    /// <summary>The host shell stand-in the bash guard wraps: the engine executable itself.</summary>
    internal ResolvedShell? EngineShell =>
        _enginePath is null
            ? null
            : new ResolvedShell(ShellKind.Sh, _enginePath, _selection.Engine, []);

    /// <summary>The readiness word `/context` prints; it never creates a container to compute it.</summary>
    internal string State { get; private set; }

    internal EnvironmentStatus Status => new(_selection.Engine, _selection.Image, State);

    internal string Display => _selection.Display;

    /// <summary>
    /// Runs one command in the workspace's container, creating it first when needed. The returned
    /// result has the runner's pgid wrapper line removed. A preparation failure throws a
    /// <see cref="ContainerEnvironmentException"/> naming the engine, image and next step.
    /// </summary>
    internal async Task<ProcessRunResult> RunAsync(
        string workspaceRoot,
        string command,
        TimeSpan timeout,
        GuardCommand guard,
        CancellationToken ct
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_unavailable is { } reason)
        {
            throw new ContainerEnvironmentException(reason);
        }

        string root = CanonicalRoot(workspaceRoot);
        string name = await EnsureAsync(root, guard, ct).ConfigureAwait(false);
        var pgid = new PgidHolder();
        ProcessRunResult result;
        try
        {
            result = await _run(
                    ExecRequest(root, name, command, guard, timeout),
                    ct,
                    token => KillGroupOrRemoveAsync(root, name, pgid, token),
                    line =>
                    {
                        if (TryPgid(line, out int value))
                        {
                            pgid.Set(value);
                        }
                    }
                )
                .ConfigureAwait(false);
        }
        catch (ContainerEnvironmentException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception
                    is Win32Exception
                        or InvalidOperationException
                        or IOException
                        or PlatformNotSupportedException
                        or ArgumentException
            )
        {
            State = "failed: " + exception.Message;
            throw new ContainerEnvironmentException(
                $"the {_selection.Engine} engine could not run a command in {_selection.Image}: "
                    + $"{exception.Message}; check that the engine is running and the image is available"
            );
        }

        State = "ready";
        return StripPgidLine(result, pgid.Value);
    }

    private sealed class PgidHolder
    {
        private int _value;

        internal int Value => Volatile.Read(ref _value);

        internal void Set(int value) => Volatile.Write(ref _value, value);
    }
}
