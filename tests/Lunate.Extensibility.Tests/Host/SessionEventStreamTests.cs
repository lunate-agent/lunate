using System.Globalization;
using Lunate.Agent;

namespace Lunate.Extensibility.Tests;

/// <summary>
/// The observable event stream's contract (ADR-0040): ordered delivery per subscription, a bounded
/// queue that drops the oldest with one warning, per-subscription error isolation, and completion
/// once after the queued events. The stream is the extension-facing half; the hooks' own tests live
/// beside the other hook tests and are unchanged.
/// </summary>
public sealed class SessionEventStreamTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task A_subscription_receives_published_events_in_order()
    {
        var log = new RecordingExtensionLog();
        using var stream = new SessionEventStream(log);
        var observer = new RecordingObserver();

        using IDisposable subscription = stream.Subscribe("watcher", observer);
        stream.Publish(new RunStarted("run-1"));
        stream.Publish(new TextMessageContent("run-1", "m1", "hi"));
        stream.Publish(new RunFinished("run-1", "stop"));

        Assert.True(observer.WaitFor(events => events.Count == 3, Wait));
        Assert.Collection(
            observer.Events,
            entry => Assert.IsType<RunStarted>(entry.Value),
            entry => Assert.IsType<TextMessageContent>(entry.Value),
            entry => Assert.IsType<RunFinished>(entry.Value)
        );
        ExtensionEventStreamStatus status = stream.Status("watcher");
        Assert.True(status.Subscribed);
        Assert.Equal(3, status.Received);
        Assert.Equal(3, status.Delivered);
        Assert.Equal(0, status.Dropped);
    }

    [Fact]
    public void A_slow_subscriber_loses_the_oldest_events_and_is_warned_once()
    {
        var log = new RecordingExtensionLog();
        using var stream = new SessionEventStream(log);
        using var gate = new ManualResetEventSlim(false);
        var observer = new RecordingObserver { BlockFirst = gate };

        using IDisposable subscription = stream.Subscribe("slow", observer);

        // The first event is taken off the queue and blocks in OnNext; everything published after it
        // fills the bounded queue and then evicts the oldest.
        stream.Publish(new RunStarted("run-0"));
        Assert.True(observer.WaitFor(events => events.Count == 1, Wait));
        int overflow = 4;
        for (var index = 1; index <= SessionEventStream.SubscriberQueueCapacity + overflow; index++)
        {
            stream.Publish(
                new TextMessageContent("run-0", "m1", index.ToString(CultureInfo.InvariantCulture))
            );
        }

        gate.Set();
        Assert.True(
            observer.WaitFor(
                events => events.Count == SessionEventStream.SubscriberQueueCapacity + 1,
                Wait
            )
        );

        // The first delivered event is the one that blocked; the next is the oldest survivor.
        var delivered = observer.Events.OfType<TextMessageContent>().ToList();
        Assert.Equal((overflow + 1).ToString(CultureInfo.InvariantCulture), delivered[0].Text);
        ExtensionEventStreamStatus status = stream.Status("slow");
        Assert.Equal(overflow, status.Dropped);
        Assert.Equal(SessionEventStream.SubscriberQueueCapacity + 1, status.Delivered);
        Assert.Equal(SessionEventStream.SubscriberQueueCapacity + overflow + 1, status.Received);
        Assert.Contains(log.Messages, message =>
            message.Contains("extension 'slow' fell behind", StringComparison.Ordinal)
        );
        Assert.Equal(
            1,
            log.Messages.Count(message => message.Contains("fell behind", StringComparison.Ordinal))
        );
    }

    [Fact]
    public void A_throwing_observer_drops_only_its_subscription()
    {
        var log = new RecordingExtensionLog();
        using var stream = new SessionEventStream(log);
        var throwing = new RecordingObserver { ThrowOnFirst = true };
        var healthy = new RecordingObserver();

        using IDisposable broken = stream.Subscribe("broken", throwing);
        using IDisposable intact = stream.Subscribe("healthy", healthy);

        stream.Publish(new RunStarted("run-1"));
        Assert.True(healthy.WaitFor(events => events.Count == 1, Wait));
        Assert.True(
            log.WaitFor(
                () =>
                    log.Messages.Any(message =>
                        message.Contains(
                            "threw from its event subscription",
                            StringComparison.Ordinal
                        )
                    ),
                Wait
            )
        );

        stream.Publish(new RunFinished("run-1", "stop"));
        Assert.True(healthy.WaitFor(events => events.Count == 2, Wait));
        Assert.Single(throwing.Events);
        Assert.False(stream.Status("broken").Subscribed);
        Assert.True(stream.Status("healthy").Subscribed);
        Assert.False(healthy.Completed);
    }

    [Fact]
    public async Task Completion_delivers_the_queued_events_then_fires_once()
    {
        var log = new RecordingExtensionLog();
        using var stream = new SessionEventStream(log);
        var observer = new RecordingObserver();
        using IDisposable subscription = stream.Subscribe("watcher", observer);

        stream.Publish(new RunStarted("run-1"));
        stream.Publish(new RunFinished("run-1", "stop"));
        await stream.CompleteExtensionAsync("watcher", TestContext.Current.CancellationToken);
        await stream.CompleteAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, observer.Events.Count);
        Assert.Equal(1, observer.CompletedCount);
        Assert.False(observer.Errored);

        stream.Publish(new RunStarted("run-2"));
        Assert.Equal(2, observer.Events.Count);
    }

    [Fact]
    public void Disposal_stops_delivery_without_a_completion_callback()
    {
        var log = new RecordingExtensionLog();
        using var stream = new SessionEventStream(log);
        var observer = new RecordingObserver();

        IDisposable subscription = stream.Subscribe("watcher", observer);
        stream.Publish(new RunStarted("run-1"));
        Assert.True(observer.WaitFor(events => events.Count == 1, Wait));
        subscription.Dispose();

        stream.Publish(new RunFinished("run-1", "stop"));
        stream.Dispose();

        Assert.Single(observer.Events);
        Assert.Equal(0, observer.CompletedCount);
        Assert.False(stream.Status("watcher").Subscribed);
    }

    [Fact]
    public void A_subscription_made_after_session_end_completes_immediately()
    {
        var log = new RecordingExtensionLog();
        using var stream = new SessionEventStream(log);
        stream.Dispose();

        var observer = new RecordingObserver();
        using IDisposable subscription = stream.Subscribe("late", observer);

        Assert.True(observer.WaitFor(_ => observer.CompletedCount == 1, Wait));
    }

    /// <summary>
    /// One observer with the switches the tests need: a gate that blocks the first callback (to make
    /// the pump slow), a throw on the first event, and synchronized recording so the assertions read
    /// what the pump wrote.
    /// </summary>
    private sealed class RecordingObserver : IObserver<AgentEvent>
    {
        private readonly object _gate = new();
        private readonly List<AgentEvent> _events = [];
        private int _completed;
        private int _errored;

        public ManualResetEventSlim? BlockFirst { get; init; }

        public bool ThrowOnFirst { get; init; }

        public IReadOnlyList<AgentEvent> Events
        {
            get
            {
                lock (_gate)
                {
                    return [.. _events];
                }
            }
        }

        public int CompletedCount => Volatile.Read(ref _completed);

        public bool Errored => Volatile.Read(ref _errored) != 0;

        public bool Completed => CompletedCount > 0;

        public void OnNext(AgentEvent value)
        {
            var block = false;
            var fail = false;
            lock (_gate)
            {
                _events.Add(value);
                block = _events.Count == 1 && BlockFirst is not null;
                fail = _events.Count == 1 && ThrowOnFirst;
            }

            if (block)
            {
                BlockFirst!.Wait(Wait);
            }

            if (fail)
            {
                throw new InvalidOperationException("the observer exploded");
            }
        }

        public void OnError(Exception error) => Interlocked.Increment(ref _errored);

        public void OnCompleted() => Interlocked.Increment(ref _completed);

        /// <summary>Polls until <paramref name="condition"/> holds; the pump runs on its own task.</summary>
        public bool WaitFor(Func<IReadOnlyList<AgentEvent>, bool> condition, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition(Events))
                {
                    return true;
                }

                Thread.Sleep(1);
            }

            return condition(Events);
        }
    }
}

/// <summary>Waits on the recording log, which is written from the pump thread.</summary>
internal static class RecordingLogWait
{
    internal static bool WaitFor(
        this RecordingExtensionLog log,
        Func<bool> condition,
        TimeSpan timeout
    )
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(1);
        }

        return condition();
    }
}
