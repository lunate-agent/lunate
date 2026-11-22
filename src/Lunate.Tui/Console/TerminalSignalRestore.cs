using System.Runtime.InteropServices;

namespace Lunate.Tui;

/// <summary>
/// A raw-mode instance a signal handler can restore without depending on a writable terminal. A
/// signal can arrive while the UI thread holds the console writer blocked on a full output buffer;
/// the ordinary dispose's focus-report request would wait for that writer and keep the process
/// alive instead of letting the signal end it. The implementation still restores the terminal mode
/// exactly as the quit path does, and treats the optional focus request as best effort.
/// </summary>
internal interface ISignalTerminalRestore
{
    /// <summary>Restores the terminal mode and returns without waiting on a terminal write.</summary>
    void RestoreForSignal();
}

/// <summary>
/// Signal exits restore the terminal: <see cref="PosixSignal.SIGTERM"/> and
/// <see cref="PosixSignal.SIGHUP"/> run the same restore an ordinary quit runs — disposing the
/// raw-mode instance the session holds — and then the signal's default disposition ends the
/// process, so the handler touches nothing else and waits for nothing. Nothing is registered
/// when raw mode was never entered (the caller passes the no-op disposable) or on Windows,
/// where these signals are not delivered to a console session like this; the quit path keeps
/// its own disposal and the restore is one shared instance, never a second implementation.
/// </summary>
internal static class TerminalSignalRestore
{
    public static IDisposable Install(IDisposable terminalMode, bool isWindows)
    {
        if (isWindows || terminalMode is NoopDisposable)
        {
            return NoopDisposable.Instance;
        }

        PosixSignalRegistration sigterm = PosixSignalRegistration.Create(
            PosixSignal.SIGTERM,
            Handle
        );
        PosixSignalRegistration sighup;
        try
        {
            sighup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, Handle);
        }
        catch
        {
            sigterm.Dispose();
            throw;
        }

        return new Handlers(sigterm, sighup);

        void Handle(PosixSignalContext context)
        {
            // The restore path /quit uses; the context's Cancel stays unset, so the runtime
            // re-raises with the default disposition after the handler returns. A mode that
            // knows its ordinary dispose can wait on a terminal write takes the signal-specific
            // entry, which restores the mode and makes that write best effort.
            if (terminalMode is ISignalTerminalRestore signalRestore)
            {
                signalRestore.RestoreForSignal();
            }
            else
            {
                terminalMode.Dispose();
            }
        }
    }

    private sealed class Handlers(PosixSignalRegistration sigterm, PosixSignalRegistration sighup)
        : IDisposable
    {
        public void Dispose()
        {
            sigterm.Dispose();
            sighup.Dispose();
        }
    }
}
