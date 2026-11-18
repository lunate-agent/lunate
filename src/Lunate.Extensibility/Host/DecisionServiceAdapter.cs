using Lunate.Ai.Decisions;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Extensibility;

internal sealed class DecisionServiceAdapter(IAgentDecisionService agent) : IDecisionService
{
    public Task<DecisionResult> DecideAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default
    ) => agent.DecideAsync(request, cancellationToken);
}
