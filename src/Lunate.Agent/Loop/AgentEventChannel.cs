using System.Threading.Channels;

namespace Lunate.Agent;

/// <summary>
/// The harness boundary: emits are ordered and non-blocking, and <see cref="Complete"/> ends the stream.
/// Unbounded by design: a run's event volume is bounded by the loop's structure (steps and streamed
/// fragments) and the harness reads continuously, while a bounded channel plus the synchronous
/// <see cref="IAgentEvents.Emit"/> would need a sync-over-async wait that risks deadlocking.
/// </summary>
internal sealed class AgentEventChannel(string? sessionId = null) : IAgentEvents
{
    private readonly Channel<AgentEvent> _channel = Channel.CreateUnbounded<AgentEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
    );

    public void Emit(AgentEvent agentEvent) => _channel.Writer.TryWrite(Stamp(agentEvent));

    public void Complete() => _channel.Writer.TryComplete();

    public IAsyncEnumerable<AgentEvent> ReadAllAsync(
        CancellationToken cancellationToken = default
    ) => _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Stamps the session onto an event that does not carry one. The stamp goes through the record
    /// base, because a union has no state of its own; the conversion back to the union is the one
    /// place an event is re-wrapped.
    /// </summary>
    private AgentEvent Stamp(AgentEvent agentEvent) =>
        sessionId is not null && agentEvent.Value is AgentEventBase { SessionId: null } record
            ? record with
            {
                SessionId = sessionId,
            }
            : agentEvent;
}
