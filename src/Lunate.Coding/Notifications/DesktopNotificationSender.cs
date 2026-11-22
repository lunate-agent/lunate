using System.Diagnostics;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding;

/// <summary>The notification seam; a test substitutes a fake so no process starts.</summary>
internal interface INotificationSender
{
    void Send(NotificationMessage message);
}

/// <summary>
/// The production sender: chooses the host's notification command, starts it detached and drains
/// its output. It never blocks the turn and never throws to it; a missing tool, a refused
/// notification or a dead output stream is the same silent no-op.
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

        startInfo.Environment.Clear();
        foreach ((string name, string value) in ChildEnvironment(command))
        {
            startInfo.Environment[name] = value;
        }

        Process? process = Process.Start(startInfo);
        if (process is not null)
        {
            _ = DrainAndDisposeAsync(process);
        }
    }

    /// <summary>
    /// The helper's environment: the builder's <see cref="ChildEnvironmentPolicy.Inherited"/>
    /// policy with the message's two variables added and Lunate's credentials withheld. The desktop
    /// session's own names have to survive or the helper cannot reach the message bus —
    /// <c>DISPLAY</c>, <c>DBUS_SESSION_BUS_ADDRESS</c> and <c>XAUTHORITY</c> on Linux, the WinRT
    /// runtime on Windows — while a provider key must not, exactly like the tool child.
    /// <paramref name="inherited"/> is the test seam for the starting set.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ChildEnvironment(
        NotificationCommand command,
        IEnumerable<KeyValuePair<string, string>>? inherited = null
    ) =>
        EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            set: command.Environment,
            strip: CredentialVariables.All,
            inherited: inherited
        );

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
