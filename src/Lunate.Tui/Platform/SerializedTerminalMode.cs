namespace Lunate.Tui.Platform;

/// <summary>
/// One terminal-mode instance's transitions, serialised. The session's ordinary exit path (a scope
/// exit) and a signal handler restoring the terminal call <see cref="Dispose"/> from different
/// threads, and the restore must never end up overwritten by a later raw application: every
/// transition runs behind one lock, and once the terminal is restored (disposed), further
/// <see cref="Suspend"/> and <see cref="Resume"/> are no-ops. The platform delegates decide only
/// whether a mode change succeeded (a failure leaves the state unchanged so a later transition
/// can retry), and <see cref="Dispose"/> runs the restore exactly once.
/// </summary>
internal sealed class SerializedTerminalMode(Func<bool> suspend, Func<bool> resume, Action restore)
    : ITerminalMode
{
    private readonly object _gate = new();
    private bool _suspended;
    private bool _disposed;

    public void Suspend()
    {
        lock (_gate)
        {
            if (_disposed || _suspended)
            {
                return;
            }

            _suspended = suspend();
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_disposed || !_suspended)
            {
                return;
            }

            _suspended = !resume();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _suspended = false;
            restore();
        }
    }
}
