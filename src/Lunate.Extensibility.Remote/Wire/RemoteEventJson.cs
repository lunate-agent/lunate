using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Lunate.Agent;
using Lunate.Contracts;

namespace Lunate.Extensibility.Remote;

/// <summary>
/// The event wire projection: one <see cref="AgentEvent"/> becomes the one JSON line object the
/// documented <c>--json</c> stream writes, which is what an <c>event/&lt;kind&gt;</c> notification
/// carries (ADR-0040). The switch covers every sealed event record and <see cref="HandledEventTypes"/>
/// pins that set, so a new event type cannot reach the wire unmapped. Pricing needs the session
/// catalog, which this package does not reference, so the caller supplies the optional
/// <c>costUsd</c> per record: <c>--json</c> and the extension host each pass the catalog estimate
/// they hold, and the run stream, the wire and the documented kind vocabulary stay one projection
/// and never a second event model.
/// </summary>
public static class RemoteEventJson
{
    /// <summary>The adapter's additional-usage key for cache-creation tokens.</summary>
    private const string CacheCreationInputTokensKey = "CacheCreationInputTokens";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// The event type each <c>--json</c> kind names, and the one place the kind vocabulary is
    /// written. <see cref="Kinds"/> publishes it for the schema and the wire catalogue.
    /// </summary>
    private static readonly FrozenDictionary<Type, string> KindByType = new Dictionary<Type, string>
    {
        [typeof(RunStarted)] = "run_started",
        [typeof(RunFinished)] = "run_finished",
        [typeof(RunError)] = "run_error",
        [typeof(TextMessageStart)] = "text_message_start",
        [typeof(TextMessageContent)] = "text_message_content",
        [typeof(TextMessageEnd)] = "text_message_end",
        [typeof(ReasoningStart)] = "reasoning_start",
        [typeof(ReasoningMessageContent)] = "reasoning_message_content",
        [typeof(ReasoningEnd)] = "reasoning_end",
        [typeof(ToolCallStart)] = "tool_call_start",
        [typeof(ToolCallArgs)] = "tool_call_args",
        [typeof(ToolCallEnd)] = "tool_call_end",
        [typeof(ToolCallResult)] = "tool_call_result",
        [typeof(ApprovalRequested)] = "approval_requested",
        [typeof(UsageUpdated)] = "usage_updated",
        [typeof(DecisionEvaluated)] = "decision_evaluated",
        [typeof(DecisionUnavailable)] = "decision_unavailable",
        [typeof(DecisionFailed)] = "decision_failed",
        [typeof(ModelRouted)] = "model_routed",
        [typeof(Retrying)] = "retrying",
        [typeof(CompactionApplied)] = "compaction_applied",
        [typeof(StepLimitReached)] = "step_limit_reached",
        [typeof(CostLimitReached)] = "cost_limit_reached",
        [typeof(TimeLimitReached)] = "time_limit_reached",
        [typeof(SteeringInjected)] = "steering_injected",
        [typeof(ImageDescribed)] = "image_described",
        [typeof(ToolProgressUpdate)] = "tool_progress_update",
        [typeof(ToolOutputSpillFailed)] = "tool_output_spill_failed",
        [typeof(TodoUpdated)] = "todo_updated",
    }.ToFrozenDictionary();

    /// <summary>
    /// The event records the projection handles, ordered by name so the set reads the same in every
    /// process; the reflection test pins it against the union.
    /// </summary>
    public static IReadOnlyList<Type> HandledEventTypes { get; } =
    [.. KindByType.Keys.OrderBy(type => type.Name, StringComparer.Ordinal)];

    /// <summary>
    /// The <c>--json</c> kind vocabulary, ordinal-sorted so the published schema and the wire
    /// catalogue are byte-stable across processes (a frozen dictionary's order is not promised).
    /// </summary>
    public static IReadOnlyList<string> Kinds { get; } =
    [.. KindByType.Values.OrderBy(kind => kind, StringComparer.Ordinal)];

    /// <summary>Whether an event record type has a wire projection.</summary>
    public static bool IsMapped(Type eventType) => KindByType.ContainsKey(eventType);

    /// <summary>The <c>--json</c> kind name for an event record type.</summary>
    public static string KindOf(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return KindByType.TryGetValue(eventType, out string? kind)
            ? kind
            : throw new ArgumentException(
                $"Event type '{eventType.Name}' has no JSON mapping.",
                nameof(eventType)
            );
    }

    /// <summary>
    /// Returns the one-line JSON object for an event, including the stamped fields: the exact
    /// object <c>--json</c> writes. <paramref name="costUsd"/> prices a <c>usage_updated</c> record
    /// the way the catalog does — a number when the model prices, absent otherwise — so the same
    /// delegate makes <c>--json</c>'s line and the wire's line byte for byte; null writes the bare
    /// projection.
    /// </summary>
    public static string Serialize(
        AgentEvent agentEvent,
        Func<UsageUpdated, decimal?>? costUsd = null
    )
    {
        string type = KindOf(agentEvent.Kind);
        JsonObject body = Describe(agentEvent);
        if (agentEvent is UsageUpdated usage && costUsd?.Invoke(usage) is { } cost)
        {
            body["costUsd"] = JsonValue.Create(cost);
        }

        var line = new JsonObject { ["type"] = type, ["runId"] = agentEvent.RunId };
        if (agentEvent.SessionId is not null)
        {
            line["sessionId"] = agentEvent.SessionId;
        }

        if (agentEvent.ParentRunId is not null)
        {
            line["parentRunId"] = agentEvent.ParentRunId;
        }

        if (agentEvent.Source != "core")
        {
            line["source"] = agentEvent.Source;
        }

        foreach ((string name, JsonNode? value) in body)
        {
            line[name] = value?.DeepClone();
        }

        return line.ToJsonString(Options);
    }

    /// <summary>Returns the event's own fields as a JSON object, without the stamped fields.</summary>
    public static JsonObject Describe(AgentEvent agentEvent) =>
        agentEvent switch
        {
            RunStarted => new JsonObject(),
            RunFinished e => new JsonObject { ["stopReason"] = e.StopReason },
            RunError e => new JsonObject { ["message"] = e.Message },
            TextMessageStart e => MessageBody(e.MessageId),
            TextMessageContent e => new JsonObject
            {
                ["messageId"] = e.MessageId,
                ["text"] = e.Text,
            },
            TextMessageEnd e => MessageBody(e.MessageId),
            ReasoningStart e => MessageBody(e.MessageId),
            ReasoningMessageContent e => new JsonObject
            {
                ["messageId"] = e.MessageId,
                ["text"] = e.Text,
            },
            ReasoningEnd e => MessageBody(e.MessageId),
            ToolCallStart e => ToolCallBody(e.CallId, e.ParentToolCallId, toolName: e.ToolName),
            ToolCallArgs e => ToolCallBody(e.CallId, e.ParentToolCallId, args: e.Args),
            ToolCallEnd e => ToolCallBody(e.CallId, e.ParentToolCallId),
            ToolCallResult e => ToolResultBody(e),
            ApprovalRequested e => ApprovalBody(e),
            UsageUpdated e => UsageBody(e),
            DecisionEvaluated e => DecisionBody(e),
            DecisionUnavailable e => new JsonObject { ["questionId"] = e.QuestionId },
            DecisionFailed e => new JsonObject
            {
                ["questionId"] = e.QuestionId,
                ["modelId"] = e.ModelId,
                ["reason"] = e.Reason,
            },
            ModelRouted e => new JsonObject
            {
                ["requestedModel"] = e.RequestedModel,
                ["resolvedModel"] = e.ResolvedModel,
            },
            Retrying e => new JsonObject { ["attempt"] = e.Attempt, ["reason"] = e.Reason },
            CompactionApplied e => CompactionBody(e),
            SteeringInjected e => SteeringBody(e),
            ImageDescribed e => new JsonObject
            {
                ["placeholder"] = e.Placeholder,
                ["description"] = e.Description,
                ["profile"] = e.Profile,
                ["modelId"] = e.ModelId,
            },
            StepLimitReached e => new JsonObject { ["maxSteps"] = e.MaxSteps },
            CostLimitReached e => new JsonObject
            {
                ["maxCostUsd"] = e.MaxCostUsd,
                ["spendUsd"] = e.SpendUsd,
            },
            TimeLimitReached e => new JsonObject
            {
                ["maxTimeSeconds"] = e.MaxTime.TotalSeconds,
                ["elapsedSeconds"] = e.Elapsed.TotalSeconds,
            },
            ToolProgressUpdate e => new JsonObject
            {
                ["callId"] = e.CallId,
                ["message"] = e.Message,
            },
            ToolOutputSpillFailed e => new JsonObject { ["message"] = e.Message },
            TodoUpdated e => new JsonObject { ["items"] = TodoBody(e.Items) },
            _ => throw new ArgumentException(
                $"Event type '{agentEvent.Kind.Name}' has no JSON mapping.",
                nameof(agentEvent)
            ),
        };

    private static JsonObject DecisionBody(DecisionEvaluated e)
    {
        var body = new JsonObject
        {
            ["questionId"] = e.QuestionId,
            ["modelId"] = e.ModelId,
            ["method"] = e.Method,
            ["answer"] = e.Answer,
            ["option"] = e.Option,
            ["probability"] = e.Probability,
        };
        if (e.Distribution is { } distribution)
        {
            var values = new JsonArray();
            foreach (DecisionProbability item in distribution)
            {
                values.Add(
                    new JsonObject { ["answer"] = item.Answer, ["probability"] = item.Probability }
                );
            }

            body["distribution"] = values;
        }

        if (e.Confidence is { } confidence)
        {
            body["confidence"] = confidence;
        }

        if (e.ConfidenceThreshold is { } confidenceThreshold)
        {
            body["confidenceThreshold"] = confidenceThreshold;
        }

        if (e.Outcome is { } outcome)
        {
            body["outcome"] = outcome;
        }

        if (e.Strategy is { } strategy)
        {
            body["strategy"] = strategy;
        }

        if (e.Warning is { } warning)
        {
            body["warning"] = warning;
        }

        return body;
    }

    private static JsonObject MessageBody(string messageId) => new() { ["messageId"] = messageId };

    private static JsonObject ApprovalBody(ApprovalRequested e)
    {
        var body = new JsonObject
        {
            ["callId"] = e.CallId,
            ["toolName"] = e.ToolName,
            ["args"] = e.Args,
        };
        if (e.WorkspaceRoot is { Length: > 0 } root)
        {
            body["workspaceRoot"] = root;
        }

        return body;
    }

    private static JsonObject UsageBody(UsageUpdated e)
    {
        var body = new JsonObject { ["usage"] = JsonSerializer.SerializeToNode(e.Usage, Options) };
        // Cache counts are additive metadata next to the provider's raw usage object; unknown
        // counts stay absent rather than becoming zero.
        if (e.Usage.CachedInputTokenCount is { } cacheReadTokens && cacheReadTokens > 0)
        {
            body["cacheReadTokens"] = cacheReadTokens;
        }

        if (
            e.Usage.AdditionalCounts is { } counts
            && counts.TryGetValue(CacheCreationInputTokensKey, out long cacheWriteTokens)
            && cacheWriteTokens > 0
        )
        {
            body["cacheWriteTokens"] = cacheWriteTokens;
        }

        if (e.ModelId is { Length: > 0 } modelId)
        {
            body["modelId"] = modelId;
        }

        return body;
    }

    private static JsonObject ToolCallBody(
        string callId,
        string? parentToolCallId,
        string? toolName = null,
        string? args = null
    )
    {
        var body = new JsonObject { ["callId"] = callId };
        AddParent(body, parentToolCallId);
        if (toolName is not null)
        {
            body["toolName"] = toolName;
        }

        if (args is not null)
        {
            body["args"] = args;
        }

        return body;
    }

    private static JsonObject ToolResultBody(ToolCallResult e)
    {
        var body = new JsonObject { ["callId"] = e.CallId };
        AddParent(body, e.ParentToolCallId);
        body["output"] = e.Output;
        body["isError"] = e.IsError;
        if (e.Details is not null)
        {
            body["details"] = JsonSerializer.SerializeToNode(
                e.Details,
                e.Details.GetType(),
                Options
            );
        }

        return body;
    }

    private static void AddParent(JsonObject body, string? parentToolCallId)
    {
        if (parentToolCallId is not null)
        {
            body["parentToolCallId"] = parentToolCallId;
        }
    }

    private static JsonObject CompactionBody(CompactionApplied e)
    {
        var replaced = new JsonArray();
        foreach (string id in e.ReplacedEntryIds)
        {
            replaced.Add(id);
        }

        return new JsonObject
        {
            ["replacedEntryIds"] = replaced,
            ["estimatedTokensAfter"] = e.EstimatedTokensAfter,
        };
    }

    private static JsonObject SteeringBody(SteeringInjected e)
    {
        var body = new JsonObject();
        if (e.EntryId is not null)
        {
            body["entryId"] = e.EntryId;
        }

        return body;
    }

    private static JsonArray TodoBody(IReadOnlyList<TodoItem> items)
    {
        var body = new JsonArray();
        foreach (TodoItem item in items)
        {
            body.Add(
                new JsonObject
                {
                    ["text"] = item.Text,
                    ["status"] = item.Status switch
                    {
                        TodoItemStatus.Pending => "pending",
                        TodoItemStatus.InProgress => "in_progress",
                        TodoItemStatus.Done => "done",
                        _ => throw new ArgumentOutOfRangeException(
                            nameof(items),
                            item.Status,
                            "Unknown todo status."
                        ),
                    },
                }
            );
        }

        return body;
    }
}
