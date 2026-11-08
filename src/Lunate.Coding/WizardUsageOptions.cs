using System.Globalization;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The flat model list the fast and decision steps offer (design §2): the provider configured in
/// this run, every provider added inline, and every provider already configured on the machine,
/// plus the decision step's native/adapter split. Pure over its inputs, so the list is unit-tested
/// without a terminal.
/// </summary>
internal static class WizardUsageOptions
{
    /// <summary>One picker row and the model ref it selects.</summary>
    internal sealed record Option(string Value, string Label, WizardModelRef Model);

    /// <summary>The rows, in provider order: the run's own provider, its inline additions, then
    /// every already-configured provider. A model id that appears twice keeps its first row.</summary>
    internal static IReadOnlyList<Option> Build(
        SetupWizardModel wizard,
        IReadOnlyList<ProviderEntry> configured,
        IReadOnlyList<WizardModelListing> listing,
        ModelCatalog catalog,
        bool decision
    )
    {
        ArgumentNullException.ThrowIfNull(wizard);
        ArgumentNullException.ThrowIfNull(configured);
        ArgumentNullException.ThrowIfNull(catalog);
        List<Option> options = [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (wizard.Provider is { } provider)
        {
            AddStaged(
                options,
                seen,
                provider,
                wizard.Credential,
                wizard.Model,
                listing,
                catalog,
                decision
            );
        }

        foreach (WizardInlineProvider inline in wizard.InlineProviders)
        {
            AddStaged(
                options,
                seen,
                inline.Provider,
                inline.Credential,
                inline.Model,
                inline.Listing,
                catalog,
                decision
            );
        }

        foreach (ProviderEntry entry in configured)
        {
            AddConfigured(options, seen, entry, catalog, decision);
        }

        return options;
    }

    /// <summary>The picker's label/value columns, in row order.</summary>
    internal static (List<string> Labels, List<string> Values) Rows(IReadOnlyList<Option> options)
    {
        List<string> labels = [];
        List<string> values = [];
        foreach (Option option in options)
        {
            labels.Add(option.Label);
            values.Add(option.Value);
        }

        return (labels, values);
    }

    /// <summary>The row's <c>provider/model</c> id: a catalog id that already carries its provider
    /// prefix (the opencode family) keeps one prefix, never two.</summary>
    internal static string DisplayId(string providerId, string modelId) =>
        modelId.StartsWith(providerId + "/", StringComparison.Ordinal)
            ? modelId
            : $"{providerId}/{modelId}";

    private static void AddStaged(
        List<Option> options,
        HashSet<string> seen,
        WizardProviderChoice provider,
        WizardCredentialChoice? credential,
        WizardModelChoice? chosen,
        IReadOnlyList<WizardModelListing> listing,
        ModelCatalog catalog,
        bool decision
    )
    {
        ProviderEntry entry = SetupWizardCommit.StagedEntry(provider, credential);
        if (decision)
        {
            foreach (string version in NativeDecisionModels(catalog, provider.Id, provider))
            {
                AddOption(
                    options,
                    seen,
                    provider,
                    credential,
                    entry,
                    new WizardModelChoice(version, ProviderApiFamilies.SystemOne),
                    native: true,
                    pinned: version,
                    adapterLabel: false
                );
            }
        }

        List<WizardModelListing> models =
            listing.Count > 0 ? [.. listing] : [.. CatalogListing(catalog, provider.Id)];
        if (chosen is { } choice && !models.Any(candidate => candidate.Id == choice.ModelId))
        {
            models.Add(
                new WizardModelListing(choice.ModelId, choice.ApiFamily, choice.Reasoning, null)
            );
        }

        foreach (WizardModelListing listed in models)
        {
            string family = listed.ApiFamily ?? provider.ApiFamily;
            if (string.Equals(family, ProviderApiFamilies.SystemOne, StringComparison.Ordinal))
            {
                continue;
            }

            AddOption(
                options,
                seen,
                provider,
                credential,
                entry,
                new WizardModelChoice(listed.Id, family, listed.Reasoning),
                native: false,
                pinned: null,
                adapterLabel: decision
            );
        }
    }

    private static void AddConfigured(
        List<Option> options,
        HashSet<string> seen,
        ProviderEntry entry,
        ModelCatalog catalog,
        bool decision
    )
    {
        WizardProviderChoice provider = CarriedProvider(entry);
        foreach (ModelInfo model in catalog.Models)
        {
            if (!string.Equals(model.Provider, entry.Kind, StringComparison.Ordinal))
            {
                continue;
            }

            bool native = string.Equals(
                model.ApiFamily,
                ProviderApiFamilies.SystemOne,
                StringComparison.Ordinal
            );
            if (native && (!decision || !ExposesSystemOne(provider)))
            {
                // A System One model is decision-only, and only a preset that exposes the family
                // can serve it.
                continue;
            }

            AddOption(
                options,
                seen,
                provider,
                credential: null,
                entry,
                new WizardModelChoice(
                    model.Id,
                    model.ApiFamily ?? ProviderApiFamilies.Name(entry.DefaultFamily),
                    model.Reasoning
                ),
                native,
                native ? model.Id : null,
                adapterLabel: decision
            );
        }
    }

    private static void AddOption(
        List<Option> options,
        HashSet<string> seen,
        WizardProviderChoice provider,
        WizardCredentialChoice? credential,
        ProviderEntry entry,
        WizardModelChoice model,
        bool native,
        string? pinned,
        bool adapterLabel
    )
    {
        string key = string.Join('\0', native ? "native" : "chat", provider.Id, model.ModelId);
        if (!seen.Add(key))
        {
            return;
        }

        string label = native
            ? NativeLabel(provider.Id, model.ModelId)
            : DisplayId(provider.Id, model.ModelId)
                + " ("
                + (adapterLabel ? "adapter, " : string.Empty)
                + model.ApiFamily
                + (model.Reasoning is { } reasoning ? $", thinking {reasoning}" : string.Empty)
                + ")";
        options.Add(
            new Option(
                "\0usage" + options.Count.ToString(CultureInfo.InvariantCulture),
                label,
                new WizardModelRef(provider, credential, entry, model, native, pinned)
            )
        );
    }

    /// <summary>The catalog's native Jev models for one provider, when its preset serves the
    /// family; a custom provider never exposes it.</summary>
    private static IEnumerable<string> NativeDecisionModels(
        ModelCatalog catalog,
        string providerId,
        WizardProviderChoice provider
    ) =>
        ExposesSystemOne(provider)
            ? catalog
                .Models.Where(model =>
                    string.Equals(model.Provider, providerId, StringComparison.Ordinal)
                    && string.Equals(
                        model.ApiFamily,
                        ProviderApiFamilies.SystemOne,
                        StringComparison.Ordinal
                    )
                )
                .Select(model => model.Id)
            : [];

    /// <summary>The catalog's entries for one provider, the shape discovery's listing has.</summary>
    private static IEnumerable<WizardModelListing> CatalogListing(
        ModelCatalog catalog,
        string providerId
    ) =>
        catalog
            .Models.Where(model =>
                string.Equals(model.Provider, providerId, StringComparison.Ordinal)
            )
            .Select(model => new WizardModelListing(
                model.Id,
                model.ApiFamily,
                model.Reasoning,
                model.ContextWindow
            ));

    /// <summary>The configured entry as the wizard's provider answer: a preset-named entry rides
    /// its preset; anything else carries its own family URL, placement and models URL.</summary>
    internal static WizardProviderChoice CarriedProvider(ProviderEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (ProviderPresets.Find(entry.Name) is { } preset)
        {
            return new WizardProviderChoice(
                entry.Name,
                Custom: false,
                entry.Region,
                Endpoint: null,
                ProviderApiFamilies.Name(preset.DefaultFamily),
                preset.PlacementFor(preset.DefaultFamily),
                entry.ModelsUrl ?? preset.ModelsUrl
            );
        }

        ProviderApiFamily family = entry.BaseUrls is { Count: > 0 } table
            ? table.Keys.First()
            : entry.DefaultFamily;
        return new WizardProviderChoice(
            entry.Name,
            Custom: true,
            entry.Region,
            entry.UrlFor(family) ?? entry.BaseUrl,
            ProviderApiFamilies.Name(family),
            entry.PlacementFor(family),
            entry.ModelsUrl
        );
    }

    private static bool ExposesSystemOne(WizardProviderChoice provider) =>
        !provider.Custom
        && ProviderPresets.Find(provider.Id) is { } preset
        && preset.Families.Contains(ProviderApiFamily.SystemOne);

    /// <summary>The native Jev row, with the Go preset's billing note exactly as step 7 states it.</summary>
    private static string NativeLabel(string providerId, string modelId) =>
        string.Equals(providerId, "opencode-go", StringComparison.Ordinal)
            ? $"{modelId} (native Jev — billed to your OpenCode pay-as-you-go balance, not the Go plan; no box questions)"
            : $"{modelId} (native Jev)";
}
