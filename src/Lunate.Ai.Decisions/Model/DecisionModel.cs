using System.Text.Json;
using Lunate.Contracts;
using Microsoft.Extensions.AI;

namespace Lunate.Ai.Decisions;

/// <summary>A batch of independent questions evaluated against one text or JSON state.</summary>
/// <remarks>
/// <paramref name="ModelId"/> is the caller's hint; the resolved model travels in
/// <see cref="DecisionOptions.ModelId"/> and wins, because a routed call may resolve another id.
/// </remarks>
public sealed record DecisionRequest(
    JsonElement State,
    IReadOnlyList<DecisionQuestion> Questions,
    string? ModelId = null
);

/// <summary>One named decision question; choice answers and score levels retain their declared order.</summary>
public sealed record DecisionQuestion(
    string Id,
    string Instructions,
    DecisionQuestionKind Kind,
    IReadOnlyList<string>? Answers = null,
    IReadOnlyDictionary<string, string>? Criteria = null,
    string? JsonPath = null,
    JsonElement? State = null
);

/// <summary>The four question kinds supported by the decision primitive.</summary>
public enum DecisionQuestionKind
{
    YesNo,
    Choice,
    Score,
    Box,
}

/// <summary>The per-call options a decision client accepts beside the request.</summary>
public sealed record DecisionOptions(
    string? ModelId = null,
    TimeSpan? Timeout = null,
    string? SessionId = null,
    string? RunId = null
);

/// <summary>A normalized image rectangle and the model's confidence in its placement.</summary>
public sealed record DecisionBox(double Top, double Bottom, double Left, double Right);

/// <summary>A validated answer and the provenance of its model exchange.</summary>
public sealed record DecisionAnswer(
    string Answer,
    double Probability,
    IReadOnlyList<DecisionProbability> Distribution,
    string ModelId,
    string Method,
    double Confidence,
    DecisionBox? Box = null,
    double? ExpectedScore = null
);

/// <summary>The tokens and provider cost one decision exchange recorded.</summary>
public sealed record DecisionUsage(
    long? InputTokens = null,
    long? OutputTokens = null,
    double? Cost = null,
    bool IsProviderReported = false
)
{
    /// <summary>
    /// The provider's own usage sidecar when one adds cost detail: OpenCode reports its billing
    /// source (its own balance or a marketplace) and list cost in <c>usage.opencode</c>.
    /// </summary>
    public OpencodeUsage? Opencode { get; init; }

    public sealed record OpencodeUsage(string? BillingSource, double? ListCost);
}

/// <summary>Validated answers keyed by question id, with what the exchange cost.</summary>
public sealed record DecisionResult(
    IReadOnlyDictionary<string, DecisionAnswer> Results,
    DecisionUsage? Usage = null,
    IReadOnlyList<UsageDetails>? ChatUsageAttempts = null
);

/// <summary>One paid chat attempt and the model that answered it.</summary>
internal sealed record DecisionUsageAttempt(UsageDetails Usage, string? ModelId);

/// <summary>
/// A decision that never matched the contract, carrying every attempt's usage so the caller can
/// record the tokens the provider already charged before the failure propagates. The engine wraps
/// it in the <see cref="InvalidDataException"/> the decision consumers already handle.
/// </summary>
internal sealed class DecisionUsageAttemptsException(
    string message,
    IReadOnlyList<DecisionUsageAttempt> attempts
) : Exception(message)
{
    public IReadOnlyList<DecisionUsageAttempt> Attempts { get; } = attempts;
}
