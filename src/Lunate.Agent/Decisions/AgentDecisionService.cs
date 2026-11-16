using Lunate.Contracts;
using Microsoft.Extensions.AI;

namespace Lunate.Agent;

internal sealed class AgentDecisionService(
    IChatClient client,
    string modelId,
    string method,
    bool supportsImages = false,
    Action<UsageDetails, string, string?, double?, double?>? recordUsage = null
) : IAgentDecisionService
{
    /// <summary>
    /// Answers question suggestions and extension-initiated decisions through the same engine as
    /// the harness, so a recorder handed in at composition also sees their usage and provider
    /// cost: without it a suggestion's spend never reaches the session, the budget or <c>/cost</c>.
    /// </summary>
    public Task<DecisionResponse> DecideAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default
    ) =>
        DecisionEngine.EvaluateAsync(
            request,
            client,
            modelId,
            method,
            cancellationToken,
            recordUsage,
            supportsImages: supportsImages
        );
}
