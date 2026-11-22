using Lunate.Agent;
using Lunate.Tui;

namespace Lunate.Coding;

internal sealed partial class InteractiveSession
{
    private DateTimeOffset _notificationTurnStartedAt;
    private string? _notificationStopReason;
    private string? _notificationError;
    private INotificationSender? _notificationSender;

    /// <summary>Opens the notification clock for a turn; shared by typed and follow-up turns.</summary>
    private void BeginNotificationTurn()
    {
        _notificationTurnStartedAt = _options.Scheduler.Now;
        _notificationStopReason = null;
        _notificationError = null;
    }

    private void MarkNotificationStopped(string stopReason) => _notificationStopReason = stopReason;

    private void MarkNotificationFailed(string? message)
    {
        _notificationStopReason = null;
        _notificationError = message;
    }

    /// <summary>
    /// The one notification decision per root turn, after its event stream settled: enabled, a
    /// known-unfocused terminal and a long enough turn. Every failure is swallowed here because a
    /// desktop notification never owns the turn's result.
    /// </summary>
    private void OfferTurnNotification()
    {
        try
        {
            TimeSpan duration = _options.Scheduler.Now - _notificationTurnStartedAt;
            bool? focused = _console is ITerminalFocus terminalFocus
                ? terminalFocus.IsFocused
                : null;
            if (
                !TurnNotifier.ShouldNotify(
                    _settings.Notify,
                    focused,
                    duration,
                    TimeSpan.FromSeconds(_settings.NotifyMinSeconds)
                )
            )
            {
                return;
            }

            NotificationMessage message = TurnNotifier.Message(
                NotificationTitle(),
                _notificationStopReason,
                _notificationError,
                duration
            );
            (
                _notificationSender ??= _options.NotificationSender
                    ?? new DesktopNotificationSender()
            ).Send(message);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Best effort: the turn has already settled, and no notification problem may change
            // its outcome, output or exit code.
        }
    }

    /// <summary>The stored session title, then the session id, then the workspace directory name.</summary>
    private string NotificationTitle()
    {
        if (_session is { } session)
        {
            return SessionTitle.Read(session.Path) ?? session.SessionId;
        }

        string name = Path.GetFileName(
            _workingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        );
        return string.IsNullOrEmpty(name) ? "Lunate" : name;
    }
}
