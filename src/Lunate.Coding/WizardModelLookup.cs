using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The wizard's catalogue lookup (spec: The model step resolves the id in the walked provider's
/// scope): discovery returns the wire ids an endpoint serves — bare for a custom endpoint, qualified
/// for an OpenAI-compatible preset — and the catalogue can hold one bare id under several providers.
/// The walk knows which provider entry it is writing, so it reads the id in that scope and never
/// fails on an id the catalogue also holds elsewhere.
/// </summary>
internal static class WizardModelLookup
{
    /// <summary>
    /// The catalogue entry the chosen id names for the walked provider, or the candidates to offer
    /// when several unrelated entries hold it. A custom endpoint's own catalogue entry is
    /// <c>&lt;provider id&gt;/&lt;model&gt;</c>, which this walk is about to write, so its listing's
    /// bare id is never ambiguous: no entry of the catalogue has to answer it.
    /// </summary>
    internal static ScopedModelLookup Resolve(
        WizardProviderChoice provider,
        ModelCatalog catalog,
        string id
    )
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        ScopedModelLookup lookup = catalog.FindFor(provider.Id, id);
        return lookup.Match is null && provider.Custom ? new ScopedModelLookup(null, []) : lookup;
    }
}
