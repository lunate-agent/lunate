using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Lunate.Ai.Decisions;

internal interface IDecisionClient
{
    Task<DecisionResult> GetDecisionAsync(
        DecisionRequest request,
        DecisionOptions? options = null,
        CancellationToken cancellationToken = default
    );

    object? GetService(Type serviceType);
}

internal class DelegatingDecisionClient(IDecisionClient innerClient) : IDecisionClient
{
    protected IDecisionClient InnerClient { get; } =
        innerClient ?? throw new ArgumentNullException(nameof(innerClient));

    public virtual Task<DecisionResult> GetDecisionAsync(
        DecisionRequest request,
        DecisionOptions? options = null,
        CancellationToken cancellationToken = default
    ) => InnerClient.GetDecisionAsync(request, options, cancellationToken);

    public virtual object? GetService(Type serviceType) => InnerClient.GetService(serviceType);
}

internal sealed record DecisionRequest(
    string ModelId,
    JsonElement State,
    IReadOnlyList<DecisionQuestion> Questions
);

internal sealed record DecisionQuestion(
    string Id,
    string Instructions,
    DecisionQuestionKind Kind,
    IReadOnlyList<string>? Answers = null,
    IReadOnlyDictionary<string, string>? Criteria = null,
    string? JsonPath = null,
    JsonElement? State = null
);

internal enum DecisionQuestionKind
{
    Noul,
    Choice,
    Score,
    Box,
}

internal sealed record DecisionOptions(
    string? ModelId = null,
    TimeSpan? Timeout = null,
    string? SessionId = null,
    string? RunId = null
);

internal sealed record DecisionProbability(string Answer, double Probability);

internal sealed record DecisionBox(double Top, double Bottom, double Left, double Right);

internal sealed record DecisionAnswer(
    string Answer,
    double Probability,
    IReadOnlyList<DecisionProbability> Distribution,
    string ModelId,
    string Method,
    double Confidence,
    DecisionBox? Box = null,
    double? ExpectedScore = null
);

internal sealed record DecisionUsage(
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

    internal sealed record OpencodeUsage(string? BillingSource, double? ListCost);
}

internal sealed record DecisionResult(
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

internal static class DecisionClientServiceExtensions
{
    public static TService? GetService<TService>(this IDecisionClient client)
        where TService : class => client.GetService(typeof(TService)) as TService;
}
