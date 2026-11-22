using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Lunate.Agent;

public sealed partial class AgentHarness
{
    /// <summary>
    /// The most read-only tool bodies one turn may run at once. The value is deliberately small: it
    /// hides per-call latency without turning a turn into an unbounded fan-out.
    /// </summary>
    internal const int MaxParallelReadOnlyCalls = 4;

    private static readonly string[] FileReadOnlyToolNames = ["read", "grep", "find", "ls"];

    private static readonly string[] WebReadOnlyToolNames = ["web_search", "web_fetch"];

    /// <summary>
    /// Whether a turn's calls may run as one concurrent read-only batch. A single call keeps the
    /// serial path, and any tool outside the reviewed read-only set keeps the whole batch serial.
    /// </summary>
    private bool CanRunReadOnlyBatch(IReadOnlyList<FunctionCallContent> calls)
    {
        if (calls.Count < 2)
        {
            return false;
        }

        foreach (FunctionCallContent call in calls)
        {
            if (call.Name is not { } name || _tools.Find(name) is not { } tool)
            {
                return false;
            }

            if (!IsParallelReadOnly(tool))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsParallelReadOnly(ITool tool)
    {
        if (tool.Concurrency != ToolConcurrency.Parallel)
        {
            return false;
        }

        // The web tools declare Execute risk so the approval policy applies to network access.
        if (WebReadOnlyToolNames.Contains(tool.Name, StringComparer.Ordinal))
        {
            return true;
        }

        return tool.Risk == ToolRisk.ReadOnly
            && FileReadOnlyToolNames.Contains(tool.Name, StringComparer.Ordinal);
    }

    /// <summary>
    /// Runs one all-read-only turn batch: start events in call order, preparation (hooks and
    /// approval) in call order, then the approved bodies concurrently under the cap, and finally
    /// each result in call order. History, session and admission stay on this loop's thread.
    /// </summary>
    private async Task ExecuteReadOnlyBatchAsync(
        string runId,
        IReadOnlyList<FunctionCallContent> calls,
        AgentEventChannel channel,
        CancellationToken ct
    )
    {
        ParallelCall[] entries = new ParallelCall[calls.Count];
        for (int index = 0; index < calls.Count; index++)
        {
            FunctionCallContent call = calls[index];
            string callId = call.CallId ?? string.Empty;
            string toolName = call.Name ?? string.Empty;
            Activity? activity = StartToolActivity(toolName, callId);
            channel.Emit(new ToolCallStart(runId, callId, toolName));
            (string argsJson, JsonElement args, string? argumentsError) = ReadArguments(call);
            channel.Emit(new ToolCallArgs(runId, callId, argsJson));
            channel.Emit(new ToolCallEnd(runId, callId));
            entries[index] = new ParallelCall(
                call,
                callId,
                toolName,
                args,
                argumentsError,
                _tools.Find(toolName),
                activity
            );
        }

        try
        {
            for (int index = 0; index < entries.Length; index++)
            {
                ParallelCall entry = entries[index];
                if (entry.ArgumentsError is { } argumentsError)
                {
                    entry.Result = new ToolResult(
                        $"Invalid JSON arguments for '{entry.ToolName}': {argumentsError}. Fix the arguments and retry.",
                        IsError: true
                    );
                    continue;
                }

                if (entry.Tool is not { } tool)
                {
                    entry.Result = UnknownTool(entry.ToolName);
                    continue;
                }

                (JsonElement approvedArgs, ToolResult? blocked) = await PrepareToolCallAsync(
                    tool,
                    runId,
                    entry.CallId,
                    entry.Args,
                    channel,
                    ct
                );
                entry.ApprovedArgs = approvedArgs;
                entry.Result = blocked;
            }

            using var semaphore = new SemaphoreSlim(MaxParallelReadOnlyCalls);
            foreach (ParallelCall entry in entries)
            {
                if (entry.Result is not null || entry.Tool is null)
                {
                    continue;
                }

                entry.NestedCalls = _options.Session is null ? null : [];
                entry.Body = RunParallelBodyAsync(entry, runId, semaphore, ct);
            }

            // Every body has settled before any result is emitted, so completion order cannot leak
            // into the event stream.
            await Task.WhenAll(
                entries.Where(entry => entry.Body is not null).Select(entry => entry.Body!)
            );

            for (int index = 0; index < entries.Length; index++)
            {
                ParallelCall entry = entries[index];
                if (entry.Result is { } prepared)
                {
                    await CompleteBatchCallAsync(runId, entry, prepared, channel);
                    continue;
                }

                ParallelBodyOutcome outcome = await entry.Body!;
                if (outcome.Error is OperationCanceledException && ct.IsCancellationRequested)
                {
                    await CompleteCancelledAsync(runId, entries, channel);
                    throw outcome.Error;
                }

                if (outcome.Error is { } error)
                {
                    entry.Result = new ToolResult(
                        $"Tool '{entry.ToolName}' failed: {error.Message}. Fix the call or try a different approach.",
                        IsError: true
                    );
                }
                else
                {
                    entry.Result = outcome.Result
                        ?? new ToolResult(
                            $"Tool '{entry.ToolName}' returned no result. Fix the call or try a different approach.",
                            IsError: true
                        );
                }

                ToolResult result = await ApplyToolResultReadyAsync(
                    entry.Tool!,
                    runId,
                    entry.CallId,
                    entry.ApprovedArgs,
                    entry.Result,
                    ct
                );
                await CompleteBatchCallAsync(runId, entry, result, channel);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await CompleteCancelledAsync(runId, entries, channel);
            throw;
        }
        catch
        {
            // A completion fault (for example a session append) must not leave a started call
            // without its result. The outer repair still appends the history for every call that
            // remains dangling.
            foreach (ParallelCall entry in entries)
            {
                if (entry.Completed)
                {
                    continue;
                }

                channel.Emit(
                    new ToolCallResult(
                        runId,
                        entry.CallId,
                        SyntheticOutput(entry.Call, cancelled: false),
                        IsError: true
                    )
                );
                entry.Activity?.SetTag(AgentTelemetry.ToolIsErrorAttribute, true);
                entry.Completed = true;
            }

            throw;
        }
        finally
        {
            foreach (ParallelCall entry in entries)
            {
                entry.Activity?.Dispose();
            }
        }
    }

    private async Task<ParallelBodyOutcome> RunParallelBodyAsync(
        ParallelCall entry,
        string runId,
        SemaphoreSlim semaphore,
        CancellationToken ct
    )
    {
        try
        {
            await semaphore.WaitAsync(ct);
        }
        catch (OperationCanceledException exception)
        {
            return new ParallelBodyOutcome(null, exception);
        }

        try
        {
            ToolResult result = await ExecuteToolBodyAsync(
                entry.Tool!,
                runId,
                entry.CallId,
                entry.ApprovedArgs,
                entry.BodyEvents,
                nestedDepth: 1,
                entry.NestedCalls,
                ct
            );
            return new ParallelBodyOutcome(result, null);
        }
        catch (Exception exception)
        {
            return new ParallelBodyOutcome(null, exception);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Emits and appends one batch call's result in call order. The body events buffered for this
    /// call are replayed first, so a tool's own events stay attributed to its call.
    /// </summary>
    private Task CompleteBatchCallAsync(
        string runId,
        ParallelCall entry,
        ToolResult result,
        AgentEventChannel channel
    )
    {
        if (entry.Completed)
        {
            return Task.CompletedTask;
        }

        entry.Activity?.SetTag(AgentTelemetry.ToolIsErrorAttribute, result.IsError);
        RecordObservation(entry.CallId, result.IsError);
        foreach (AgentEvent buffered in entry.BodyEvents.Drain())
        {
            channel.Emit(buffered);
        }

        if (_options.Session is { } session && entry.NestedCalls is { Count: > 0 } nestedCalls)
        {
            session.AppendNestedCalls(entry.CallId, nestedCalls);
        }

        string output = AdmitToolOutput(
            runId,
            entry.CallId,
            entry.ToolName,
            entry.Args,
            result,
            channel
        );
        channel.Emit(
            new ToolCallResult(runId, entry.CallId, output, result.IsError, result.Details)
            {
                HasImages = result.Images is { Count: > 0 },
            }
        );
        // The result event is the point after which a completion fault must not synthesize a
        // second result for this call; history append failures are the outer repair's concern.
        entry.Completed = true;
        AppendHistoryMessage(
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent(entry.CallId, output)])
        );
        if (result.Images is { Count: > 0 } images)
        {
            AppendHistoryMessage(BuildToolImageMessage(entry.ToolName, images));
        }

        _danglingCalls.Remove(entry.Call);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gives every started call that has no result one last result, so a cancellation still leaves
    /// a valid event stream and a valid history. Work that already completed keeps its real result.
    /// </summary>
    private async Task CompleteCancelledAsync(
        string runId,
        IReadOnlyList<ParallelCall> entries,
        AgentEventChannel channel
    )
    {
        foreach (ParallelCall entry in entries)
        {
            if (entry.Completed)
            {
                continue;
            }

            ToolResult result;
            if (entry.Result is { } prepared)
            {
                result = prepared;
            }
            else if (entry.Body is { } body && body.IsCompletedSuccessfully)
            {
                ParallelBodyOutcome outcome = await body;
                result = outcome.Result
                    ?? new ToolResult(SyntheticOutput(entry.Call, cancelled: true), IsError: true);
            }
            else
            {
                if (entry.Body is { } pending)
                {
                    try
                    {
                        await pending;
                    }
                    catch
                    {
                        // The synthetic result below is the run's answer for this call.
                    }
                }

                result = new ToolResult(
                    SyntheticOutput(entry.Call, cancelled: true),
                    IsError: true
                );
            }

            await CompleteBatchCallAsync(runId, entry, result, channel);
        }
    }

    private static Activity? StartToolActivity(string toolName, string callId)
    {
        Activity? activity = AgentTelemetry.Source.StartActivity(
            $"{AgentTelemetry.ExecuteToolOperation} {toolName}",
            ActivityKind.Internal
        );
        activity?.SetTag(
            AgentTelemetry.OperationNameAttribute,
            AgentTelemetry.ExecuteToolOperation
        );
        activity?.SetTag(AgentTelemetry.ToolNameAttribute, toolName);
        activity?.SetTag(AgentTelemetry.ToolCallIdAttribute, callId);
        return activity;
    }

    private sealed class ParallelCall(
        FunctionCallContent call,
        string callId,
        string toolName,
        JsonElement args,
        string? argumentsError,
        ITool? tool,
        Activity? activity
    )
    {
        public FunctionCallContent Call { get; } = call;

        public string CallId { get; } = callId;

        public string ToolName { get; } = toolName;

        public JsonElement Args { get; } = args;

        public string? ArgumentsError { get; } = argumentsError;

        public ITool? Tool { get; } = tool;

        public Activity? Activity { get; } = activity;

        public JsonElement ApprovedArgs { get; set; } = args;

        public ToolResult? Result { get; set; }

        public List<SessionNestedCall>? NestedCalls { get; set; }

        public AgentEventChannel BodyEvents { get; } = new();

        public Task<ParallelBodyOutcome>? Body { get; set; }

        public bool Completed { get; set; }
    }

    private sealed record ParallelBodyOutcome(ToolResult? Result, Exception? Error);
}
