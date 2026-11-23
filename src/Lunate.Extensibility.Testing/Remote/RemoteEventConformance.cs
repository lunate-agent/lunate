using Lunate.Agent;
using Lunate.Contracts;
using Lunate.Extensibility.Remote;
using Microsoft.Extensions.AI;

namespace Lunate.Extensibility.Testing;

/// <summary>One event-wire conformance sample: an event record and its projected kind.</summary>
public sealed record RemoteEventSample(string Name, AgentEvent Event);

/// <summary>
/// The observable event conformance inventory: one sample per <see cref="AgentEvent"/> record and
/// the reflection check that proves the inventory is exhaustive. The projection itself lives in
/// <see cref="RemoteEventJson"/> (the wire package), so a new event type cannot ship without a wire
/// round-trip entry.
/// </summary>
public static class RemoteEventConformance
{
    public static IReadOnlyList<RemoteEventSample> Samples { get; } = BuildSamples();

    /// <summary>The concrete event types the inventory does not cover; empty when complete.</summary>
    public static IReadOnlyList<string> MissingCoverage()
    {
        HashSet<Type> covered = [.. Samples.Select(sample => sample.Event.Kind)];
        List<string> missing = [];
        foreach (Type type in ConcreteEventTypes())
        {
            if (!covered.Contains(type))
            {
                missing.Add(type.FullName ?? type.Name);
            }
        }

        missing.Sort(StringComparer.Ordinal);
        return missing;
    }

    /// <summary>Every concrete event record in the contract's union assembly.</summary>
    public static IReadOnlyList<Type> ConcreteEventTypes() =>
        [
            .. typeof(AgentEvent)
                .Assembly.GetTypes()
                .Where(type =>
                    type.IsSubclassOf(typeof(AgentEventBase))
                    && !type.IsAbstract
                    // The union's case wrappers are compiler-generated; the records are the kinds.
                    && !type.IsDefined(
                        typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute),
                        inherit: false
                    )
                )
                .OrderBy(type => type.Name, StringComparer.Ordinal),
        ];

    private static List<RemoteEventSample> BuildSamples()
    {
        var usage = new UsageDetails
        {
            InputTokenCount = 1_000,
            OutputTokenCount = 500,
            CachedInputTokenCount = 100,
            AdditionalCounts = new AdditionalPropertiesDictionary<long>
            {
                ["CacheCreationInputTokens"] = 50,
            },
        };
        ApprovalRequested approval = new("run_1", "call_1", "bash", """{"command":"ls"}""")
        {
            WorkspaceRoot = "/work",
        };
        DecisionEvaluated decision = new("run_1", "model_1", "one-choice", "approve", 0.9)
        {
            QuestionId = "approval",
            Distribution =
            [
                new DecisionProbability("approve", 0.9),
                new DecisionProbability("deny", 0.1),
            ],
            Confidence = 0.9,
            ConfidenceThreshold = 0.7,
            Outcome = "routed",
            Strategy = "one-choice",
            Warning = null,
        };
        UsageUpdated usageUpdated = new("run_1", usage) { ModelId = "model_1" };
        // One child-run sample: the projected line keeps the child's run id and ParentRunId, which
        // is the property a subscriber filters on.
        TextMessageContent childText = new("run_child", "msg_1", "hello")
        {
            SessionId = "session_1",
            ParentRunId = "run_1",
            Source = "ext/node-remote",
        };

        return
        [
            new("RunStarted", new RunStarted("run_1")),
            new("RunFinished", new RunFinished("run_1", "stop")),
            new("RunError", new RunError("run_1", "provider failed")),
            new("TextMessageStart", new TextMessageStart("run_1", "msg_1")),
            new("TextMessageContent", new TextMessageContent("run_1", "msg_1", "hello")),
            new("TextMessageEnd", new TextMessageEnd("run_1", "msg_1")),
            new("ReasoningStart", new ReasoningStart("run_1", "msg_2")),
            new("ReasoningMessageContent", new ReasoningMessageContent("run_1", "msg_2", "why")),
            new("ReasoningEnd", new ReasoningEnd("run_1", "msg_2")),
            new("ToolCallStart", new ToolCallStart("run_1", "call_1", "read")),
            new("ToolCallArgs", new ToolCallArgs("run_1", "call_1", """{"path":"a.txt"}""")),
            new("ToolCallEnd", new ToolCallEnd("run_1", "call_1")),
            new(
                "ToolCallResult",
                new ToolCallResult("run_1", "call_1", "wrote a.txt", IsError: false)
            ),
            new("ApprovalRequested", approval),
            new("UsageUpdated", usageUpdated),
            new("DecisionEvaluated", decision),
            new("DecisionUnavailable", new DecisionUnavailable("run_1")),
            new("DecisionFailed", new DecisionFailed("run_1", "model_1", "off contract")),
            new("ModelRouted", new ModelRouted("run_1", "fast", "model_1")),
            new("Retrying", new Retrying("run_1", 1, "rate limited")),
            new(
                "CompactionApplied",
                new CompactionApplied("run_1", ["entry_1", "entry_2"], EstimatedTokensAfter: 900)
            ),
            new("StepLimitReached", new StepLimitReached("run_1", MaxSteps: 8)),
            new("CostLimitReached", new CostLimitReached("run_1", 1.5m, 1.75m)),
            new(
                "TimeLimitReached",
                new TimeLimitReached("run_1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(6))
            ),
            new("SteeringInjected", new SteeringInjected("run_1", "entry_3", "focus")),
            new(
                "ImageDescribed",
                new ImageDescribed("run_1", "[image 2x3]", "A red square.", "sight", "vision-test")
            ),
            new("ToolProgressUpdate", new ToolProgressUpdate("run_1", "call_1", "halfway")),
            new("ToolOutputSpillFailed", new ToolOutputSpillFailed("run_1", "disk full")),
            new(
                "TodoUpdated",
                new TodoUpdated(
                    "run_1",
                    [
                        new TodoItem("read the docs", TodoItemStatus.Done),
                        new TodoItem("write the code", TodoItemStatus.InProgress),
                    ]
                )
            ),
            new("ChildRunText", childText),
        ];
    }
}
