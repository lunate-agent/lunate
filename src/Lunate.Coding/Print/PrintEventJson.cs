using Lunate.Agent;
using Lunate.Ai;
using Lunate.Extensibility.Remote;

namespace Lunate.Coding;

/// <summary>
/// Serializes one <see cref="AgentEvent"/> to one JSON line for <c>lunate -p --json</c>: the shared
/// projection in <see cref="RemoteEventJson"/> (the same object the extension wire carries,
/// ADR-0040) plus <c>--json</c>'s own optional <c>costUsd</c>, which needs the model catalog the
/// wire does not have. Keeping the event switch in one place is what stops the stream, the wire and
/// the documented kind vocabulary from drifting apart.
/// </summary>
internal static class PrintEventJson
{
    /// <summary>The event types the serializer handles; the reflection test pins this against the assembly.</summary>
    internal static IReadOnlyList<Type> HandledEventTypes => RemoteEventJson.HandledEventTypes;

    /// <summary>
    /// Returns the one-line JSON object for an event, including the stamped fields. A catalog and
    /// fallback model id price a <c>usage_updated</c> line: a fully priced record adds a numeric
    /// <c>costUsd</c>, an unpriced one keeps the line unchanged.
    /// </summary>
    internal static string Serialize(
        AgentEvent agentEvent,
        ModelCatalog? catalog = null,
        string? fallbackModelId = null
    )
    {
        if (
            agentEvent is UsageUpdated usage
            && CostUsd(usage, catalog, fallbackModelId) is { } cost
        )
        {
            return RemoteEventJson.Serialize(agentEvent, body =>
                body["costUsd"] = System.Text.Json.Nodes.JsonValue.Create(cost)
            );
        }

        return RemoteEventJson.Serialize(agentEvent);
    }

    private static decimal? CostUsd(
        UsageUpdated usage,
        ModelCatalog? catalog,
        string? fallbackModelId
    ) =>
        catalog is null
            ? null
            : UsageCost.ForRecord(
                catalog,
                usage.ModelId ?? fallbackModelId,
                UsageLanes.From(usage.Usage),
                SelectedProvider(catalog, fallbackModelId)
            );

    /// <summary>
    /// The provider the run selected, from the fallback model id (always a catalogue id): a
    /// provider echoes its bare wire id on the usage record, and the selected provider is what
    /// tells two presets serving the same wire id apart.
    /// </summary>
    private static string? SelectedProvider(ModelCatalog catalog, string? fallbackModelId) =>
        string.IsNullOrEmpty(fallbackModelId)
            ? null
            : catalog.FindUsageModel(fallbackModelId, provider: null)?.Provider;
}
