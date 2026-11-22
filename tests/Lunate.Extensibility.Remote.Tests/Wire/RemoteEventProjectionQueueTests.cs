using System.Text.Json;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;
using Lunate.Extensibility.Remote;

namespace Lunate.Extensibility.Remote.Tests;

/// <summary>
/// The wire projection's bounded queue and per-event cap, driven directly with a channel that
/// models a child which stops reading its stdin: the oldest events are dropped and counted with one
/// warning instead of delaying the run, and an event over the line cap is dropped with one warning
/// while the wire keeps going.
/// </summary>
public sealed class RemoteEventProjectionQueueTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_stalled_child_loses_its_oldest_events_with_one_warning()
    {
        var log = new RecordingLog();
        var channel = new GatedLineChannel();
        await using RemoteWire wire = Connected(channel);
        var source = new TestEvents();
        using var projection = new RemoteEventProjection(
            "remote-sample",
            log,
            wire,
            ["*"],
            maxLineBytes: 16 * 1024 * 1024,
            source,
            onStopped: () => { }
        );

        int offered = RemoteProtocol.EventQueueCapacity + 44;
        for (var index = 0; index < offered; index++)
        {
            source.Publish(new RunStarted("run_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        await WaitAsync(
            () => projection.Dropped > 0,
            "the queue to overflow"
        );

        // The pump took a few before the queue filled, so the exact count is the queue's business;
        // what the contract states is that the loss happens, is counted, and warns exactly once.
        Assert.InRange(projection.Dropped, 40, offered - RemoteProtocol.EventQueueCapacity);
        Assert.Single(log.Warnings, warning =>
            warning.Contains("fell behind", StringComparison.Ordinal)
        );

        channel.Release();
    }

    [Fact]
    public async Task An_oversized_event_is_dropped_and_the_wire_continues()
    {
        var log = new RecordingLog();
        var channel = new RecordingLineChannel();
        await using RemoteWire wire = Connected(channel);
        var source = new TestEvents();
        using var projection = new RemoteEventProjection(
            "remote-sample",
            log,
            wire,
            ["*"],
            maxLineBytes: 200,
            source,
            onStopped: () => { }
        );

        source.Publish(new TextMessageContent("run_1", "msg_1", new string('x', 400)));
        source.Publish(new RunStarted("run_1"));

        await WaitAsync(
            () => channel.Methods.Contains("event/run_started"),
            "the next event to reach the wire"
        );
        Assert.DoesNotContain("event/text_message_content", channel.Methods);
        Assert.Single(log.Warnings, warning =>
            warning.Contains("line cap", StringComparison.Ordinal)
            && warning.Contains("text_message_content", StringComparison.Ordinal)
        );
    }

    private static RemoteWire Connected(IRemoteLineChannel channel)
    {
        var wire = new RemoteWire(channel);
        wire.Start();
        return wire;
    }

    private static async Task WaitAsync(Func<bool> condition, string what)
    {
        for (var attempt = 0; attempt < 250; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20, Ct);
        }

        Assert.Fail($"timed out waiting for {what}");
    }

    /// <summary>The observable the projection subscribes to; a test publishes into it directly.</summary>
    private sealed class TestEvents : IObservable<AgentEvent>
    {
        private IObserver<AgentEvent>? _observer;

        public IDisposable Subscribe(IObserver<AgentEvent> observer)
        {
            _observer = observer;
            return new Subscription();
        }

        public void Publish(AgentEvent agentEvent) => _observer!.OnNext(agentEvent);

        private sealed class Subscription : IDisposable
        {
            public void Dispose() { }
        }
    }

    /// <summary>A channel whose writes block until <see cref="Release"/>, like a full child stdin.</summary>
    private sealed class GatedLineChannel : IRemoteLineChannel
    {
        private readonly TaskCompletionSource _gate = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private readonly TaskCompletionSource<string?> _read = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private readonly List<string> _lines = [];

        public void Release() => _gate.TrySetResult();

        public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
            await _read.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        public async ValueTask WriteLineAsync(string line, CancellationToken cancellationToken)
        {
            await _gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            _lines.Add(line);
        }

        public ValueTask DisposeAsync()
        {
            _gate.TrySetResult();
            _read.TrySetResult(null);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A channel that records the methods written and never reads.</summary>
    private sealed class RecordingLineChannel : IRemoteLineChannel
    {
        private readonly TaskCompletionSource<string?> _read = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private readonly List<string> _methods = [];
        private readonly object _gate = new();

        public IReadOnlyList<string> Methods
        {
            get
            {
                lock (_gate)
                {
                    return [.. _methods];
                }
            }
        }

        public async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
            await _read.Task.WaitAsync(cancellationToken).ConfigureAwait(false);

        public ValueTask WriteLineAsync(string line, CancellationToken cancellationToken)
        {
            using JsonDocument frame = JsonDocument.Parse(line);
            lock (_gate)
            {
                _methods.Add(frame.RootElement.GetProperty("method").GetString()!);
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _read.TrySetResult(null);
            return ValueTask.CompletedTask;
        }
    }
}
