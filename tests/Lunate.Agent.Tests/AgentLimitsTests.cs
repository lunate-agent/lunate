using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lunate.Ai;
using Microsoft.Extensions.AI;
using static Lunate.Agent.Tests.AgentTestSupport;

namespace Lunate.Agent.Tests;

public sealed class AgentLimitsTests
{
    [Fact]
    public async Task The_step_limit_ends_a_run_that_keeps_calling_tools()
    {
        ScriptedTool read = ReadTool("contents");
        var client = new ScriptedChatClient();
        for (int call = 1; call <= 3; call++)
        {
            client.Enqueue(
                LoopScripts.Call($"call_{call}", "read", LoopScripts.Args(("path", "a.txt"))),
                LoopScripts.ToolCalls()
            );
        }

        var harness = new AgentHarness(
            client,
            Registry(read),
            new AgentHarnessOptions { MaxSteps = 3 }
        );

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        Assert.Equal(3, client.Requests.Count);
        Assert.Equal(3, events.OfType<ToolCallResult>().Count());
        StepLimitReached limit = Assert.Single(events.OfType<StepLimitReached>());
        Assert.Equal(3, limit.MaxSteps);
        RunFinished finished = Assert.IsType<RunFinished>(events[^1]);
        Assert.Equal(StopReasons.StepLimit, finished.StopReason);
        int lastToolResult = events.FindLastIndex(agentEvent => agentEvent is ToolCallResult);
        int stepLimit = events.FindIndex(agentEvent => agentEvent is StepLimitReached);
        Assert.True(lastToolResult < stepLimit && stepLimit < events.Count - 1);
    }

    [Fact]
    public void Max_nested_tool_depth_must_be_at_least_one()
    {
        var client = new ScriptedChatClient();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AgentHarness(
                client,
                new ToolRegistry(),
                new AgentHarnessOptions { MaxNestedToolDepth = 0 }
            )
        );

        Assert.Equal(nameof(AgentHarnessOptions.MaxNestedToolDepth), exception.ParamName);
    }

    [Fact]
    public async Task An_answer_on_the_last_allowed_call_finishes_with_stop()
    {
        ScriptedTool read = ReadTool("contents");
        var client = new ScriptedChatClient()
            .Enqueue(
                LoopScripts.Call("call_1", "read", LoopScripts.Args(("path", "a.txt"))),
                LoopScripts.ToolCalls()
            )
            .Enqueue(LoopScripts.Text("Done"), LoopScripts.Stop());
        var harness = new AgentHarness(
            client,
            Registry(read),
            new AgentHarnessOptions { MaxSteps = 2 }
        );

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        Assert.Equal(2, client.Requests.Count);
        Assert.Empty(events.OfType<StepLimitReached>());
        Assert.Equal(StopReasons.Stop, Assert.IsType<RunFinished>(events[^1]).StopReason);
    }

    [Fact]
    public async Task The_cost_limit_triggers_exactly_at_the_boundary()
    {
        ModelInfo model = PricedModel();
        var client = new ScriptedChatClient().Enqueue(
            LoopScripts.Usage(1, 0),
            LoopScripts.Call("call_1", "read", LoopScripts.Args(("path", "a.txt"))),
            LoopScripts.ToolCalls()
        );
        var harness = new AgentHarness(
            client,
            Registry(ReadTool("contents")),
            new AgentHarnessOptions
            {
                Model = model,
                ModelId = model.Id,
                ModelCatalog = ModelCatalog.Single(model),
                MaxCostUsd = 1m,
            }
        );

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        Assert.Single(client.Requests);
        Assert.Empty(events.OfType<ToolCallResult>());
        CostLimitReached limit = Assert.Single(events.OfType<CostLimitReached>());
        Assert.Equal(1m, limit.MaxCostUsd);
        Assert.Equal(1m, limit.SpendUsd);
        Assert.Equal(StopReasons.CostLimit, Assert.IsType<RunFinished>(events[^1]).StopReason);
    }

    [Fact]
    public async Task Spend_below_the_cost_limit_finishes_normally()
    {
        ModelInfo model = PricedModel();
        var client = new ScriptedChatClient().Enqueue(
            LoopScripts.Usage(1, 0),
            LoopScripts.Text("Done"),
            LoopScripts.Stop()
        );
        var harness = new AgentHarness(
            client,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Model = model,
                ModelId = model.Id,
                ModelCatalog = ModelCatalog.Single(model),
                MaxCostUsd = 2m,
            }
        );

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(events.OfType<CostLimitReached>());
        Assert.Equal(StopReasons.Stop, Assert.IsType<RunFinished>(events[^1]).StopReason);
    }

    [Fact]
    public async Task The_time_limit_triggers_exactly_at_the_boundary()
    {
        var clock = new ManualTimeProvider();
        var harness = new AgentHarness(
            new AdvancingChatClient(clock, TimeSpan.FromSeconds(5)),
            new ToolRegistry(),
            new AgentHarnessOptions { MaxTime = TimeSpan.FromSeconds(5), TimeProvider = clock }
        );

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(EventSequenceValidator.Validate(events));
        TimeLimitReached limit = Assert.Single(events.OfType<TimeLimitReached>());
        Assert.Equal(TimeSpan.FromSeconds(5), limit.MaxTime);
        Assert.Equal(TimeSpan.FromSeconds(5), limit.Elapsed);
        Assert.Equal(StopReasons.TimeLimit, Assert.IsType<RunFinished>(events[^1]).StopReason);
    }

    [Fact]
    public async Task A_resumed_session_keeps_its_spend()
    {
        using var temp = new TempDirectory();
        ModelInfo model = PricedModel();
        string path = temp.File("s.jsonl");
        Session created = Session.Create(
            path,
            "/work",
            new FixedTimeProvider(DateTimeOffset.UnixEpoch)
        );
        created.AppendMessage(
            new ChatMessage(ChatRole.Assistant, "earlier"),
            model.Id,
            new SessionUsage(1, 0)
        );
        Session loaded = Session.Load(path);

        var client = new ScriptedChatClient();
        var harness = new AgentHarness(
            client,
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Session = loaded,
                Model = model,
                ModelId = model.Id,
                ModelCatalog = ModelCatalog.Single(model),
                MaxCostUsd = 1m,
            }
        );

        List<AgentEvent> events = await Run(harness);

        Assert.Empty(client.Requests);
        CostLimitReached limit = Assert.Single(events.OfType<CostLimitReached>());
        Assert.Equal(1m, limit.SpendUsd);
        SessionExtensionEntry record = Assert.Single(
            loaded.Entries.OfType<SessionExtensionEntry>()
        );
        Assert.Equal("ext/lunate/run-limit", record.ExtensionType);
        JsonNode payload = record.Payload!;
        Assert.Equal("cost", payload["limit"]!.GetValue<string>());
        Assert.Equal(1m, payload["maxCostUsd"]!.GetValue<decimal>());
        Assert.Equal(1m, payload["spendUsd"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task A_time_limit_is_recorded_in_the_session()
    {
        using var temp = new TempDirectory();
        var clock = new ManualTimeProvider();
        Session session = Session.Create(
            temp.File("s.jsonl"),
            "/work",
            new FixedTimeProvider(DateTimeOffset.UnixEpoch)
        );
        var harness = new AgentHarness(
            new AdvancingChatClient(clock, TimeSpan.FromSeconds(5)),
            new ToolRegistry(),
            new AgentHarnessOptions
            {
                Session = session,
                MaxTime = TimeSpan.FromSeconds(5),
                TimeProvider = clock,
            }
        );

        List<AgentEvent> events = await Run(harness);

        Assert.Equal(StopReasons.TimeLimit, Assert.IsType<RunFinished>(events[^1]).StopReason);
        SessionExtensionEntry record = Assert.Single(
            session.Entries.OfType<SessionExtensionEntry>()
        );
        Assert.Equal("ext/lunate/run-limit", record.ExtensionType);
        JsonNode payload = record.Payload!;
        Assert.Equal("time", payload["limit"]!.GetValue<string>());
        Assert.Equal(5d, payload["maxTimeSeconds"]!.GetValue<double>());
        Assert.Equal(5d, payload["elapsedSeconds"]!.GetValue<double>());
    }

    [Fact]
    public void A_cost_limit_over_an_unpriced_model_refuses_to_start()
    {
        var model = new ModelInfo("freeform", "fixture", null, 1_000, true);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new AgentHarness(
                new ScriptedChatClient(),
                new ToolRegistry(),
                new AgentHarnessOptions
                {
                    Model = model,
                    ModelId = model.Id,
                    ModelCatalog = ModelCatalog.Single(model),
                    MaxCostUsd = 1m,
                }
            )
        );

        Assert.Contains("freeform", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AllowUnpricedCost", exception.Message, StringComparison.Ordinal);
    }

    private static ModelInfo PricedModel() =>
        new("priced", "fixture", null, 1_000, true)
        {
            Pricing = new ModelPricing(Input: 1_000_000m, Output: 0m),
        };

    private sealed class AdvancingChatClient(ManualTimeProvider clock, TimeSpan advance)
        : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(new ChatResponse());

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            clock.Advance(advance);
            cancellationToken.ThrowIfCancellationRequested();
            yield return LoopScripts.Text("Done");
            yield return LoopScripts.Stop();
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
