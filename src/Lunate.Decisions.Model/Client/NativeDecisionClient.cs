namespace Lunate.Ai.Decisions;

/// <summary>A provider-native decision endpoint exposed by an <c>IChatClient</c>.</summary>
public interface INativeDecisionClient
{
    /// <summary>
    /// Evaluates the original request and returns raw JSON shaped as a <c>results</c> object keyed by
    /// question id; each value has an <c>answer</c> and a <c>probabilities</c> object.
    /// </summary>
    Task<string> GetDecisionAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default
    );
}
