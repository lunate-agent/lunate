namespace Lunate.Agent;

/// <summary>
/// The event sink handed to tools: tools may only emit extension events, so anything else is
/// rejected and surfaces as a tool error.
/// </summary>
internal sealed class ExtensionOnlyEventSink(IAgentEvents inner) : IAgentEvents
{
    public void Emit(AgentEvent agentEvent)
    {
        if (agentEvent.Value is not ExtensionEventBase)
        {
            throw new InvalidOperationException(
                $"Tools may only emit extension events; {agentEvent.Kind.Name} is not allowed."
            );
        }

        inner.Emit(agentEvent);
    }
}
