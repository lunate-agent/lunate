using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lunate.Coding.Tests;

public sealed class SystemOneDecisionProfileTests
{
    [Fact]
    public async Task Configured_systemone_profile_resolves_to_a_native_decision_exchange()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(
            temp.File("models.json"),
            """
            {
              "schemaVersion": 2,
              "models": [
                { "id": "openrouter/typesafe/jev-1.14", "provider": "openrouter",
                  "wireId": "typesafe/jev-1.14", "contextWindow": 65536,
                  "supportsTools": false, "supportsVision": true,
                  "apiType": "native", "apiFamily": "systemone" }
              ]
            }
            """
        );
        File.WriteAllText(
            temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "decision": {
                  "provider": "openrouter",
                  "model": "openrouter/typesafe/jev-1.14",
                  "apiFamily": "systemone"
                }
              },
              "decisions": { "profile": "decision" }
            }
            """
        );
        Assert.True(
            ProviderEntry.TryCreate(
                "openrouter",
                "openrouter",
                new Uri("https://openrouter.ai/api/v1"),
                "auth:openrouter",
                headers: null,
                region: null,
                out ProviderEntry? provider,
                out string? error
            ),
            error
        );
        using var handler = new FixtureHandler(
            File.ReadAllText(Fixture("openrouter-noul-choice-score.json"))
        );
        using ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        var factory = new ChatClientFactory(loggerFactory) { BaseTransportFactory = () => handler };
        AgentSettings settings = SettingsStore.Resolve(temp.File("settings.json"), _ => null);
        ModelCatalog catalog = ModelCatalog.Load(temp.File("models.json"));
        UsageModelClient decisionModel = Assert.IsType<UsageModelClient>(
            DecisionUsageModels.ResolveDecisionModel(
                settings,
                () => (AuthStore.Load(temp.File("auth.json"), _ => null), [provider!]),
                catalog,
                name => name == "OPENROUTER_API_KEY" ? "fixture-key" : null,
                factory,
                _ => { },
                []
            )
        );
        using JsonDocument state = JsonDocument.Parse("{}");
        Lunate.Contracts.DecisionRequest request = new(
            state.RootElement.Clone(),
            [
                new Lunate.Contracts.DecisionQuestion(
                    "is_bug",
                    "Is this a bug?",
                    Lunate.Contracts.DecisionQuestionKind.YesNo
                ),
            ]
        );
        UsageDetails? recordedUsage = null;
        string? recordedMethod = null;
        double? recordedCost = null;

        Lunate.Contracts.DecisionResponse response = await DecisionEngine.EvaluateAsync(
            request,
            decisionModel.Client,
            decisionModel.Model.Id,
            "native",
            cancellationToken: TestContext.Current.CancellationToken,
            recordUsage: (usage, _, method, _, cost) =>
            {
                recordedUsage = usage;
                recordedMethod = method;
                recordedCost = cost;
            },
            supportsImages: DecisionClientResolver.SupportsImageInput(decisionModel.Model)
        );

        Lunate.Contracts.DecisionAnswer answer = response.Results["is_bug"];
        Assert.Equal("yes", answer.Answer);
        Assert.Equal("native", answer.Method);
        Assert.Equal("typesafe/jev-1.14-20261014", answer.ModelId);
        Assert.Equal(394, recordedUsage!.InputTokenCount);
        Assert.Equal(63, recordedUsage.OutputTokenCount);
        Assert.Equal("native", recordedMethod);
        Assert.Equal(0.000016548, recordedCost);
        Assert.Equal(
            "https://openrouter.ai/api/v1/systemone",
            handler.Request.RequestUri!.AbsoluteUri
        );
        using JsonDocument requestBody = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal("typesafe/jev-1.14", requestBody.RootElement.GetProperty("model").GetString());
        Assert.Equal("Bearer fixture-key", handler.Authorization);
    }

    private static string Fixture(string fileName) =>
        Path.Combine(TestPaths.RepositoryRoot, "tests", "fixtures", "systemone", fileName);

    private sealed class FixtureHandler(string response) : HttpMessageHandler
    {
        public HttpRequestMessage Request { get; private set; } = null!;
        public string RequestBody { get; private set; } = string.Empty;
        public string? Authorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    response,
                    System.Text.Encoding.UTF8,
                    "application/json"
                ),
            };
        }
    }
}
