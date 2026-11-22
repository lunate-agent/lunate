using System.Globalization;
using System.Text;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Extensibility.Remote;

// The observable half of the wire (ADR-0040): after a successful handshake the remote extension
// subscribes to the session's event stream once and projects the manifest's watch list to the child
// as `event/<kind>` notifications. One pump and one bounded queue keep the run's path free of the
// child's stdin, and the drop is counted and warned once instead of silently lost.
public sealed partial class RemoteExtension
{
    private RemoteEventProjection? _eventProjection;

    /// <summary>
    /// Resolves the kinds the projection sends from the manifest's watch list and the child's
    /// accepted list, warning once for a manifest kind the child does not accept. An empty result
    /// means nothing is sent: the child accepts no events, or the manifest declares none.
    /// </summary>
    private IReadOnlyList<string> ResolveEventKinds(InitializeResult answer)
    {
        IReadOnlyList<string> declared = _options.Manifest.Events;
        if (declared.Count == 0)
        {
            return [];
        }

        if (answer.ProtocolVersion < RemoteProtocol.EventsVersion)
        {
            // The handshake refuses this before it reaches here; the guard is the one place the
            // rule is stated, so a future caller cannot project to a child that cannot receive it.
            return [];
        }

        IReadOnlyList<string> accepted = answer.Events;
        if (accepted.Count == 0)
        {
            _options.Context.Log.Warn(
                $"extension '{Id}': the manifest declares 'events' but the child accepts no event kinds; it will be sent nothing. Declare the kinds in the initialize result's 'events' (or [\"*\"]), which needs protocol version {RemoteProtocol.EventsVersion.ToString(CultureInfo.InvariantCulture)}."
            );
            return [];
        }

        bool acceptedAll = accepted.Contains("*", StringComparer.Ordinal);
        if (declared.Contains("*", StringComparer.Ordinal))
        {
            return acceptedAll ? ["*"] : accepted;
        }

        HashSet<string> acceptedKinds = [.. accepted];
        List<string> missing =
        [
            .. declared.Where(kind => !acceptedAll && !acceptedKinds.Contains(kind)),
        ];
        foreach (string kind in missing)
        {
            _options.Context.Log.Warn(
                $"extension '{Id}': the manifest declares event '{kind}' but the child does not accept it; the kind is dropped from the wire projection."
            );
        }

        return acceptedAll ? declared : [.. declared.Where(acceptedKinds.Contains)];
    }

    /// <summary>
    /// Starts the projection for a started child, replacing one left by an earlier child of the same
    /// session. Called only after a successful handshake and never before: a subscription that
    /// outlived its wire would drop every event for a child that cannot receive it.
    /// </summary>
    private void StartEventProjection(RemoteWire wire, InitializeResult answer)
    {
        StopEventProjection();
        IReadOnlyList<string> kinds = ResolveEventKinds(answer);
        if (kinds.Count == 0)
        {
            return;
        }

        _eventProjection = new RemoteEventProjection(
            Id,
            _options.Context.Log,
            wire,
            kinds,
            _options.MaxLineBytes,
            _options.Context.Events,
            () => Interlocked.Exchange(ref _eventProjection, null)
        );
    }

    /// <summary>Ends the projection without a completion callback; the child it fed is going away.</summary>
    private void StopEventProjection() =>
        Interlocked.Exchange(ref _eventProjection, null)?.Dispose();
}

/// <summary>
/// One started child's projection: the subscription to the session's event stream plus the bounded
/// queue and the single pump that serialises each accepted event and writes it as an
/// <c>event/&lt;kind&gt;</c> notification. <see cref="OnNext"/> only enqueues, so the stream's pump
/// never waits on the child's stdin; overflow drops the oldest event, counts it and warns once, and
/// a line over the wire's cap is dropped with one warning naming the kind rather than breaking the
/// wire.
/// </summary>
internal sealed class RemoteEventProjection : IObserver<AgentEvent>, IDisposable
{
    private readonly string _extensionId;
    private readonly IExtensionLog _log;
    private readonly RemoteWire _wire;
    private readonly int _maxLineBytes;
    private readonly Action _onStopped;
    private readonly HashSet<string> _kinds;
    private readonly bool _allKinds;
    private readonly object _gate = new();
    private readonly Queue<AgentEvent> _pending = new();
    private readonly SemaphoreSlim _signal = new(0, int.MaxValue);
    private readonly HashSet<string> _oversizedKinds = new(StringComparer.Ordinal);
    private IDisposable? _subscription;
    private Task? _pump;
    private bool _completing;
    private bool _closed;
    private bool _warnedDropped;
    private long _dropped;

    public RemoteEventProjection(
        string extensionId,
        IExtensionLog log,
        RemoteWire wire,
        IReadOnlyList<string> kinds,
        int maxLineBytes,
        IObservable<AgentEvent> events,
        Action onStopped
    )
    {
        _extensionId = extensionId;
        _log = log;
        _wire = wire;
        _maxLineBytes = maxLineBytes;
        _onStopped = onStopped;
        _allKinds = kinds.Contains("*", StringComparer.Ordinal);
        _kinds = _allKinds ? [] : [.. kinds];

        // The subscription is the last thing made, so a throwing Subscribe cannot leave a pump
        // without a source or a half-built projection behind.
        _subscription = events.Subscribe(this);
    }

    /// <summary>How many events the projection has dropped (overflow or an oversized line).</summary>
    public long Dropped
    {
        get
        {
            lock (_gate)
            {
                return _dropped;
            }
        }
    }

    public void OnNext(AgentEvent value)
    {
        if (
            !RemoteEventJson.IsMapped(value.Kind)
            || (!_allKinds && !_kinds.Contains(RemoteEventJson.KindOf(value.Kind)))
        )
        {
            return;
        }

        bool warn = false;
        lock (_gate)
        {
            if (_closed || _completing)
            {
                return;
            }

            if (_pending.Count >= RemoteProtocol.EventQueueCapacity)
            {
                _pending.Dequeue();
                _dropped++;
                warn = !_warnedDropped;
                _warnedDropped = true;
            }

            _pending.Enqueue(value);
            _pump ??= Task.Run(PumpAsync);
        }

        _signal.Release();
        if (warn)
        {
            _log.Warn(
                $"extension '{_extensionId}' fell behind on its event projection and its oldest events were dropped; the run is unaffected, and the wire keeps sending the newest events. Move durable work off OnNext in the child, and decisions into a hook."
            );
        }
    }

    public void OnError(Exception error) => Stop();

    public void OnCompleted()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _completing = true;
            _pump ??= Task.Run(PumpAsync);
        }

        _signal.Release();
    }

    /// <summary>
    /// Ends the projection: the subscription is disposed first so no new event is offered, then the
    /// queue is cleared and the pump is woken. A write already in flight ends on its own when the
    /// wire closes.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _pending.Clear();
        }

        Release();
    }

    private void Stop() => Dispose();

    /// <summary>
    /// Releases the subscription and the owner's reference once, whichever path ended the
    /// projection: an unload, a dead wire, or the stream's own completion after the queue drained.
    /// </summary>
    private void Release()
    {
        _subscription?.Dispose();
        _subscription = null;
        _signal.Release();
        _onStopped();
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
                }
                else if (_completing)
                {
                    _closed = true;
                    complete = true;
                }
            }

            if (next is { } agentEvent)
            {
                if (!await WriteAsync(agentEvent).ConfigureAwait(false))
                {
                    Dispose();
                    return;
                }

                continue;
            }

            if (!complete)
            {
                // A wake-up with nothing to take (the signal may outrun the queue): retry.
                continue;
            }

            // The stream completed the subscription and the queue is drained: release what the
            // projection held without re-entering the closed guard.
            Release();
            return;
        }
    }

    /// <summary>Writes one event; false ends the projection (the wire is gone or the line is refused).</summary>
    private async ValueTask<bool> WriteAsync(AgentEvent agentEvent)
    {
        string line;
        try
        {
            line = RemoteEventJson.Serialize(agentEvent);
        }
        catch (ArgumentException exception)
        {
            _log.Warn(
                $"extension '{_extensionId}': an event of kind '{agentEvent.Kind.Name}' has no wire projection ({exception.Message}); it was dropped and the wire continues."
            );
            return true;
        }

        int size = Encoding.UTF8.GetByteCount(line);
        if (size > _maxLineBytes)
        {
            bool first;
            lock (_gate)
            {
                first = _oversizedKinds.Add(RemoteEventJson.KindOf(agentEvent.Kind));
                _dropped++;
            }

            if (first)
            {
                _log.Warn(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"extension '{_extensionId}': an event of kind '{RemoteEventJson.KindOf(agentEvent.Kind)}' is {size} bytes, over the wire's {_maxLineBytes}-byte line cap; it was dropped and the wire continues. A tool result that large does not fit the projection."
                    )
                );
            }

            return true;
        }

        try
        {
            using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(
                line
            );
            await _wire
                .NotifyAsync(
                    RemoteProtocol.EventMethod(RemoteEventJson.KindOf(agentEvent.Kind)),
                    document.RootElement.Clone(),
                    CancellationToken.None
                )
                .ConfigureAwait(false);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (RemoteProtocolException exception)
        {
            // The wire refused the frame (for example the negotiated cap shrank under the options):
            // dropping the projection is the child's loss, never the run's.
            _log.Warn(
                $"extension '{_extensionId}': the wire refused an event notification: {exception.Message} The projection stopped; the run continues."
            );
            return false;
        }
        catch (Exception)
        {
            // The channel is gone (the child exited or the wire closed): the supervisor reports it.
            return false;
        }
    }
}
