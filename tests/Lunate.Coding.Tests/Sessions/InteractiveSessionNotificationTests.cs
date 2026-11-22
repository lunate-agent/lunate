using System.Collections.Concurrent;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The session's notification gate: only a long, known-unfocused root turn sends, the title comes
/// from the session sidecar, and a sender failure leaves the finished turn untouched.
/// </summary>
public sealed class InteractiveSessionNotificationTests
{
    [Fact]
    public async Task A_long_unfocused_turn_notifies_with_the_session_title_and_outcome()
    {
        var sender = new RecordingNotificationSender();
        using var host = new InteractiveSessionHost(
            settingsJson: Enabled(seconds: 0),
            notificationSender: sender
        );
        host.Console.IsFocused = false;
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await RunTurnAsync(host, "first", "first answer");
        await host.WaitUntilAsync(() => sender.Messages.Count == 1);

        string sessionFile = Assert.Single(
            Directory.GetFiles(host.Temp.File("sessions"), "*.jsonl")
        );
        SessionTitle.Write(sessionFile, "release prep");
        await RunTurnAsync(host, "second", "second answer");
        await host.WaitUntilAsync(() => sender.Messages.Count == 2);

        NotificationMessage message = sender.Messages[1];
        Assert.Equal("release prep", message.Title);
        Assert.Contains("finished after", message.Body, StringComparison.Ordinal);

        await QuitAsync(host, run);
    }

    [Fact]
    public async Task The_notify_flag_enables_an_otherwise_disabled_session()
    {
        var sender = new RecordingNotificationSender();
        using var host = new InteractiveSessionHost(
            settingsJson: """{ "schemaVersion": 2, "notifyMinSeconds": 0 }""",
            launch: new LaunchOptions { Notify = true },
            notificationSender: sender
        );
        host.Console.IsFocused = false;
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await RunTurnAsync(host, "go", "done");
        await host.WaitUntilAsync(() => sender.Messages.Count == 1);

        Assert.Contains("finished after", sender.Messages[0].Body, StringComparison.Ordinal);
        await QuitAsync(host, run);
    }

    [Fact]
    public async Task Notifications_are_off_by_default()
    {
        var sender = new RecordingNotificationSender();
        using var host = new InteractiveSessionHost(
            settingsJson: """{ "schemaVersion": 2 }""",
            notificationSender: sender
        );
        host.Console.IsFocused = false;
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await RunTurnAsync(host, "go", "done");

        Assert.Empty(sender.Messages);
        await QuitAsync(host, run);
    }

    [Fact]
    public async Task A_child_run_finishing_during_the_root_turn_sends_no_extra_notification()
    {
        var sender = new RecordingNotificationSender();
        InteractiveSessionHost? host = null;
        var injected = false;
        host = new InteractiveSessionHost(
            settingsJson: Enabled(seconds: 0),
            notificationSender: sender,
            eventObserverSuffix: agentEvent =>
            {
                if (injected || agentEvent is not RunStarted { ParentRunId: null } started)
                {
                    return;
                }

                injected = true;
                host!.Session.HandleEvent(
                    new RunStarted("child_run") { ParentRunId = started.RunId }
                );
                host.Session.HandleEvent(
                    new RunFinished("child_run", StopReasons.Stop) { ParentRunId = started.RunId }
                );
            }
        );
        using (host)
        {
            host.Console.IsFocused = false;
            Task run = host.RunAsync();
            await host.WaitUntilAsync(() => host.Session.IsComposed);

            await RunTurnAsync(host, "go", "done");
            await host.WaitUntilAsync(() => sender.Messages.Count == 1);

            Assert.Single(sender.Messages);
            await QuitAsync(host, run);
        }
    }

    [Fact]
    public async Task A_focused_terminal_sends_nothing()
    {
        var sender = new RecordingNotificationSender();
        using var host = new InteractiveSessionHost(
            settingsJson: Enabled(seconds: 0),
            notificationSender: sender
        );
        host.Console.IsFocused = true;
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await RunTurnAsync(host, "go", "done");

        Assert.Empty(sender.Messages);
        await QuitAsync(host, run);
    }

    [Fact]
    public async Task An_unknown_focus_state_sends_nothing()
    {
        var sender = new RecordingNotificationSender();
        using var host = new InteractiveSessionHost(
            settingsJson: Enabled(seconds: 0),
            notificationSender: sender
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await RunTurnAsync(host, "go", "done");

        Assert.Empty(sender.Messages);
        await QuitAsync(host, run);
    }

    [Fact]
    public async Task A_turn_below_the_threshold_sends_nothing()
    {
        var sender = new RecordingNotificationSender();
        using var host = new InteractiveSessionHost(
            settingsJson: Enabled(seconds: 10),
            notificationSender: sender
        );
        host.Console.IsFocused = false;
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await RunTurnAsync(host, "go", "done");

        Assert.Empty(sender.Messages);
        await QuitAsync(host, run);
    }

    [Fact]
    public async Task A_throwing_sender_never_fails_the_turn()
    {
        using var host = new InteractiveSessionHost(
            settingsJson: Enabled(seconds: 0),
            notificationSender: new ThrowingNotificationSender()
        );
        host.Console.IsFocused = false;
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await RunTurnAsync(host, "go", "done");

        Assert.Contains("done", host.Scrollback.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("notification", host.Diagnostics, StringComparison.OrdinalIgnoreCase);
        await QuitAsync(host, run);
    }

    private static string Enabled(int seconds) =>
        $$"""
            { "schemaVersion": 2, "notify": true, "notifyMinSeconds": {{seconds}} }
            """;

    private static async Task RunTurnAsync(
        InteractiveSessionHost host,
        string prompt,
        string answer
    )
    {
        host.Client.Enqueue(Scripts.Text(answer), Scripts.Stop());
        host.Console.SendText(prompt);
        host.Console.SendEnter();
        await host.WaitForScrollbackAsync(answer);
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
    }

    private static async Task QuitAsync(InteractiveSessionHost host, Task run)
    {
        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    private sealed class RecordingNotificationSender : INotificationSender
    {
        private readonly ConcurrentQueue<NotificationMessage> _messages = new();

        public IReadOnlyList<NotificationMessage> Messages => [.. _messages];

        public void Send(NotificationMessage message) => _messages.Enqueue(message);
    }

    private sealed class ThrowingNotificationSender : INotificationSender
    {
        public void Send(NotificationMessage message) =>
            throw new InvalidOperationException("the notification transport is unavailable");
    }
}
