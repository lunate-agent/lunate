namespace Lunate.Ai.Decisions;

/// <summary>The per-call options a decision client accepts beside the request.</summary>
internal sealed record DecisionOptions(
    string? ModelId = null,
    TimeSpan? Timeout = null,
    string? SessionId = null,
    string? RunId = null
);
