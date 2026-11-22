using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The notification decision and platform command shapes: text travels out of band, an unsupported
/// platform or a throwing starter is silent, and the outcome wording covers every stop reason.
/// </summary>
public sealed class DesktopNotificationTests
{
    private static readonly NotificationMessage Message = new("release prep", "finished");

    [Fact]
    public void Macos_passes_the_text_as_environment_data()
    {
        NotificationCommand command = Assert.IsType<NotificationCommand>(
            DesktopNotificationCommand.For(NotificationPlatform.MacOs, Message)
        );

        Assert.Equal("osascript", command.Executable);
        Assert.DoesNotContain("release prep", string.Join(" ", command.Arguments));
        Assert.Equal("release prep", command.Environment[DesktopNotificationCommand.TitleVariable]);
    }

    [Fact]
    public void Linux_passes_the_text_after_the_end_of_options_marker()
    {
        NotificationCommand command = Assert.IsType<NotificationCommand>(
            DesktopNotificationCommand.For(NotificationPlatform.Linux, Message)
        );

        Assert.Equal("notify-send", command.Executable);
        Assert.Equal(
            ["--app-name=Lunate", "--urgency=normal", "--", "release prep", "finished"],
            command.Arguments
        );
        Assert.Empty(command.Environment);
    }

    [Fact]
    public void Windows_uses_the_shell_notification_area_and_no_app_identity()
    {
        NotificationCommand command = Assert.IsType<NotificationCommand>(
            DesktopNotificationCommand.For(NotificationPlatform.Windows, Message)
        );

        Assert.Equal("powershell.exe", command.Executable);
        Assert.Contains("-NoProfile", command.Arguments);
        Assert.DoesNotContain("release prep", string.Join(" ", command.Arguments));
        Assert.Equal("finished", command.Environment[DesktopNotificationCommand.BodyVariable]);
        Assert.Equal("release prep", command.Environment[DesktopNotificationCommand.TitleVariable]);

        string script = string.Join(" ", command.Arguments);

        // An unpackaged app has no registered AppUserModelID, so a Windows Runtime toast cannot
        // show; the shell notification area's balloon needs no identity and renders as a toast.
        Assert.DoesNotContain("CreateToastNotifier", script, StringComparison.Ordinal);
        Assert.Contains("System.Windows.Forms.NotifyIcon", script, StringComparison.Ordinal);
        Assert.Contains("ShowBalloonTip", script, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unsupported_platform_has_no_command()
    {
        Assert.Null(DesktopNotificationCommand.For(NotificationPlatform.Unsupported, Message));
    }

    [Fact]
    public void The_helper_keeps_the_desktop_session_and_not_the_credentials()
    {
        NotificationCommand command = Assert.IsType<NotificationCommand>(
            DesktopNotificationCommand.For(NotificationPlatform.Linux, Message)
        );

        IReadOnlyDictionary<string, string> environment =
            DesktopNotificationSender.ChildEnvironment(
                command,
                [
                    new("DISPLAY", ":0"),
                    new("DBUS_SESSION_BUS_ADDRESS", "unix:path=/run/user/1000/bus"),
                    new("XAUTHORITY", "/run/user/1000/gdm/Xauthority"),
                    new("OPENAI_API_KEY", "sk-live"),
                    new("GITHUB_TOKEN", "ghp-live"),
                ]
            );

        // Without DISPLAY and the message bus address, notify-send cannot reach the user's session.
        Assert.Equal(":0", environment["DISPLAY"]);
        Assert.Equal("unix:path=/run/user/1000/bus", environment["DBUS_SESSION_BUS_ADDRESS"]);
        Assert.Equal("/run/user/1000/gdm/Xauthority", environment["XAUTHORITY"]);

        // The helper reads nothing of Lunate's; a notification is not a reason to hand a key over.
        Assert.DoesNotContain("OPENAI_API_KEY", environment.Keys);
        Assert.DoesNotContain("GITHUB_TOKEN", environment.Keys);
    }

    [Fact]
    public void The_helper_receives_the_message_in_the_declared_variables()
    {
        NotificationCommand command = Assert.IsType<NotificationCommand>(
            DesktopNotificationCommand.For(NotificationPlatform.MacOs, Message)
        );

        IReadOnlyDictionary<string, string> environment =
            DesktopNotificationSender.ChildEnvironment(command, []);

        Assert.Equal("release prep", environment[DesktopNotificationCommand.TitleVariable]);
        Assert.Equal("finished", environment[DesktopNotificationCommand.BodyVariable]);
    }

    [Fact]
    public void A_throwing_process_start_is_swallowed()
    {
        var sender = new DesktopNotificationSender(
            () => NotificationPlatform.Linux,
            _ => throw new InvalidOperationException("notify-send is missing")
        );

        sender.Send(Message);
    }

    [Theory]
    [InlineData(true, false, 10, true)]
    [InlineData(true, true, 10, false)]
    [InlineData(true, null, 10, false)]
    [InlineData(false, false, 10, false)]
    [InlineData(true, false, 9, false)]
    public void The_gate_requires_enabled_known_unfocused_and_long_enough(
        bool enabled,
        bool? focused,
        int seconds,
        bool expected
    )
    {
        Assert.Equal(
            expected,
            TurnNotifier.ShouldNotify(
                enabled,
                focused,
                TimeSpan.FromSeconds(seconds),
                TimeSpan.FromSeconds(10)
            )
        );
    }

    [Fact]
    public void The_gate_accepts_the_threshold_and_a_zero_threshold()
    {
        Assert.True(
            TurnNotifier.ShouldNotify(
                true,
                false,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10)
            )
        );
        Assert.True(TurnNotifier.ShouldNotify(true, false, TimeSpan.Zero, TimeSpan.FromSeconds(0)));
    }

    [Theory]
    [InlineData(StopReasons.Stop, "finished after 42s")]
    [InlineData(StopReasons.Length, "finished (answer cut at the length limit) after 42s")]
    [InlineData(StopReasons.Cancelled, "cancelled after 42s")]
    [InlineData(StopReasons.StepLimit, "stopped at the step limit after 42s")]
    [InlineData(StopReasons.TokenLimit, "stopped at the token limit after 42s")]
    [InlineData(StopReasons.CostLimit, "stopped at the cost limit after 42s")]
    [InlineData(StopReasons.TimeLimit, "stopped at the time limit after 42s")]
    public void The_body_names_the_outcome_and_the_elapsed_time(string reason, string expected)
    {
        NotificationMessage message = TurnNotifier.Message(
            "release prep",
            reason,
            null,
            TimeSpan.FromSeconds(42)
        );

        Assert.Equal("release prep", message.Title);
        Assert.Equal(expected, message.Body);
    }

    [Fact]
    public void A_failure_message_is_one_bounded_line()
    {
        NotificationMessage message = TurnNotifier.Message(
            "release prep",
            null,
            "provider failed\n" + new string('x', 500),
            TimeSpan.FromSeconds(2)
        );

        Assert.StartsWith("failed: provider failed ", message.Body, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', message.Body);
        Assert.True(message.Body.Length < 260);
    }
}
