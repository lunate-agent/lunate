using Lunate.Contracts;
using Microsoft.Extensions.AI;

namespace Lunate.Agent;

/// <summary>
/// The state every event record carries: the run it belongs to and the fields the event channel
/// stamps centrally. The event records inherit it; <see cref="AgentEvent"/> is the union an emitter
/// produces and a consumer matches on (ADR-0043).
/// </summary>
public abstract record AgentEventBase(string RunId)
{
    /// <summary>
    /// The session the event belongs to; stamped centrally by the event channel when the run has a
    /// session attached, so no emitter has to know about sessions.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>The parent run for child runs (subagents later); null for top-level runs.</summary>
    public string? ParentRunId { get; init; }

    /// <summary>The emitter: <c>"core"</c> for built-in events, an extension id for extension events.</summary>
    public string Source { get; init; } = "core";
}

/// <summary>
/// The common base of the Lunate-specific events; mappers forward or filter them generically.
/// </summary>
public abstract record ExtensionEventBase(string RunId) : AgentEventBase(RunId);

public sealed record RunStarted(string RunId) : AgentEventBase(RunId);

public sealed record RunFinished(string RunId, string StopReason) : AgentEventBase(RunId);

public sealed record RunError(string RunId, string Message) : AgentEventBase(RunId);

public sealed record TextMessageStart(string RunId, string MessageId) : AgentEventBase(RunId);

public sealed record TextMessageContent(string RunId, string MessageId, string Text)
    : AgentEventBase(RunId);

public sealed record TextMessageEnd(string RunId, string MessageId) : AgentEventBase(RunId);

/// <summary>Opens one streamed reasoning span; content events follow until <see cref="ReasoningEnd"/>.</summary>
public sealed record ReasoningStart(string RunId, string MessageId) : AgentEventBase(RunId);

public sealed record ReasoningMessageContent(string RunId, string MessageId, string Text)
    : AgentEventBase(RunId);

public sealed record ReasoningEnd(string RunId, string MessageId) : AgentEventBase(RunId);

public sealed record ToolCallStart(string RunId, string CallId, string ToolName)
    : AgentEventBase(RunId)
{
    /// <summary>The parent call id for nested calls; null for top-level calls.</summary>
    public string? ParentToolCallId { get; init; }
}

public sealed record ToolCallArgs(string RunId, string CallId, string Args) : AgentEventBase(RunId)
{
    /// <summary>The parent call id for nested calls; null for top-level calls.</summary>
    public string? ParentToolCallId { get; init; }
}

public sealed record ToolCallEnd(string RunId, string CallId) : AgentEventBase(RunId)
{
    /// <summary>The parent call id for nested calls; null for top-level calls.</summary>
    public string? ParentToolCallId { get; init; }
}

public sealed record ToolCallResult(
    string RunId,
    string CallId,
    string Output,
    bool IsError,
    object? Details = null
) : AgentEventBase(RunId)
{
    /// <summary>The parent call id for nested calls; null for top-level calls.</summary>
    public string? ParentToolCallId { get; init; }

    /// <summary>Whether the tool produced image parts for this result.</summary>
    public bool HasImages { get; init; }
}

public sealed record ApprovalRequested(string RunId, string CallId, string ToolName, string Args)
    : ExtensionEventBase(RunId)
{
    /// <summary>The asking run's workspace root: where the call will act; null when unknown.</summary>
    public string? WorkspaceRoot { get; init; }
}

public sealed record UsageUpdated(string RunId, UsageDetails Usage) : ExtensionEventBase(RunId)
{
    /// <summary>
    /// The resolved model the usage belongs to: the backend for routed calls, the requested model
    /// otherwise; null when the emitter does not know (older streams and test fixtures).
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>The usage, profile, and provider that produced these tokens; null for legacy callers.</summary>
    public ModelUsageAttribution? Attribution { get; init; }

    /// <summary>
    /// The amount the provider reported for this usage in US dollars; null when the provider does
    /// not report a cost and a reader prices the record from the catalog instead.
    /// </summary>
    public double? ProviderCost { get; init; }
}

/// <summary>A validated approval recommendation from the configured decision model.</summary>
public sealed record DecisionEvaluated(
    string RunId,
    string ModelId,
    string Method,
    string Option,
    double Probability
) : ExtensionEventBase(RunId)
{
    /// <summary>The question this result answers; legacy approval events use <c>approval</c>.</summary>
    public string QuestionId { get; init; } = "approval";

    /// <summary>The selected answer; mirrors <see cref="Option"/> for compatibility.</summary>
    public string Answer { get; init; } = Option;

    /// <summary>The full distribution for a routing decision; null for other decision consumers.</summary>
    public IReadOnlyList<DecisionProbability>? Distribution { get; init; }

    /// <summary>The computed confidence for a routing decision.</summary>
    public double? Confidence { get; init; }

    /// <summary>The minimum confidence required to apply a routing decision.</summary>
    public double? ConfidenceThreshold { get; init; }

    /// <summary>The routing outcome: routed, confidence-below-threshold, or shadow.</summary>
    public string? Outcome { get; init; }

    /// <summary>The routing strategy: one choice question or one noul question per profile.</summary>
    public string? Strategy { get; init; }

    /// <summary>A one-time warning about missing routing-profile descriptions.</summary>
    public string? Warning { get; init; }
}

/// <summary>A decision consumer was enabled without a configured decision profile.</summary>
public sealed record DecisionUnavailable(string RunId) : ExtensionEventBase(RunId)
{
    /// <summary>The question that could not be evaluated.</summary>
    public string QuestionId { get; init; } = "approval";
}

/// <summary>A decision model returned a malformed contract twice; this is not a model verdict.</summary>
public sealed record DecisionFailed(string RunId, string ModelId, string Reason)
    : ExtensionEventBase(RunId)
{
    /// <summary>The question whose response remained off-contract.</summary>
    public string QuestionId { get; init; } = "approval";
}

/// <summary>Usage/profile/provider attribution for one recorded model call.</summary>
public sealed record ModelUsageAttribution(
    string Usage,
    string ProfileName,
    string ProviderName,
    string ProviderKind,
    string? DecisionMethod = null,
    double? Probability = null
);

/// <summary>
/// A call routed through a virtual model: the requested (virtual) model id and the resolved
/// backend id, emitted when the resolution happens.
/// </summary>
public sealed record ModelRouted(string RunId, string RequestedModel, string ResolvedModel)
    : ExtensionEventBase(RunId);

public sealed record Retrying(string RunId, int Attempt, string Reason) : ExtensionEventBase(RunId);

/// <summary>
/// A compaction that replaced earlier entries with a summary. <see cref="ReplacedEntryIds"/> lists
/// the session entries the summary replaces; <see cref="EstimatedTokensAfter"/> is the estimated
/// size of the rebuilt request in tokens.
/// </summary>
public sealed record CompactionApplied(
    string RunId,
    IReadOnlyList<string> ReplacedEntryIds,
    int EstimatedTokensAfter
) : ExtensionEventBase(RunId);

public sealed record StepLimitReached(string RunId, int MaxSteps) : ExtensionEventBase(RunId);

/// <summary>
/// A run that reached its configured cost limit. <see cref="MaxCostUsd"/> is the limit and
/// <see cref="SpendUsd"/> the cumulative spend reached when the run stopped.
/// </summary>
public sealed record CostLimitReached(string RunId, decimal MaxCostUsd, decimal SpendUsd)
    : ExtensionEventBase(RunId);

/// <summary>
/// A run that reached its configured wall-clock limit. <see cref="MaxTime"/> is the limit and
/// <see cref="Elapsed"/> the run time reached when the run stopped.
/// </summary>
public sealed record TimeLimitReached(string RunId, TimeSpan MaxTime, TimeSpan Elapsed)
    : ExtensionEventBase(RunId);

/// <summary>
/// A steering message the loop injected into the running history, carrying the appended session
/// entry id (null when no session is attached) and the message text itself. The text rides on the
/// event because a frontend that echoes the message cannot rely on the session entry being visible
/// yet: the echo used to look the entry up, miss it, and drop the line silently.
/// </summary>
public sealed record SteeringInjected(string RunId, string? EntryId, string? Text = null)
    : ExtensionEventBase(RunId);

/// <summary>Streamed progress a tool reports through its context while it runs.</summary>
public sealed record ToolProgressUpdate(string RunId, string CallId, string Message)
    : ExtensionEventBase(RunId);

/// <summary>
/// A best-effort spill write failed; the tool result keeps its count-only marker. Surfaces as a
/// user notice, never as a tool error.
/// </summary>
public sealed record ToolOutputSpillFailed(string RunId, string Message)
    : ExtensionEventBase(RunId);

/// <summary>The status of one todo item; the set is closed so a consumer can render every case.</summary>
public enum TodoItemStatus
{
    Pending,
    InProgress,
    Done,
}

/// <summary>One item of the session's todo list: its model-written text and its status.</summary>
public sealed record TodoItem(string Text, TodoItemStatus Status);

/// <summary>
/// The complete todo list after one successful <c>todo</c> call; a later event replaces the whole
/// list, so a consumer never has to replay earlier updates.
/// </summary>
public sealed record TodoUpdated(string RunId, IReadOnlyList<TodoItem> Items)
    : ExtensionEventBase(RunId);

/// <summary>
/// One event in a run's stream. The type is a C# 15 union over the event records, so the set of
/// kinds is fixed at compile time: no assembly outside this contract can add one, a consumer's
/// <c>switch</c> over the event is checked for exhaustiveness, and <see cref="OfType{T}"/> queries
/// the concrete kinds. Adding a kind is a contract change: it moves the API version and carries a
/// version note for extension authors (ADR-0043).
/// </summary>
public union AgentEvent(
    RunStarted,
    RunFinished,
    RunError,
    TextMessageStart,
    TextMessageContent,
    TextMessageEnd,
    ReasoningStart,
    ReasoningMessageContent,
    ReasoningEnd,
    ToolCallStart,
    ToolCallArgs,
    ToolCallEnd,
    ToolCallResult,
    ApprovalRequested,
    UsageUpdated,
    DecisionEvaluated,
    DecisionUnavailable,
    DecisionFailed,
    ModelRouted,
    Retrying,
    CompactionApplied,
    StepLimitReached,
    CostLimitReached,
    TimeLimitReached,
    SteeringInjected,
    ToolProgressUpdate,
    ToolOutputSpillFailed,
    TodoUpdated
)
{
    /// <summary>The run the event belongs to.</summary>
    public string RunId => ((AgentEventBase)Value!).RunId;

    /// <summary>The session the event belongs to; null when the run has none.</summary>
    public string? SessionId => (Value as AgentEventBase)?.SessionId;

    /// <summary>The parent run for child runs; null for a top-level run.</summary>
    public string? ParentRunId => (Value as AgentEventBase)?.ParentRunId;

    /// <summary>The emitter: <c>"core"</c> for built-in events, an extension id otherwise.</summary>
    public string Source => (Value as AgentEventBase)?.Source ?? "core";

    /// <summary>The concrete event record's type, for a log line or a diagnostic.</summary>
    public Type Kind => Value?.GetType() ?? typeof(AgentEvent);

    public override string ToString() => Value?.ToString() ?? typeof(AgentEvent).Name;

    public override bool Equals(object? obj) =>
        obj is AgentEvent other && Equals(Value, other.Value);

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public static bool operator ==(AgentEvent left, AgentEvent right) => left.Equals(right);

    public static bool operator !=(AgentEvent left, AgentEvent right) => !left.Equals(right);

    /// <summary>
    /// Wraps a record built or re-stamped through <see cref="AgentEventBase"/> — the shape the event
    /// channel stamps a session onto — so emitting through the base type stays one conversion.
    /// </summary>
    public static implicit operator AgentEvent(AgentEventBase value) =>
        value switch
        {
            RunStarted e => new AgentEvent(e),
            RunFinished e => new AgentEvent(e),
            RunError e => new AgentEvent(e),
            TextMessageStart e => new AgentEvent(e),
            TextMessageContent e => new AgentEvent(e),
            TextMessageEnd e => new AgentEvent(e),
            ReasoningStart e => new AgentEvent(e),
            ReasoningMessageContent e => new AgentEvent(e),
            ReasoningEnd e => new AgentEvent(e),
            ToolCallStart e => new AgentEvent(e),
            ToolCallArgs e => new AgentEvent(e),
            ToolCallEnd e => new AgentEvent(e),
            ToolCallResult e => new AgentEvent(e),
            ApprovalRequested e => new AgentEvent(e),
            UsageUpdated e => new AgentEvent(e),
            DecisionEvaluated e => new AgentEvent(e),
            DecisionUnavailable e => new AgentEvent(e),
            DecisionFailed e => new AgentEvent(e),
            ModelRouted e => new AgentEvent(e),
            Retrying e => new AgentEvent(e),
            CompactionApplied e => new AgentEvent(e),
            StepLimitReached e => new AgentEvent(e),
            CostLimitReached e => new AgentEvent(e),
            TimeLimitReached e => new AgentEvent(e),
            SteeringInjected e => new AgentEvent(e),
            ToolProgressUpdate e => new AgentEvent(e),
            ToolOutputSpillFailed e => new AgentEvent(e),
            TodoUpdated e => new AgentEvent(e),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                $"'{value.GetType().Name}' is not a declared event kind."
            ),
        };
}

/// <summary>
/// The extension half of the event union: the kinds a tool or an extension emits, so its emit
/// surface is checked against the set the contract declares (ADR-0043).
/// </summary>
public union ExtensionEvent(
    ApprovalRequested,
    UsageUpdated,
    DecisionEvaluated,
    DecisionUnavailable,
    DecisionFailed,
    ModelRouted,
    Retrying,
    CompactionApplied,
    StepLimitReached,
    CostLimitReached,
    TimeLimitReached,
    SteeringInjected,
    ToolProgressUpdate,
    ToolOutputSpillFailed,
    TodoUpdated
)
{
    /// <summary>The run the event belongs to.</summary>
    public string RunId => ((ExtensionEventBase)Value!).RunId;

    /// <summary>The session the event belongs to; null when the run has none.</summary>
    public string? SessionId => (Value as ExtensionEventBase)?.SessionId;

    /// <summary>The parent run for child runs; null for a top-level run.</summary>
    public string? ParentRunId => (Value as ExtensionEventBase)?.ParentRunId;

    /// <summary>The emitter: an extension id for these events.</summary>
    public string Source => (Value as ExtensionEventBase)?.Source ?? "core";

    /// <summary>The concrete event record's type.</summary>
    public Type Kind => Value?.GetType() ?? typeof(ExtensionEvent);

    public override string ToString() => Value?.ToString() ?? typeof(ExtensionEvent).Name;

    public override bool Equals(object? obj) =>
        obj is ExtensionEvent other && Equals(Value, other.Value);

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public static bool operator ==(ExtensionEvent left, ExtensionEvent right) => left.Equals(right);

    public static bool operator !=(ExtensionEvent left, ExtensionEvent right) =>
        !left.Equals(right);

    /// <summary>Wraps a record built or re-stamped through <see cref="ExtensionEventBase"/>.</summary>
    public static implicit operator ExtensionEvent(ExtensionEventBase value) =>
        value switch
        {
            ApprovalRequested e => new ExtensionEvent(e),
            UsageUpdated e => new ExtensionEvent(e),
            DecisionEvaluated e => new ExtensionEvent(e),
            DecisionUnavailable e => new ExtensionEvent(e),
            DecisionFailed e => new ExtensionEvent(e),
            ModelRouted e => new ExtensionEvent(e),
            Retrying e => new ExtensionEvent(e),
            CompactionApplied e => new ExtensionEvent(e),
            StepLimitReached e => new ExtensionEvent(e),
            CostLimitReached e => new ExtensionEvent(e),
            TimeLimitReached e => new ExtensionEvent(e),
            SteeringInjected e => new ExtensionEvent(e),
            ToolProgressUpdate e => new ExtensionEvent(e),
            ToolOutputSpillFailed e => new ExtensionEvent(e),
            TodoUpdated e => new ExtensionEvent(e),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                $"'{value.GetType().Name}' is not a declared extension event kind."
            ),
        };

    /// <summary>Folds an extension event into the run's event union.</summary>
    public static implicit operator AgentEvent(ExtensionEvent value) =>
        (AgentEvent)(ExtensionEventBase)value.Value!;
}

/// <summary>
/// Type-based queries over the event union. A union's <c>Value</c> is <c>object</c>, so a LINQ
/// <c>OfType</c> over <see cref="AgentEvent"/> would never match; these overloads project the union
/// to its line of records first, which is why the repository's existing
/// <c>events.OfType&lt;ToolCallResult&gt;()</c> keeps working (ADR-0043).
/// </summary>
public static class AgentEventExtensions
{
    /// <summary>Every event whose concrete record is <typeparamref name="T"/>, in order.</summary>
    public static IEnumerable<T> OfType<T>(this IEnumerable<AgentEvent> events)
        where T : AgentEventBase => events.Select(agentEvent => agentEvent.Value).OfType<T>();

    /// <summary>The first event whose concrete record is <typeparamref name="T"/>, or null.</summary>
    public static T? FirstOrDefaultOfType<T>(this IEnumerable<AgentEvent> events)
        where T : AgentEventBase =>
        events.Select(agentEvent => agentEvent.Value).OfType<T>().FirstOrDefault();
}
