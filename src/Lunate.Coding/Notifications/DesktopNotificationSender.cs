using System.Diagnostics;

namespace Lunate.Coding;

/// <summary>The notification seam; a test substitutes a fake so no process starts.</summary>
internal interface INotificationSender
{
    void Send(NotificationMessage message);
}

/// <summary>
/// The production sender: chooses the host's notification command, starts it detached and drains
/// its output. It never blocks the turn and never throws to it; a missing tool, a refused toast or
/// a dead output stream is the same silent no-op.
/// </summary>
internal sealed class DesktopNotificationSender : INotificationSender
{
    private readonly Func<NotificationPlatform> _platform;
    private readonly Action<NotificationCommand> _start;

    public DesktopNotificationSender()
        : this(() => DesktopNotificationCommand.Detect(), StartDetached) { }

    internal DesktopNotificationSender(
        Func<NotificationPlatform> platform,
        Action<NotificationCommand> start
    )
    {
        _platform = platform;
        _start = start;
    }

    public void Send(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            if (DesktopNotificationCommand.For(_platform(), message) is { } command)
            {
                _start(command);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Best effort by contract: the turn has already settled and a notification failure is
            // not the user's problem to solve mid-run.
        }
    }

    private static void StartDetached(NotificationCommand command)
    {
        var startInfo = new ProcessStartInfo(command.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach ((string name, string value) in command.Environment)
        {
            startInfo.Environment[name] = value;
        }

        Process? process = Process.Start(startInfo);
        if (process is not null)
        {
            _ = DrainAndDisposeAsync(process);
        }
    }

    private static async Task DrainAndDisposeAsync(Process process)
    {
        using (process)
        {
            try
            {
                await Task.WhenAll(
                        process.StandardOutput.ReadToEndAsync(),
                        process.StandardError.ReadToEndAsync(),
                        process.WaitForExitAsync()
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception
                        is IOException
                            or InvalidOperationException
                            or ObjectDisposedException
                )
            {
                // The child disappeared while its output was being drained.
            }
        }
    }
}
