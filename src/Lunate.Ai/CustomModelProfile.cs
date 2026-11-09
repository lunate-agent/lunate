namespace Lunate.Ai;

/// <summary>
/// Resolves a profile on a custom provider entry — an entry whose name is not a provider preset
/// id, and whose kind is therefore only the adapter that speaks its API family's wire. The model
/// is the entry's own, <c>&lt;provider id&gt;/&lt;model&gt;</c>, and the check that remains is the
/// family the entry serves. A catalogue entry under another provider supplies model facts — never
/// its pricing — and is never a reason to refuse the profile (#708).
/// </summary>
internal static class CustomModelProfile
{
    /// <summary>
    /// Whether the entry is custom. The store refuses a preset id as a custom provider id, so a
    /// name that is not a preset id can only be a user-declared endpoint. A preset-named entry
    /// keeps the kind check, because there its kind is the vendor.
    /// </summary>
    internal static bool IsCustom(ProviderEntry provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return ProviderPresets.Find(provider.Name) is null;
    }

    /// <summary>
    /// The model the entry serves, named by the profile: the matched catalogue entry when one
    /// resolved in the entry's scope, otherwise the entry's own entry carrying the facts of the one
    /// catalogue entry in the family it serves. Endpoint, family, headers and credential placement
    /// are resolved on the entry afterwards, exactly as for a preset profile.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The catalogue entry speaks a family the entry does not serve, and the profile does not
    /// declare the family itself.
    /// </exception>
    internal static ModelInfo Resolve(
        ModelProfile profile,
        ProviderEntry provider,
        ModelInfo? known,
        IReadOnlyList<ModelInfo> candidates
    )
    {
        ModelInfo? facts = known ?? FactsFor(provider, candidates);
        if (facts is not null && profile.ApiFamily is null)
        {
            // A profile that declares its family is the user's explicit routing intent: the entry
            // then has to declare a URL for that family, and the endpoint lookup says so.
            RequireFamilyServed(provider, facts);
        }

        // The entry's catalogue id for this model: `<entry>/<wire id>`, the same shape the settings
        // section's custom provider contributes to the catalogue.
        string id = $"{provider.Name}/{facts?.WireId ?? profile.Model}";
        // The entry's own catalogue entry is the only one whose endpoint and auth reference belong
        // to this endpoint; another provider's entry would route or authorize the wrong place.
        bool own = known is not null && string.Equals(known.Id, id, StringComparison.Ordinal);
        return new ModelInfo(
            id,
            provider.Kind,
            Endpoint: own ? known!.Endpoint : null,
            ContextWindow: facts?.ContextWindow ?? 0,
            SupportsTools: facts?.SupportsTools ?? false,
            AuthRef: own ? known!.AuthRef : null,
            Reasoning: facts?.Reasoning
        )
        {
            MaxOutput = facts?.MaxOutput,
            SupportsVision = facts?.SupportsVision ?? false,
            Capabilities = facts?.Capabilities,
            ApiFamily = facts?.ApiFamily,
            SupportsTemperature = facts?.SupportsTemperature ?? false,
            WireId = facts?.WireId ?? profile.Model,
        };
    }

    /// <summary>
    /// The facts of the one candidate the entry's family serves. Several candidates in the family
    /// name no model this endpoint is known to be, so none of them supplies limits — another
    /// endpoint's context window is not this one's.
    /// </summary>
    private static ModelInfo? FactsFor(ProviderEntry provider, IReadOnlyList<ModelInfo> candidates)
    {
        ModelInfo[] served =
        [
            .. candidates.Where(candidate => provider.ChatFamilies.Contains(FamilyOf(candidate))),
        ];
        return served is [ModelInfo only] ? only : null;
    }

    private static void RequireFamilyServed(ProviderEntry provider, ModelInfo facts)
    {
        ProviderApiFamily spoken = FamilyOf(facts);
        if (provider.ChatFamilies.Contains(spoken))
        {
            return;
        }

        string served = ProviderApiFamilies.NamesFor(provider.ChatFamilies);
        throw new InvalidOperationException(
            $"'{provider.Name}' serves {served}, but model '{facts.Id}' speaks "
                + $"'{ProviderApiFamilies.Name(spoken)}'; name a model of {served}, or declare "
                + $"'{ProviderApiFamilies.Name(spoken)}' on the entry with its own baseUrls."
        );
    }

    /// <summary>
    /// The family a catalogue entry speaks: its own annotation, or its provider preset's default —
    /// the same fallback the family resolver takes when the entry declares none.
    /// </summary>
    private static ProviderApiFamily FamilyOf(ModelInfo model) =>
        model.ApiFamily is { } declared
        && ProviderApiFamilies.TryParse(declared, out ProviderApiFamily family)
            ? family
            : ProviderPresets.Find(model.Provider)?.DefaultFamily ?? ProviderApiFamily.OpenAiChat;
}
