using Microsoft.Extensions.AI;

namespace Lunate.Ai.Decisions;

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
