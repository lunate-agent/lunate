using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>The no-op event sink the shell and process suites hand to the agent.</summary>
internal sealed class NullAgentEvents : IAgentEvents
{
    public void Emit(AgentEvent agentEvent) { }
}
