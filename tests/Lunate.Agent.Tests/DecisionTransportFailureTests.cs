using System.Net;
using Lunate.Ai;
using Lunate.Ai.Decisions;

namespace Lunate.Agent.Tests;

/// <summary>
/// A deterministic native transport failure keeps the provider's own message. The approval gate and
/// the router used to replace every non-contract exception with "provider request failed", so a
/// state past the model's budget or a 422 field never reached the operator (S-5).
/// </summary>
public sealed class DecisionTransportFailureTests
{
    private const string FieldRejectionBody =
        """{"error":{"message":"[{\"path\":[\"questions\",\"writes_outside_workspace\",\"criteria\"],\"message\":\"Invalid input\"}]"}}""";

    private const string StateTooLargeBody =
        """{"error":{"code":"max_tokens_exceeded","message":"The model's maximum context length was exceeded"}}""";

    [Fact]
    public async Task Approval_gate_records_the_422_field_on_every_question()
    {
        ApprovalRun run = await RunApprovalAsync(
            FieldRejectionBody,
            HttpStatusCode.UnprocessableEntity
        );

        DecisionFailed[] failures = [.. run.Events.OfType<DecisionFailed>()];
        Assert.Equal(4, failures.Length);
        Assert.All(
            failures,
            failure =>
                Assert.Contains(
                    "System One rejected field 'questions.writes_outside_workspace.criteria'",
                    failure.Reason,
                    StringComparison.Ordinal
                )
        );
        Assert.All(
            failures,
            failure => Assert.Contains("Invalid input", failure.Reason, StringComparison.Ordinal)
        );
        Assert.All(
            failures,
            failure =>
                Assert.DoesNotContain(
                    "provider request failed",
                    failure.Reason,
                    StringComparison.Ordinal
                )
        );
        // A deterministic 4xx is not retried, and the failure leaves the ordinary flow in charge.
        Assert.Equal(1, run.TransportRequests);
        Assert.Equal(1, run.Executions);
        Assert.Single(run.Approver.Requests);
    }

    [Fact]
    public async Task Approval_gate_records_that_the_state_is_too_large()
    {
        ApprovalRun run = await RunApprovalAsync(StateTooLargeBody, HttpStatusCode.BadRequest);

        DecisionFailed[] failures = [.. run.Events.OfType<DecisionFailed>()];
        Assert.Equal(4, failures.Length);
        Assert.All(
            failures,
            failure =>
                Assert.Contains(
                    "Decision state too large for this model",
                    failure.Reason,
                    StringComparison.Ordinal
                )
        );
        Assert.All(
            failures,
            failure =>
                Assert.Contains("maximum context length", failure.Reason, StringComparison.Ordinal)
        );
        Assert.Equal(1, run.TransportRequests);
        Assert.Equal(1, run.Executions);
    }

    [Fact]
    public async Task Routing_records_the_422_field_and_keeps_the_configured_profile()
    {
        RoutingRun run = await RunRoutingAsync(
            FieldRejectionBody,
            HttpStatusCode.UnprocessableEntity
        );

        DecisionFailed failure = Assert.Single(run.Events.OfType<DecisionFailed>());
        Assert.Equal("routing", failure.QuestionId);
        Assert.Contains(
            "System One rejected field 'questions.writes_outside_workspace.criteria'",
            failure.Reason,
            StringComparison.Ordinal
        );
        Assert.Equal(1, run.TransportRequests);
        Assert.Equal(1, run.ConfiguredRequests);
        Assert.Equal(0, run.FastRequests);
    }

    [Fact]
    public async Task An_unexpected_exception_stays_generic()
    {
        var chat = new ScriptedChatClient().Enqueue(
            LoopScripts.Call("call_1", "write", LoopScripts.Args()),
            LoopScripts.ToolCalls()
        );
        chat.Enqueue(LoopScripts.Text("done"), LoopScripts.Stop());
        var approver = new RecordingApprover(decision: true);
        var tool = new ScriptedTool(
            "write",
            "Writes a file.",
            """{"type":"object"}""",
            ToolRisk.Write
        );
        tool.OnExecuteAsync = (_, _, _) =>
            Task.FromResult(new ToolResult("written", IsError: false));
        var registry = new ToolRegistry();
        registry.Add(tool);
        List<AgentEvent> events = await AgentTestSupport.Run(
            new AgentHarness(
                chat,
                registry,
                new AgentHarnessOptions
                {
                    DecisionMode = DecisionMode.Enforce,
                    Approver = approver,
                    UsageModels = new Dictionary<string, UsageModelClient>(StringComparer.Ordinal)
                    {
                        ["chat"] = new(
                            chat,
                            "chat-profile",
                            "openai",
                            new ModelInfo("chat-model", "openai", null, 64000, SupportsTools: true)
                        ),
                        ["decision"] = new(
                            new ScriptedChatClient
                            {
                                DecisionClient = new ThrowingDecisionClient(),
                            },
                            "decision-profile",
                            "systemone",
                            new ModelInfo(
                                "typesafe/jev-1.14",
                                "systemone",
                                null,
                                32000,
                                SupportsTools: false
                            )
                            {
                                ApiType = "native",
                            }
                        ),
                    },
                }
            )
        );

        DecisionFailed[] failures = [.. events.OfType<DecisionFailed>()];
        Assert.Equal(4, failures.Length);
        Assert.All(failures, failure => Assert.Equal("provider request failed", failure.Reason));
        Assert.Single(approver.Requests);
    }

    private static async Task<ApprovalRun> RunApprovalAsync(string body, HttpStatusCode statusCode)
    {
        var chat = new ScriptedChatClient().Enqueue(
            LoopScripts.Call("call_1", "write", LoopScripts.Args()),
            LoopScripts.ToolCalls()
        );
        chat.Enqueue(LoopScripts.Text("done"), LoopScripts.Stop());
        int executions = 0;
        var tool = new ScriptedTool(
            "write",
            "Writes a file.",
            """{"type":"object"}""",
            ToolRisk.Write
        );
        tool.OnExecuteAsync = (_, _, _) =>
        {
            executions++;
            return Task.FromResult(new ToolResult("written", IsError: false));
        };
        var registry = new ToolRegistry();
        registry.Add(tool);
        var approver = new RecordingApprover(decision: true);
        using SystemOneTransport transport = SystemOneTransport.Start(body, statusCode);
        var harness = new AgentHarness(
            chat,
            registry,
            new AgentHarnessOptions
            {
                DecisionMode = DecisionMode.Enforce,
                Approver = approver,
                UsageModels = new Dictionary<string, UsageModelClient>(StringComparer.Ordinal)
                {
                    ["chat"] = new(
                        chat,
                        "chat-profile",
                        "openai",
                        new ModelInfo("chat-model", "openai", null, 64000, SupportsTools: true)
                    ),
                    ["decision"] = transport.DecisionProfile(),
                },
            }
        );

        List<AgentEvent> events = await AgentTestSupport.Run(harness);
        return new ApprovalRun(events, transport.RequestCount, executions, approver);
    }

    private static async Task<RoutingRun> RunRoutingAsync(string body, HttpStatusCode statusCode)
    {
        var configured = new ScriptedChatClient().Enqueue(
            LoopScripts.Text("configured response"),
            LoopScripts.Stop()
        );
        var fast = new ScriptedChatClient();
        using SystemOneTransport transport = SystemOneTransport.Start(body, statusCode);
        var harness = new AgentHarness(
            configured,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                RoutingMode = DecisionMode.Enforce,
                UsageModels = new Dictionary<string, UsageModelClient>(StringComparer.Ordinal)
                {
                    ["chat"] = Model(configured, "default", "chat-model"),
                    ["decision"] = transport.DecisionProfile(),
                },
                RoutingProfiles = new Dictionary<string, UsageModelClient>(StringComparer.Ordinal)
                {
                    ["default"] = Model(configured, "default", "chat-model"),
                    ["fast"] = Model(fast, "fast", "fast-model"),
                },
            }
        );

        List<AgentEvent> events = await AgentTestSupport.Run(harness);
        return new RoutingRun(
            events,
            transport.RequestCount,
            configured.Requests.Count,
            fast.Requests.Count
        );
    }

    private static UsageModelClient Model(
        Microsoft.Extensions.AI.IChatClient client,
        string profile,
        string modelId
    ) =>
        new(
            client,
            profile,
            "openai",
            new ModelInfo(modelId, "openai", null, 32000, SupportsTools: false)
        );

    private sealed record ApprovalRun(
        IReadOnlyList<AgentEvent> Events,
        int TransportRequests,
        int Executions,
        RecordingApprover Approver
    );

    private sealed record RoutingRun(
        IReadOnlyList<AgentEvent> Events,
        int TransportRequests,
        int ConfiguredRequests,
        int FastRequests
    );
}
