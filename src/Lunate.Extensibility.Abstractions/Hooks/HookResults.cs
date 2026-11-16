using System.Text.Json;

namespace Lunate.Extensibility.Abstractions;

// Every result family below is a C# 15 union over its cases: a handler returns the case it means and
// the host matches the union, so the set of variants is fixed at compile time and a dispatch switch is
// checked for exhaustiveness. Adding a case is a contract change — it moves the API version and carries
// a version note for extension authors (ADR-0043). The wire never serializes the union wrapper: the
// remote protocol's hand-written `outcome` codec (HookResultCodec) writes the case, so the JSON is
// unchanged (System.Text.Json cannot deserialize a union without a custom classifier anyway).

/// <summary>The outcome of the project-trust hook.</summary>
public union ProjectTrustResult(ProjectTrustResult.Allow, ProjectTrustResult.Deny) : IHookResult
{
    /// <summary>Trust the project extension and record its content hash.</summary>
    public sealed record Allow : IHookResult;

    /// <summary>Refuse the project extension.</summary>
    public sealed record Deny(string Reason) : IHookResult;
}

/// <summary>The outcome of the raw-input hook.</summary>
public union InputReceivedResult(
    InputReceivedResult.PassThrough,
    InputReceivedResult.Transform,
    InputReceivedResult.Consume
) : IHookResult
{
    /// <summary>Keep the text unchanged.</summary>
    public sealed record PassThrough : IHookResult;

    /// <summary>Replace the text; the next handler sees the replacement.</summary>
    public sealed record Transform(string Text) : IHookResult;

    /// <summary>Drop the input; the run does not start.</summary>
    public sealed record Consume : IHookResult;
}

/// <summary>The outcome of the run-starting hook.</summary>
public union RunStartingResult(RunStartingResult.None, RunStartingResult.Apply) : IHookResult
{
    /// <summary>No prompt or tool changes.</summary>
    public sealed record None : IHookResult;

    /// <summary>Prompt-section edits and an optional active-tool selection.</summary>
    public sealed record Apply(
        IReadOnlyList<PromptSectionEdit> SectionEdits,
        IReadOnlyList<string>? ActiveTools
    ) : IHookResult;
}

/// <summary>The request-local context an extension adds; merged with a per-extension budget.</summary>
public sealed record ContextBuildingResult(IReadOnlyList<ContextMessage> AddedMessages)
    : IHookResult
{
    /// <summary>No additions.</summary>
    public static ContextBuildingResult None { get; } = new([]);
}

/// <summary>The outcome of the message-completed hook.</summary>
public union MessageCompletedResult(MessageCompletedResult.Keep, MessageCompletedResult.Replace)
    : IHookResult
{
    /// <summary>Keep the message as produced.</summary>
    public sealed record Keep : IHookResult;

    /// <summary>Replace the message text; the same role is persisted.</summary>
    public sealed record Replace(string Text) : IHookResult;
}

/// <summary>The outcome of the tool-calling hook.</summary>
public union ToolCallingResult(ToolCallingResult.Proceed, ToolCallingResult.Block) : IHookResult
{
    /// <summary>Run the call, optionally with mutated arguments.</summary>
    public sealed record Proceed(JsonElement? Arguments) : IHookResult;

    /// <summary>Block the call with a reason; nothing is approved or executed.</summary>
    public sealed record Block(string Reason) : IHookResult;
}

/// <summary>The tool result after extensions transformed it.</summary>
public sealed record ToolResultReadyResult(
    string Output,
    JsonElement? Data,
    JsonElement? StructuredContent = null
) : IHookResult;

/// <summary>The outcome of the turn-ended hook.</summary>
public union TurnEndedResult(TurnEndedResult.None, TurnEndedResult.TurnEnded) : IHookResult
{
    /// <summary>No entries and no continuation.</summary>
    public sealed record None : IHookResult;

    /// <summary>Session entries to append and an optional continuation request.</summary>
    public sealed record TurnEnded(
        IReadOnlyList<TurnEndedEntry> Entries,
        bool RequestContinuation
    ) : IHookResult;
}

/// <summary>The outcome of the compacting hook.</summary>
public union CompactingResult(CompactingResult.UseDefault, CompactingResult.Provide) : IHookResult
{
    /// <summary>Use the host's default compaction.</summary>
    public sealed record UseDefault : IHookResult;

    /// <summary>Supply the summary to compact with.</summary>
    public sealed record Provide(string Summary) : IHookResult;
}
