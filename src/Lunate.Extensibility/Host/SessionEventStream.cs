using Lunate.Agent;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Extensibility;

/// <summary>
/// The observable half of the extension surface (ADR-0040): the session's <see cref="AgentEvent"/>
/// stream, hot and session-scoped, which an extension watches through
/// <see cref="Abstractions.IExtensionContext.Events"/> without gaining any way to change a run.
/// <para>
/// Every subscription gets its own bounded queue and its own pump, so a slow subscriber delays only
/// itself; the publisher never awaits, never calls an observer on its own thread and never throws.
/// On overflow the oldest event is dropped, counted per subscription, warned about once and shown by
/// <c>/extensions</c>. A throw from any observer callback is caught, logged once naming the
/// extension and drops that subscription only. <see cref="IObserver{T}.OnError"/> is never used (a
/// failed run is a <see cref="RunError"/> event); <see cref="IObserver{T}.OnCompleted"/> fires once
/// per subscription at session end or when the extension unloads, after the events already queued.
/// </para>
/// </summary>
public sealed class SessionEventStream : IDisposable
{
    /// <summary>
    /// How many events one subscription may have queued before the oldest is dropped. Large enough
    /// that an observer doing ordinary buffered work keeps up with a streamed turn, small enough
    /// that a stuck one cannot grow session memory without limit.
    /// </summary>
    public const int SubscriberQueueCapacity = 256;

    /// <summary>The cap <see cref="CompleteAllAsync"/> and <see cref="CompleteExtensionAsync"/>
    /// wait for the pumps to finish, so a callback that blocks cannot hold session end open.</summary>
    private static readonly TimeSpan CompletionCap = TimeSpan.FromSeconds(2);

    private readonly IExtensionLog _log;
    private readonly object _gate = new();
    private readonly Dictionary<string, List<Subscription>> _subscriptions = new(
        StringComparer.Ordinal
    );
    private bool _disposed;

    public SessionEventStream(IExtensionLog? log = null) => _log = log ?? NullExtensionLog.Instance;

    /// <summary>
    /// Subscribes one observer to the stream. The returned disposable ends the subscription and its
    /// pump; disposing it never fires <see cref="IObserver{T}.OnCompleted"/>, because the subscriber
    /// itself let go.
    /// </summary>
    public IDisposable Subscribe(string extensionId, IObserver<AgentEvent> observer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionId);
        ArgumentNullException.ThrowIfNull(observer);

        var subscription = new Subscription(this, extensionId, observer);
        bool closed;
        lock (_gate)
        {
            closed = _disposed;
            if (!closed)
            {
                if (!_subscriptions.TryGetValue(extensionId, out List<Subscription>? list))
                {
                    list = [];
                    _subscriptions[extensionId] = list;
                }

                list.Add(subscription);
            }
        }

        if (closed)
        {
            // A subscription made after session end completes at once rather than never answering.
            subscription.Complete();
        }

        return subscription;
    }

    /// <summary>
    /// Offers one event to every live subscription. Enqueueing only: the publisher's thread never
    /// runs an observer and is never delayed by one.
    /// </summary>
    public void Publish(AgentEvent agentEvent)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            foreach (List<Subscription> list in _subscriptions.Values)
            {
                foreach (Subscription subscription in list)
                {
                    subscription.Enqueue(agentEvent);
                }
            }
        }
    }

    /// <summary>
    /// What one extension's subscriptions have seen: whether one is live, how many events were
    /// offered and delivered, and how many were dropped because the queue was full (the
    /// <c>/extensions</c> counters). An extension that never subscribed reads all zeros.
    /// </summary>
    public ExtensionEventStreamStatus Status(string extensionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionId);
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(extensionId, out List<Subscription>? list))
            {
                return new ExtensionEventStreamStatus(extensionId, false, 0, 0, 0);
            }

            var live = false;
            long received = 0;
            long delivered = 0;
            long dropped = 0;
            foreach (Subscription subscription in list)
            {
                SubscriptionSnapshot snapshot = subscription.Snapshot();
                live |= snapshot.Live;
                received += snapshot.Received;
                delivered += snapshot.Delivered;
                dropped += snapshot.Dropped;
            }

            return new ExtensionEventStreamStatus(extensionId, live, received, delivered, dropped);
        }
    }

    /// <summary>
    /// Ends one extension's subscriptions: what is queued is delivered, then
    /// <see cref="IObserver{T}.OnCompleted"/> fires once. The unload and rollback path calls it
    /// before the extension's context is marked disposed.
    /// </summary>
    public ValueTask CompleteExtensionAsync(
        string extensionId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extensionId);
        List<Subscription> subscriptions;
        lock (_gate)
        {
            subscriptions = _subscriptions.TryGetValue(extensionId, out List<Subscription>? list)
                ? [.. list]
                : [];
        }

        return CompleteAsync(subscriptions, cancellationToken);
    }

    /// <summary>Ends every subscription; the session-end path calls it once.</summary>
    public ValueTask CompleteAllAsync(CancellationToken cancellationToken = default)
    {
        List<Subscription> subscriptions;
        lock (_gate)
        {
            _disposed = true;
            subscriptions = [.. _subscriptions.Values.SelectMany(list => list)];
        }

        return CompleteAsync(subscriptions, cancellationToken);
    }

    /// <summary>Completes every subscription without waiting; the host's last-resort teardown.</summary>
    public void Dispose()
    {
        List<Subscription> subscriptions;
        lock (_gate)
        {
            _disposed = true;
            subscriptions = [.. _subscriptions.Values.SelectMany(list => list)];
        }

        foreach (Subscription subscription in subscriptions)
        {
            subscription.Complete();
        }
    }

    private static async ValueTask CompleteAsync(
        IReadOnlyList<Subscription> subscriptions,
        CancellationToken cancellationToken
    )
    {
        if (subscriptions.Count == 0)
        {
            return;
        }

        Task[] pumps = [.. subscriptions.Select(subscription => subscription.Complete())];
        Task finished = Task.WhenAll(pumps);
        await Task.WhenAny(finished, Task.Delay(CompletionCap, cancellationToken))
            .ConfigureAwait(false);
        if (finished.IsCompleted)
        {
            // Observe a fault a pump could not contain, so a completion callback's throw is a
            // logged failure and never an unobserved task.
            await finished.ConfigureAwait(false);
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            if (_subscriptions.TryGetValue(subscription.ExtensionId, out List<Subscription>? list))
            {
                list.Remove(subscription);
                if (list.Count == 0)
                {
                    _subscriptions.Remove(subscription.ExtensionId);
                }
            }
        }
    }

    private void WarnDropped(string extensionId) =>
        _log.Warn(
            $"extension '{extensionId}' fell behind on its event subscription and its oldest events were dropped; the run is unaffected, the count is shown by /extensions. Move durable work off OnNext into a background service, and decisions into a hook."
        );

    private void ReportFailure(string extensionId, Exception exception) =>
        _log.Warn(
            $"extension '{extensionId}' threw from its event subscription's OnNext, so that subscription was dropped; the run and the other subscribers are unaffected. The failure: {exception.GetType().Name}: {exception.Message}"
        );

    private void ReportCompletionFailure(string extensionId, Exception exception) =>
        _log.Warn(
            $"extension '{extensionId}' threw from its event subscription's OnCompleted; the session continues. The failure: {exception.GetType().Name}: {exception.Message}"
        );

    /// <summary>The counters one subscription reports to <see cref="Status"/>.</summary>
    private readonly record struct SubscriptionSnapshot(
        bool Live,
        long Received,
        long Delivered,
        long Dropped
    );

    /// <summary>
    /// One observer's bounded queue and pump. Enqueueing is the publisher's only contact with it:
    /// the pump is where <see cref="IObserver{T}.OnNext"/> runs, on its own task.
    /// </summary>
    private sealed class Subscription(
        SessionEventStream owner,
        string extensionId,
        IObserver<AgentEvent> observer
    ) : IDisposable
    {
        private readonly object _gate = new();
        private readonly Queue<AgentEvent> _pending = new();
        private readonly SemaphoreSlim _signal = new(0, int.MaxValue);
        private readonly TaskCompletionSource _finished = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private Task? _pump;
        private bool _completing;
        private bool _closed;
        private long _received;
        private long _delivered;
        private long _dropped;

        public string ExtensionId => extensionId;

        public void Enqueue(AgentEvent agentEvent)
        {
            var warn = false;
            lock (_gate)
            {
                if (_closed || _completing)
                {
                    return;
                }

                _received++;
                if (_pending.Count >= SubscriberQueueCapacity)
                {
                    _pending.Dequeue();
                    warn = ++_dropped == 1;
                }

                _pending.Enqueue(agentEvent);
                _pump ??= Task.Run(PumpAsync);
            }

            _signal.Release();
            if (warn)
            {
                owner.WarnDropped(extensionId);
            }
        }

        /// <summary>Requests completion and returns the pump, which finishes after the queue drains.</summary>
        public Task Complete()
        {
            lock (_gate)
            {
                if (_closed)
                {
                    return Task.CompletedTask;
                }

                _completing = true;
                _pump ??= Task.Run(PumpAsync);
            }

            _signal.Release();
            return _finished.Task;
        }

        /// <summary>Ends the subscription without a completion callback; the subscriber let go.</summary>
        public void Dispose()
        {
            bool notify;
            lock (_gate)
            {
                notify = !_closed;
                _closed = true;
                _pending.Clear();
            }

            if (notify)
            {
                _signal.Release();
            }

            owner.Remove(this);
        }

        public SubscriptionSnapshot Snapshot()
        {
            lock (_gate)
            {
                return new SubscriptionSnapshot(!_closed, _received, _delivered, _dropped);
            }
        }

        private async Task PumpAsync()
        {
            while (true)
            {
                await _signal.WaitAsync().ConfigureAwait(false);

                AgentEvent? next = null;
                var complete = false;
                lock (_gate)
                {
                    if (_closed)
                    {
                        return;
                    }

                    if (_pending.Count > 0)
                    {
                        next = _pending.Dequeue();
                        _delivered++;
                    }
                    else if (_completing)
                    {
                        _closed = true;
                        complete = true;
                    }
                }

                if (next is { } agentEvent)
                {
                    try
                    {
                        observer.OnNext(agentEvent);
                    }
                    catch (Exception exception)
                    {
                        lock (_gate)
                        {
                            _closed = true;
                            _pending.Clear();
                        }

                        owner.ReportFailure(extensionId, exception);
                        owner.Remove(this);
                        _finished.TrySetResult();
                        return;
                    }

                    continue;
                }

                if (!complete)
                {
                    // A wake-up with nothing to take (the signal may outrun the queue): retry.
                    continue;
                }

                try
                {
                    observer.OnCompleted();
                }
                catch (Exception exception)
                {
                    owner.ReportCompletionFailure(extensionId, exception);
                }

                owner.Remove(this);
                _finished.TrySetResult();
                return;
            }
        }
    }
}

/// <summary>
/// One extension's event-stream counters: whether a subscription is live, how many events were
/// offered to it, how many reached <see cref="IObserver{T}.OnNext"/>, and how many were dropped
/// because its queue was full. The <c>/extensions</c> view shows the delivered and dropped counts.
/// </summary>
public readonly record struct ExtensionEventStreamStatus(
    string ExtensionId,
    bool Subscribed,
    long Received,
    long Delivered,
    long Dropped
);
