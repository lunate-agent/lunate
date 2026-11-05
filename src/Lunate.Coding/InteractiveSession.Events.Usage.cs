using Lunate.Agent;
using Microsoft.Extensions.AI;

namespace Lunate.Coding;

internal sealed partial class InteractiveSession
{
    private void RecordDecisionUsage(UsageUpdated usage) =>
        RecordDecisionUsage(usage.Usage, usage.ModelId, usage.Attribution, usage.ProviderCost);

    /// <summary>
    /// Persists one decision usage as a schema-4 model-usage entry. The provider-reported cost is
    /// kept beside the tokens so <c>/cost</c> prefers it over a catalog estimate and a resume
    /// reads the same amount back.
    /// </summary>
    private void RecordDecisionUsage(
        UsageDetails details,
        string? modelId,
        ModelUsageAttribution? attribution,
        double? providerCost
    )
    {
        if (_session is not { Schema: >= 4 } session || attribution is not { } attributed)
        {
            return;
        }

        int? cacheRead = details.CachedInputTokenCount is > 0 and long reportedRead
            ? (int)reportedRead
            : null;
        int? cacheWrite =
            details.AdditionalCounts is { } counts
            && counts.TryGetValue(CacheCreationInputTokensKey, out long reportedWrite)
            && reportedWrite > 0
                ? (int)reportedWrite
                : null;
        var recorded = new SessionUsage(
            (int)(details.InputTokenCount ?? 0),
            (int)(details.OutputTokenCount ?? 0),
            cacheRead,
            cacheWrite
        )
        {
            Attribution = attributed,
            Cost = providerCost is { } reportedCost ? (decimal)reportedCost : null,
        };

        session.AppendModelUsage(modelId, recorded, attributed);
    }

    /// <summary>
    /// Records a decision the extension decision service evaluated outside the harness loop
    /// (a question suggestion or an extension-initiated decision): the footer, the turn cost and
    /// the session record all get the same accounting a harness decision usage gets.
    /// </summary>
    private void RecordServiceDecisionUsage(
        UsageDetails details,
        string? modelId,
        UsageModelClient decisionModel,
        string? method,
        double? probability,
        double? providerCost
    )
    {
        var attribution = new ModelUsageAttribution(
            "decision",
            decisionModel.ProfileName,
            decisionModel.ProviderName,
            decisionModel.ProviderKind,
            method,
            probability
        );
        AddUsage(details, modelId, root: false);
        RecordDecisionUsage(details, modelId, attribution, providerCost);
    }
}
