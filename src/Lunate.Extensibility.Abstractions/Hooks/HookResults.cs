using System.Text.Json;

namespace Lunate.Extensibility.Abstractions;

// Every result family below heads a closed set and is declared `closed`: its cases are the whole set,
// so the host's dispatch switch is checked for exhaustiveness and no assembly outside this contract
// can add a variant. Adding a case is a contract change — it moves the API version and carries a
// version note for extension authors (ADR-0042). A `union` is the wrong shape here: its Value is
// object, which would erase the IHookResult marker and the families' value equality, and
// System.Text.Json cannot deserialize one without a custom classifier. Each family keeps its
// private constructor so the compiler's synthesized one stays off the public surface.

/// <summary>The outcome of the project-trust hook.</summary>
public closed record ProjectTrustResult : IHookResult
{
    private ProjectTrustResult() { }

    /// <summary>Trust the project extension and record its content hash.</summary>
    public sealed record Allow : ProjectTrustResult;

    /// <summary>Refuse the project extension.</summary>
    public sealed record Deny(string Reason) : ProjectTrustResult;
}

/// <summary>The outcome of the raw-input hook.</summary>
public closed record InputReceivedResult : IHookResult
{
    private InputReceivedResult() { }

    /// <summary>Keep the text unchanged.</summary>
    public sealed record PassThrough : InputReceivedResult;

    /// <summary>Replace the text; the next handler sees the replacement.</summary>
    public sealed record Transform(string Text) : InputReceivedResult;

    /// <summary>Drop the input; the run does not start.</summary>
    public sealed record Consume : InputReceivedResult;
}

/// <summary>The outcome of the run-starting hook.</summary>
public closed record RunStartingResult : IHookResult
{
    private RunStartingResult() { }

    /// <summary>No prompt or tool changes.</summary>
    public sealed record None : RunStartingResult;

    /// <summary>Prompt-section edits and an optional active-tool selection.</summary>
    public sealed record Apply(
        IReadOnlyList<PromptSectionEdit> SectionEdits,
        IReadOnlyList<string>? ActiveTools
    ) : RunStartingResult;
}

/// <summary>The request-local context an extension adds; merged with a per-extension budget.</summary>
public sealed record ContextBuildingResult(IReadOnlyList<ContextMessage> AddedMessages)
    : IHookResult
{
    /// <summary>No additions.</summary>
    public static ContextBuildingResult None { get; } = new([]);
}

/// <summary>The outcome of the message-completed hook.</summary>
public closed record MessageCompletedResult : IHookResult
{
    private MessageCompletedResult() { }

    /// <summary>Keep the message as produced.</summary>
    public sealed record Keep : MessageCompletedResult;

    /// <summary>Replace the message text; the same role is persisted.</summary>
    public sealed record Replace(string Text) : MessageCompletedResult;
}

/// <summary>The outcome of the tool-calling hook.</summary>
public closed record ToolCallingResult : IHookResult
{
    private ToolCallingResult() { }

    /// <summary>Run the call, optionally with mutated arguments.</summary>
    public sealed record Proceed(JsonElement? Arguments) : ToolCallingResult;

    /// <summary>Block the call with a reason; nothing is approved or executed.</summary>
    public sealed record Block(string Reason) : ToolCallingResult;
}

/// <summary>The tool result after extensions transformed it.</summary>
public sealed record ToolResultReadyResult(
    string Output,
    JsonElement? Data,
    JsonElement? StructuredContent = null
) : IHookResult;

/// <summary>The outcome of the turn-ended hook.</summary>
public closed record TurnEndedResult : IHookResult
{
    private TurnEndedResult() { }

    /// <summary>No entries and no continuation.</summary>
    public sealed record None : TurnEndedResult;

    /// <summary>Session entries to append and an optional continuation request.</summary>
    public sealed record TurnEnded(IReadOnlyList<TurnEndedEntry> Entries, bool RequestContinuation)
        : TurnEndedResult;
}

/// <summary>The outcome of the compacting hook.</summary>
public closed record CompactingResult : IHookResult
{
    private CompactingResult() { }

    /// <summary>Use the host's default compaction.</summary>
    public sealed record UseDefault : CompactingResult;

    /// <summary>Supply the summary to compact with.</summary>
    public sealed record Provide(string Summary) : CompactingResult;
}
