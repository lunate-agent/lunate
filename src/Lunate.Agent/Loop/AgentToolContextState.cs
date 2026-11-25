using Lunate.Ai.Decisions;
using Lunate.Contracts;
using Microsoft.Extensions.AI;

namespace Lunate.Agent;

internal sealed record AgentToolContextState(
    Session? Session,
    IReadLedger? Reads,
    Func<IReadOnlyList<UserQuestion>, CancellationToken, Task<UserQuestionResult>>? AskUserAsync,
    bool QuestionSuggestionsEnabled = false,
    Func<DecisionRequest, CancellationToken, Task<DecisionResult>>? QuestionDecisionAsync = null
)
{
    internal Func<
        DecisionRequest,
        CancellationToken,
        Task<DecisionResult>
    >? DecideAsync { get; init; }

    /// <summary>
    /// Charges one model call a tool made outside the loop to the owning run: the harness stamps
    /// the same <c>AccumulateUsage</c> path the run's own calls use, so the event, the token budget
    /// and the spend all agree. Null outside a run.
    /// </summary>
    internal Action<UsageDetails, string, ModelUsageAttribution>? RecordUsage { get; init; }

    internal static Session? SessionFor(ToolContext context) =>
        (context.AgentState as AgentToolContextState)?.Session;

    internal static IReadLedger? ReadsFor(ToolContext context) =>
        (context.AgentState as AgentToolContextState)?.Reads;

    /// <summary>Reports one tool-made model call's usage; a no-op outside a run.</summary>
    internal static void RecordUsageFor(
        ToolContext context,
        UsageDetails details,
        string modelId,
        ModelUsageAttribution attribution
    ) =>
        (context.AgentState as AgentToolContextState)?.RecordUsage?.Invoke(
            details,
            modelId,
            attribution
        );
}
