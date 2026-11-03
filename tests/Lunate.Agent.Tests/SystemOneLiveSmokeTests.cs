using System.Net.Http.Headers;
using System.Text.Json;
using Lunate.Ai.Decisions;
using Lunate.Ai.SystemOne;

namespace Lunate.Agent.Tests;

public sealed class SystemOneLiveSmokeTests
{
    private const string LiveVariable = "LUNATE_LIVE";
    private const string OpenRouterKey = "OPENROUTER_API_KEY";
    private const string OpenCodeKey = "OPENCODE_API_KEY";

    [Fact]
    public async Task OpenRouter_systemone_live_smoke()
    {
        Assert.SkipUnless(LiveRequested(), $"Live contracts require {LiveVariable}=1.");
        string? key = Environment.GetEnvironmentVariable(OpenRouterKey);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(key), $"{OpenRouterKey} is not set.");

        using HttpClient httpClient = Client(key!);
        var client = new SystemOneDecisionClient(
            httpClient,
            new SystemOnePreset(
                new Uri("https://openrouter.ai/api/v1/systemone"),
                WireModelId: "typesafe/jev-1.14"
            )
        );

        DecisionResult result = await client.GetDecisionAsync(
            Request("typesafe/jev-1.14"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Single(result.Results);
        Assert.True(result.Usage?.InputTokens > 0);
        Assert.True(result.Usage?.IsProviderReported);
    }

    [Fact]
    public async Task OpenCode_systemone_live_smoke()
    {
        Assert.SkipUnless(LiveRequested(), $"Live contracts require {LiveVariable}=1.");
        string? key = Environment.GetEnvironmentVariable(OpenCodeKey);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(key), $"{OpenCodeKey} is not set.");

        using HttpClient httpClient = Client(key!);
        var client = new SystemOneDecisionClient(
            httpClient,
            new SystemOnePreset(
                new Uri("https://opencode.ai/inference/systemone/v1/systemone"),
                WireModelId: "jev-1.13"
            )
        );

        DecisionResult result = await client.GetDecisionAsync(
            Request("jev-1.13"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        Assert.Single(result.Results);
        Assert.True(result.Usage?.InputTokens > 0);
        Assert.True(result.Usage?.IsProviderReported);
    }

    private static bool LiveRequested() =>
        string.Equals(
            Environment.GetEnvironmentVariable(LiveVariable),
            "1",
            StringComparison.Ordinal
        );

    private static HttpClient Client(string key)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return client;
    }

    private static DecisionRequest Request(string modelId)
    {
        using JsonDocument state = JsonDocument.Parse(
            """{"message":"A short live smoke check."}"""
        );
        return new DecisionRequest(
            modelId,
            state.RootElement.Clone(),
            [
                new DecisionQuestion(
                    "smoke",
                    "Does the message contain words?",
                    DecisionQuestionKind.Noul,
                    ["yes", "no"]
                ),
            ]
        );
    }
}
