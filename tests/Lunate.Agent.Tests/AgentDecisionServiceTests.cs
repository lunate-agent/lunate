using System.Text.Json;
using Lunate.Ai.Decisions;
using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

public sealed class AgentDecisionServiceTests
{
    [Fact]
    public async Task Native_service_decision_reports_its_tokens_and_provider_cost()
    {
        using JsonDocument state = JsonDocument.Parse("""{"plan":"flexible"}""");
        Lunate.Contracts.DecisionRequest request = new(
            state.RootElement.Clone(),
            [
                new Lunate.Contracts.DecisionQuestion(
                    "question-1",
                    "Which plan should I use?",
                    Lunate.Contracts.DecisionQuestionKind.Choice,
                    ["Simple", "Flexible"]
                ),
            ]
        );
        var result = new DecisionResult(
            new Dictionary<string, DecisionAnswer>(StringComparer.Ordinal)
            {
                ["question-1"] = new(
                    "Flexible",
                    0.8,
                    [
                        new DecisionProbability("Simple", 0.2),
                        new DecisionProbability("Flexible", 0.8),
                    ],
                    "decision-model",
                    "native",
                    0.6
                ),
            },
            new DecisionUsage(500, 40, 0.0021, IsProviderReported: true)
        );
        var chat = new ScriptedChatClient { DecisionClient = new FixedDecisionClient(result) };
        List<(long Input, long Output, string ModelId, double? Cost)> recorded = [];
        var service = new AgentDecisionService(
            chat,
            "decision-model",
            "native",
            recordUsage: (usage, modelId, _, _, cost) =>
                recorded.Add(
                    (usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0, modelId, cost)
                )
        );

        Lunate.Contracts.DecisionResponse response = await service.DecideAsync(
            request,
            TestContext.Current.CancellationToken
        );

        Assert.Equal("Flexible", response.Results["question-1"].Answer);
        var usage = Assert.Single(recorded);
        Assert.Equal(500, usage.Input);
        Assert.Equal(40, usage.Output);
        Assert.Equal("decision-model", usage.ModelId);
        Assert.Equal(0.0021, usage.Cost);
    }

    [Fact]
    public async Task Adapter_service_decision_records_every_failed_attempt()
    {
        using JsonDocument state = JsonDocument.Parse("""{"plan":"flexible"}""");
        Lunate.Contracts.DecisionRequest request = new(
            state.RootElement.Clone(),
            [
                new Lunate.Contracts.DecisionQuestion(
                    "question-1",
                    "Which plan should I use?",
                    Lunate.Contracts.DecisionQuestionKind.Choice,
                    ["Simple", "Flexible"]
                ),
            ]
        );
        var chat = new ScriptedChatClient()
            .Enqueue(LoopScripts.Text("not json"), LoopScripts.Usage(90, 5), LoopScripts.Stop())
            .Enqueue(
                LoopScripts.Text("still not json"),
                LoopScripts.Usage(95, 6),
                LoopScripts.Stop()
            );
        List<(long Input, long Output)> recorded = [];
        var service = new AgentDecisionService(
            chat,
            "decision-model",
            "adapter",
            recordUsage: (usage, _, _, _, _) =>
                recorded.Add((usage.InputTokenCount ?? 0, usage.OutputTokenCount ?? 0))
        );

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DecideAsync(request, TestContext.Current.CancellationToken)
        );

        Assert.Equal([(90L, 5L), (95L, 6L)], recorded);
    }

    private sealed class FixedDecisionClient(DecisionResult result) : IDecisionClient
    {
        public Task<DecisionResult> GetDecisionAsync(
            DecisionRequest request,
            DecisionOptions? options = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(result);

        public object? GetService(Type serviceType) => null;
    }
}
