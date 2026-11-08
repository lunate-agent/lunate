namespace Lunate.Ai;

/// <summary>
/// A catalogue lookup made in the scope of one provider entry: either the entry the id names, or —
/// when it names none — the entries whose wire id the bare id is. A flow that knows the provider
/// (the setup wizard, a profile's resolution) resolves through the entry instead of failing on an
/// id the catalogue also holds under other providers.
/// </summary>
/// <param name="Match">The resolved entry, or null when the scope resolves nothing.</param>
/// <param name="Candidates">
/// The entries the bare id matches when it resolves to more than one: the caller that knows the
/// provider can name them to the user. Empty whenever <paramref name="Match"/> is set.
/// </param>
internal sealed record ScopedModelLookup(ModelInfo? Match, IReadOnlyList<ModelInfo> Candidates)
{
    /// <summary>Whether the id names several entries and the scope resolved none of them.</summary>
    internal bool IsAmbiguous => Match is null && Candidates.Count > 1;
}
