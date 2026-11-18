using System.Text.Json;
using Lunate.Ai.Decisions;
using Lunate.Contracts;

namespace Lunate.Agent.Tests;

public sealed class NativeDecisionCompatibilityTests
{
    [Fact]
    public async Task Legacy_native_contract_forwards_the_one_decision_request()
    {
        using JsonDocument state = JsonDocument.Parse("{\"path\":\"src\"}");
        DecisionRequest request = new(
            state.RootElement.Clone(),
            [new DecisionQuestion("safe", "Is this safe?", DecisionQuestionKind.YesNo)]
        );
        RecordingDecisionClient inner = new();
        INativeDecisionClient adapter = new NativeDecisionClientForwarder(inner, "model-x");

        string response = await adapter.GetDecisionAsync(
            request,
            TestContext.Current.CancellationToken
        );

        using JsonDocument parsed = JsonDocument.Parse(response);
        JsonElement result = parsed.RootElement.GetProperty("results").GetProperty("safe");
        Assert.Equal("yes", result.GetProperty("answer").GetString());
        Assert.Equal(0.8, result.GetProperty("probabilities").GetProperty("yes").GetDouble());
        // The forwarder stamps the model it was built for when the caller left the hint empty, and
        // applies the legacy contract's rule that a yes/no question's answers are implicit.
        Assert.Equal("model-x", inner.Request!.ModelId);
        Assert.Equal("safe", inner.Request.Questions[0].Id);
        Assert.Equal(["yes", "no"], inner.Request.Questions[0].Answers);
    }

    private sealed class RecordingDecisionClient : IDecisionClient
    {
        public DecisionRequest? Request { get; private set; }

        public Task<DecisionResult> GetDecisionAsync(
            DecisionRequest request,
            DecisionOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            Request = request;
            return Task.FromResult(
                new DecisionResult(
                    new Dictionary<string, DecisionAnswer>
                    {
                        ["safe"] = new(
                            "yes",
                            0.8,
                            [
                                new DecisionProbability("yes", 0.8),
                                new DecisionProbability("no", 0.2),
                            ],
                            request.ModelId ?? "model-x",
                            "native",
                            0.6
                        ),
                    }
                )
            );
        }

        public object? GetService(Type serviceType) => null;
    }
}
