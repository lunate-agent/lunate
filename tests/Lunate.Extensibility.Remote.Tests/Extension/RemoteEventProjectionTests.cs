using System.Text.Json;
using System.Text.Json.Nodes;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;
using Lunate.Extensibility.Remote;

namespace Lunate.Extensibility.Remote.Tests;

/// <summary>
/// The observable wire half of a remote extension (protocol version 3, ADR-0040): the handshake
/// negotiation and its refusal, the manifest-filtered <c>event/&lt;kind&gt;</c> projection, the
/// child that accepts fewer kinds or none, and the subscription's lifecycle across crash, restart
/// and shutdown.
/// </summary>
public sealed class RemoteEventProjectionTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly WorkspaceInfo Workspace = new("/work", "/repo", "/repo/.git");

    [Fact]
    public async Task A_declared_event_reaches_the_child_as_the_json_line()
    {
        Harness harness = Harness.Create(events: ["*"]);
        await harness.Extension.EnsureStartedAsync(Ct);

        var started = new RunStarted("run_1") { SessionId = "s_1" };
        harness.Context.Stream.Publish(started);
        harness.Context.Stream.Publish(new RunFinished("run_1", "stop"));

        await WaitAsync(
            () =>
                harness.Child.Notifications.Any(notification =>
                    notification.Method == "event/run_finished"
                ),
            "both event notifications"
        );
        (string Method, JsonElement? Parameters) received = harness.Child.Notifications.Single(
            notification => notification.Method == "event/run_started"
        );
        Assert.Equal("event/run_started", received.Method);
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(RemoteEventJson.Serialize(started)),
                JsonNode.Parse(received.Parameters!.Value.GetRawText())
            ),
            received.Parameters!.Value.GetRawText()
        );
        // The line, not a second model: the same stamped fields `--json` writes.
        Assert.Equal("run_started", received.Parameters!.Value.GetProperty("type").GetString());
        Assert.Equal("s_1", received.Parameters!.Value.GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task The_manifest_watch_list_filters_the_wire_and_a_missing_kind_warns()
    {
        Harness harness = Harness.Create(events: ["run_started", "run_finished"]);
        harness.Child.Handshake = request => new InitializeResult(
            request.ProtocolVersion,
            request.Hooks,
            request.Services
        )
        {
            Events = ["run_started"],
        };

        await harness.Extension.EnsureStartedAsync(Ct);
        harness.Context.Stream.Publish(new RunStarted("run_1"));
        harness.Context.Stream.Publish(new RunFinished("run_1", "stop"));

        await WaitAsync(
            () => harness.Child.ObservedEventKinds().Contains("run_started"),
            "the declared kind"
        );
        Assert.DoesNotContain(harness.Child.Notifications, notification =>
            notification.Method == "event/run_finished"
        );
        Assert.Contains(harness.Context.Log.Warnings, warning =>
            warning.Contains("event 'run_finished'", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task A_child_that_accepts_no_kinds_warns_and_is_not_subscribed()
    {
        Harness harness = Harness.Create(events: ["*"]);
        harness.Child.Handshake = request => new InitializeResult(
            request.ProtocolVersion,
            request.Hooks,
            request.Services
        );

        await harness.Extension.EnsureStartedAsync(Ct);
        harness.Context.Stream.Publish(new RunStarted("run_1"));

        Assert.Contains(harness.Context.Log.Warnings, warning =>
            warning.Contains("accepts no event kinds", StringComparison.Ordinal)
        );
        Assert.False(harness.Context.Stream.Status(harness.Context.Id).Subscribed);
        await Task.Delay(100, Ct);
        Assert.Empty(harness.Child.ObservedEventKinds());
    }

    [Fact]
    public async Task A_version_2_child_loads_without_events()
    {
        Harness harness = Harness.Create(events: []);
        harness.Child.Handshake = request => new InitializeResult(
            2,
            request.Hooks,
            request.Services
        );

        await harness.Extension.EnsureStartedAsync(Ct);

        Assert.Equal(RemoteExtensionState.Ready, harness.Extension.Status.State);
        Assert.Equal(RemoteProtocol.Version, harness.Child.LastHandshake!.ProtocolVersion);
        Assert.False(harness.Context.Stream.Status(harness.Context.Id).Subscribed);
    }

    [Fact]
    public async Task A_version_2_child_with_a_declared_watch_list_is_refused_naming_protocol_3()
    {
        Harness harness = Harness.Create(events: ["*"]);
        harness.Child.Handshake = request => new InitializeResult(
            2,
            request.Hooks,
            request.Services
        );

        RemoteExtensionException exception = await Assert.ThrowsAsync<RemoteExtensionException>(
            () =>
                harness.Extension.EnsureStartedAsync(Ct).AsTask()
        );

        Assert.Equal(RemoteExtensionState.Refused, harness.Extension.Status.State);
        Assert.Contains("'events'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("protocol version 3", exception.Message, StringComparison.Ordinal);
        Assert.Contains("protocol 2", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_handshake_carries_the_manifest_watch_list()
    {
        Harness harness = Harness.Create(events: ["run_started", "tool_call_result"]);
        harness.Child.Handshake = request => new InitializeResult(
            request.ProtocolVersion,
            request.Hooks,
            request.Services
        )
        {
            Events = ["*"],
        };

        await harness.Extension.EnsureStartedAsync(Ct);

        Assert.Equal(["run_started", "tool_call_result"], harness.Child.LastHandshake!.Events);
    }

    [Fact]
    public async Task A_restart_subscribes_again_and_a_shutdown_stops_it()
    {
        Harness harness = Harness.Create(events: ["*"]);
        await harness.Extension.EnsureStartedAsync(Ct);
        harness.Context.Stream.Publish(new RunStarted("run_1"));
        await WaitAsync(
            () => harness.Child.ObservedEventKinds().Contains("run_started"),
            "the first child's event"
        );

        harness.Child.Crash();
        await WaitAsync(
            () =>
                harness.Extension.Status.State == RemoteExtensionState.Ready
                && harness.Child.StartCount == 2,
            "the automatic restart"
        );
        harness.Context.Stream.Publish(new RunStarted("run_2"));
        await WaitAsync(
            () =>
                harness.Child.ObservedEventKinds().Count >= 1
                && harness.Child.NotificationCount > 1,
            "the restarted child's event"
        );

        await harness.Extension.ShutdownAsync(Ct);
        Assert.False(harness.Context.Stream.Status(harness.Context.Id).Subscribed);
        int seen = harness.Child.NotificationCount;
        harness.Context.Stream.Publish(new RunStarted("run_3"));
        await Task.Delay(100, Ct);
        Assert.Equal(seen, harness.Child.NotificationCount);
    }

    [Fact]
    public async Task A_session_end_drains_the_queue_and_releases_the_subscription()
    {
        Harness harness = Harness.Create(events: ["*"]);
        await harness.Extension.EnsureStartedAsync(Ct);
        harness.Context.Stream.Publish(new RunStarted("run_1"));

        await harness.Context.Stream.CompleteAllAsync(Ct);

        await WaitAsync(
            () => harness.Child.ObservedEventKinds().Contains("run_started"),
            "the queued event to reach the child"
        );
        await WaitAsync(
            () => !harness.Context.Stream.Status(harness.Context.Id).Subscribed,
            "the projection to release its subscription"
        );
    }

    [Fact]
    public async Task A_priced_usage_reaches_the_child_as_the_json_line_with_its_cost()
    {
        Harness harness = Harness.Create(events: ["usage_updated"], costUsd: _ => 0.5m);
        await harness.Extension.EnsureStartedAsync(Ct);
        var usage = new UsageUpdated(
            "run_1",
            new Microsoft.Extensions.AI.UsageDetails { InputTokenCount = 1000 }
        );

        harness.Context.Stream.Publish(usage);

        await WaitAsync(
            () => harness.Child.ObservedEventKinds().Contains("usage_updated"),
            "the usage event"
        );
        (string _, JsonElement? parameters) = harness.Child.Notifications.Single(notification =>
            notification.Method == "event/usage_updated"
        );
        // The wire line is the --json line, cost included: the host supplies the catalog estimate,
        // because the wire package sits below the catalog and cannot price a record itself.
        Assert.True(
            JsonNode.DeepEquals(
                JsonNode.Parse(RemoteEventJson.Serialize(usage, _ => 0.5m)),
                JsonNode.Parse(parameters!.Value.GetRawText())
            ),
            parameters!.Value.GetRawText()
        );
        Assert.Equal(0.5m, parameters!.Value.GetProperty("costUsd").GetDecimal());
    }

    private static async Task WaitAsync(Func<bool> condition, string what)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20, Ct);
        }

        Assert.Fail($"timed out waiting for {what}");
    }

    private sealed class Harness
    {
        private Harness(
            RemoteExtension extension,
            ScriptedChild child,
            FakeExtensionContext context,
            string directory
        )
        {
            Extension = extension;
            Child = child;
            Context = context;
            ExtensionDirectory = directory;
        }

        public RemoteExtension Extension { get; }

        public ScriptedChild Child { get; }

        public FakeExtensionContext Context { get; }

        public string ExtensionDirectory { get; }

        public static Harness Create(
            IReadOnlyList<string>? events = null,
            IReadOnlyList<string>? hooks = null,
            Func<UsageUpdated, decimal?>? costUsd = null
        )
        {
            var context = new FakeExtensionContext();
            var child = new ScriptedChild();
            var directory = Path.Combine(
                Path.GetTempPath(),
                "lunate-remote-event-tests",
                Guid.NewGuid().ToString("N")
            );
            System.IO.Directory.CreateDirectory(directory);
            var extension = new RemoteExtension(
                new RemoteExtensionOptions
                {
                    Manifest = new ExtensionManifest
                    {
                        Id = context.Id,
                        Version = "0.1.0",
                        ApiVersion = "^1.0.0",
                        Runtime = new ExtensionRuntime("node", ["index.js"]),
                        Hooks = hooks ?? [],
                        Services = [],
                        Events = events ?? [],
                    },
                    Directory = directory,
                    Workspace = Workspace,
                    ProcessRunner = child.Runner,
                    Context = context,
                    CostUsd = costUsd,
                    UnregisterHook = handler => context.Handlers.Remove(handler),
                    ShutdownGrace = TimeSpan.FromMilliseconds(300),
                }
            );
            return new Harness(extension, child, context, directory);
        }
    }
}
