namespace Lunate.Ai.Decisions;

/// <summary>
/// The agent harness's decision service as the host composes it: the bridge
/// <c>Lunate.Extensibility</c> adapts onto the extension-facing <c>IDecisionService</c> without the
/// contract assembly depending on the loop.
/// </summary>
internal interface IAgentDecisionService
{
    Task<DecisionResult> DecideAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default
    );
}
