using Lunate.Agent;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// Prices a <c>usage_updated</c> record for a remote observer's <c>event/usage_updated</c> line, so
/// its params carry the same <c>costUsd</c> the <c>--json</c> line does (ADR-0040).
/// <see cref="Lunate.Extensibility.Remote"/> sits below the catalog and cannot price, so the
/// composition that owns one hands this delegate to the extension host; a record names its own
/// model and provider, so the price needs no run-scoped fallback and an unpriced record stays
/// absent rather than zero.
/// </summary>
internal static class ExtensionWireCost
{
    internal static Func<UsageUpdated, decimal?>? For(ModelCatalog? catalog) =>
        catalog is null
            ? null
            : usage =>
                UsageCost.ForRecord(
                    catalog,
                    usage.ModelId,
                    UsageLanes.From(usage.Usage),
                    usage.Attribution?.ProviderKind
                );
}
