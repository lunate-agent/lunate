using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// Live smoke for the <c>opencode-go</c> preset's pinned System One model, gated by
/// <c>LUNATE_LIVE=1</c> plus <c>OPENCODE_GO_API_KEY</c> (or the preset's legacy alias <c>OPENCODE_API_KEY</c>). Without the gate
/// the test is skipped and never touches the network. The request and the model both resolve
/// through the real preset and decision-usage seams — <see cref="ProviderPresets.Find"/>,
/// the embedded <see cref="ModelCatalog"/>, <see cref="DecisionUsageModels"/> and
/// <see cref="DecisionEngine"/> — never a hand-built URL or wire id.
/// </summary>
public sealed class OpenCodeGoSystemOneLiveSmokeTests
{
    private const string LiveVariable = "LUNATE_LIVE";
    private const string OpenCodeApiKeyVariable = "OPENCODE_GO_API_KEY";
    private const string OpenCodeLegacyApiKeyVariable = "OPENCODE_API_KEY";

    [Fact]
    public async Task Opencode_go_systemone_resolves_native_and_returns_a_usable_noul()
    {
        Assert.SkipUnless(LiveRequested(), $"Live contracts require {LiveVariable}=1.");
        string? key = Environment.GetEnvironmentVariable(OpenCodeApiKeyVariable)
            ?? Environment.GetEnvironmentVariable(OpenCodeLegacyApiKeyVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(key), $"{OpenCodeApiKeyVariable} is not set.");

        // The preset resolution: the pinned model and the family URL/headers come from the preset
        // and the embedded catalog, exactly as a configured decisions.profile resolves them.
        ProviderPreset preset = Assert.IsType<ProviderPreset>(ProviderPresets.Find("opencode-go"));
        ModelCatalog catalog = ModelCatalog.LoadLayered(managedFilePath: null, userFilePath: null);
        ModelInfo model = Assert.IsType<ModelInfo>(catalog.Find("opencode-go/jev-1.13"));

        Assert.Equal("native", model.ApiType);
        Assert.Equal(ProviderApiFamilies.SystemOne, model.ApiFamily);
        Assert.Equal("jev-1.13", model.WireId);
        Assert.Equal(
            new Uri("https://opencode.ai/inference/systemone/v1/systemone"),
            preset.UrlFor(ProviderApiFamily.SystemOne)
        );

        using var temp = new TempDirectory();
        Assert.True(
            ProviderEntry.TryCreate(
                "opencode-go",
                "opencode-go",
                preset.BaseUrl,
                "env:OPENCODE_GO_API_KEY",
                headers: null,
                region: null,
                out ProviderEntry? provider,
                out string? error
            ),
            error
        );
        var settings = new AgentSettings
        {
            Decisions = new DecisionsSettings { Profile = "decision" },
            Profiles = new Dictionary<string, ModelProfile>(StringComparer.Ordinal)
            {
                ["decision"] = new ModelProfile(
                    "decision",
                    "opencode-go",
                    "opencode-go/jev-1.13",
                    ApiFamily: ProviderApiFamilies.SystemOne
                ),
            },
        };
        var factory = new ChatClientFactory(NullLoggerFactory.Instance, enableOpenTelemetry: false);
        UsageModelClient decisionModel = Assert.IsType<UsageModelClient>(
            DecisionUsageModels.ResolveDecisionModel(
                settings,
                () => (AuthStore.Load(temp.File("auth.json"), _ => null), [provider!]),
                catalog,
                name => name == OpenCodeApiKeyVariable ? key : null,
                factory,
                _ => { },
                []
            )
        );

        // The decision-model choice: the profile resolved the preset's pinned native Jev entry.
        Assert.Equal("opencode-go/jev-1.13", decisionModel.Model.Id);
        Assert.Equal("native", decisionModel.Model.ApiType);

        using JsonDocument state = JsonDocument.Parse("{}");
        Lunate.Contracts.DecisionRequest request = new(
            state.RootElement.Clone(),
            [
                new Lunate.Contracts.DecisionQuestion(
                    "smoke",
                    "Does this message contain words?",
                    Lunate.Contracts.DecisionQuestionKind.YesNo
                ),
            ]
        );
        UsageDetails? recordedUsage = null;
        string? recordedMethod = null;

        Lunate.Contracts.DecisionResponse response = await DecisionEngine.EvaluateAsync(
            request,
            decisionModel.Client,
            decisionModel.Model.Id,
            "native",
            cancellationToken: TestContext.Current.CancellationToken,
            recordUsage: (usage, _, method, _, _) =>
            {
                recordedUsage = usage;
                recordedMethod = method;
            },
            supportsImages: DecisionClientResolver.SupportsImageInput(decisionModel.Model)
        );

        Lunate.Contracts.DecisionAnswer answer = Assert.Single(response.Results).Value;
        Assert.Equal("native", answer.Method);
        Assert.False(string.IsNullOrWhiteSpace(answer.Answer), "the decision returned no answer");
        Assert.InRange(answer.Probability, 0.0, 1.0);
        Assert.Equal("native", recordedMethod);
        Assert.True(
            recordedUsage?.InputTokenCount > 0,
            "the live response reported no input tokens"
        );
    }

    private static bool LiveRequested() =>
        string.Equals(
            Environment.GetEnvironmentVariable(LiveVariable),
            "1",
            StringComparison.OrdinalIgnoreCase
        );
}
