using System.Text.Json.Nodes;
using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The setup wizard's one commit pass (design §5, spec: One settings writer), executed at the
/// summary's confirmation: the credential through <see cref="AuthStore"/>, the provider entry
/// through the provider entry's own writers — <see cref="SettingsStore.WriteProvider"/> for the
/// custom entry a typed key pairs with, exactly what <c>auth login --custom</c> writes — and the
/// profiles, usages and decisions keys through the <see cref="SettingsStore"/> writers the store
/// itself owns. A preset entry, and any entry carrying an <c>env:</c>/<c>auth:</c> reference, is
/// saved through <see cref="ProviderStore.Save"/>. The wizard keeps no JSON emitter; a walk that
/// staged no change at all is a no-op, not a rewrite.
/// <para>
/// The commit is one transaction over the three files a walk can touch: every new body is built
/// in a staging directory by those same writers, the files are then replaced one after another,
/// and a failure anywhere restores each file already replaced byte for byte. A credential
/// therefore never reaches auth.json unless its provider entry, the profiles and the decisions
/// keys land beside it, and a failed commit leaves the previous configuration exactly as it was.
/// </para>
/// <para>
/// The commit is also a read-modify-write, so it takes the target claims before it seeds: the
/// staged bodies start from the bytes the targets hold once the claims are held, and the replace
/// lands while they are still held. A change another process lands while this commit waits is
/// therefore merged, not dropped (spec: Cross-process settings writes, Cross-process
/// provider-store writes). The walk itself — the interactive part — happens before this call, so
/// the claims are held for one short pass, never while a person answers a step.
/// </para>
/// </summary>
internal static class SetupWizardCommit
{
    /// <summary>
    /// Commits the staged answers. Nothing is written when the walk staged no change at all
    /// (skip everything confirmed), so the three files stay byte-identical. A failed write
    /// surfaces as one exception naming what failed, and no target file keeps a partial commit:
    /// auth.json, providers.json and settings.json are unchanged byte for byte.
    /// </summary>
    public static void Commit(
        SetupWizardModel model,
        CommitPaths paths,
        Func<AuthStore> loadAuth,
        TimeSpan? claimTimeout = null
    )
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(loadAuth);

        if (!model.HasStagedWrites)
        {
            return;
        }

        AuthStore live = loadAuth();
        IReadOnlyList<WizardProviderRun> runs = model.AllProviderRuns;
        var typedSecrets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (WizardProviderRun run in runs)
        {
            if (
                run.Credential is { Key: { Length: > 0 } secret }
                && (run.Provider != model.Provider || !model.IsCarried(SetupStep.Provider))
            )
            {
                typedSecrets[run.Provider.Id] = secret;
            }
        }

        // The claims come before the seed: the seed's read is the read-modify-write base, so what
        // another process wrote while this commit waited for the claim is merged rather than
        // replaced. Both claims are held until the last replace; nothing before them is written.
        using IDisposable claims = SetupWizardTransaction.ClaimTargets(
            paths.SettingsPath,
            paths.ProviderStorePath,
            claimTimeout
        );
        using SetupWizardTransaction transaction = SetupWizardTransaction.Create(
            paths.SettingsPath,
            typedSecrets.Count == 0 ? null : live.FilePath
        );

        // Every writer is a read-modify-write, so a staged file starts from the bytes it has
        // today: the sections the walk does not touch survive the commit.
        SetupWizardTransaction.Seed(live.FilePath, transaction.Auth);
        SetupWizardTransaction.Seed(paths.SettingsPath, transaction.Settings);
        SetupWizardTransaction.Seed(paths.ProviderStorePath, transaction.Providers);

        WriteStaged(model, runs, transaction, typedSecrets);
        transaction.Commit([
            new(transaction.Auth, live.FilePath),
            new(transaction.Providers, paths.ProviderStorePath),
            new(transaction.Settings, paths.SettingsPath),
        ]);

        // The session's in-memory store follows the file only once every file is in place:
        // nothing is applied to memory that a failed commit would have to undo.
        foreach ((string provider, string secret) in typedSecrets)
        {
            live.Set(provider, secret);
        }
    }

    private static void WriteStaged(
        SetupWizardModel model,
        IReadOnlyList<WizardProviderRun> runs,
        SetupWizardTransaction transaction,
        IReadOnlyDictionary<string, string> typedSecrets
    )
    {
        var stagedPaths = new CommitPaths(transaction.Settings, transaction.Providers);
        foreach (WizardProviderRun run in runs)
        {
            if (run.Provider == model.Provider && model.IsCarried(SetupStep.Provider))
            {
                continue;
            }

            if (typedSecrets.TryGetValue(run.Provider.Id, out string? secret))
            {
                // The credential is staged like every other file: the wizard's key reaches
                // auth.json in one replace, never before the rest of the commit.
                AuthStore auth = AuthStore.Load(transaction.Auth);
                auth.Set(run.Provider.Id, secret);
                auth.Save();
            }

            WriteProviderEntry(run, stagedPaths);
        }

        WriteInlineProviders(model, transaction, stagedPaths);
        WriteProfileAndUsages(model, runs, stagedPaths);
        WriteDecisions(model, runs, stagedPaths);
    }

    /// <summary>
    /// The providers a usage step added inline: each entry through the same provider-store writer
    /// as the walk's own, and each typed key through the staged auth file — one transaction, one
    /// review, so an inline provider cannot land without the usage that selected it.
    /// </summary>
    private static void WriteInlineProviders(
        SetupWizardModel model,
        SetupWizardTransaction transaction,
        CommitPaths paths
    )
    {
        if (model.InlineProviders.Count == 0)
        {
            return;
        }

        AuthStore auth = AuthStore.Load(transaction.Auth);
        foreach (WizardInlineProvider inline in model.InlineProviders)
        {
            ProviderEntry entry = StagedEntry(inline.Provider, inline.Credential);
            if (
                inline.Credential is { Key: { Length: > 0 } secret }
                && !IsReference(inline.Credential.Reference)
            )
            {
                auth.Set(entry.Name, secret);
            }

            SaveEntry(entry, paths);
        }

        auth.Save();
    }

    /// <summary>The paths a commit touches; tests pass temp paths, the session its stores.</summary>
    internal sealed record CommitPaths(string SettingsPath, string ProviderStorePath);

    // --- provider entry ---

    private static void WriteProviderEntry(WizardProviderRun run, CommitPaths paths)
    {
        WizardProviderChoice provider = run.Provider;
        if (provider.Custom)
        {
            // A typed custom key pairs with the settings-section entry: the writer is the one
            // credential login uses, so the wizard's custom entry cannot drift from it. The
            // section shape names its model, so this pair needs the model step answered; a
            // skipped model (or a reference-form credential) writes an explicit provider-store
            // entry instead — the same written shape, with the reference stored.
            if (
                run.Credential?.Key is { } secretKey
                && !IsReference(run.Credential.Reference)
                && run.Model is { } modelChoice
            )
            {
                CommitTypedCustomEntry(provider, modelChoice, paths);
            }
            else
            {
                SaveEntry(StagedEntry(provider, run.Credential), paths);
            }

            return;
        }

        SaveEntry(StagedEntry(provider, run.Credential), paths);
    }

    /// <summary>
    /// The provider entry the staged answer commits, built without writing anything: the check
    /// steps resolve the same entry the commit will store, so a check cannot pass on an entry the
    /// commit then writes differently. A custom provider is the family-keyed entry the same
    /// answers write; a preset entry carries the preset's URL, the walk's region and — when the
    /// walk resolved another family — that family's URL and placement.
    /// </summary>
    internal static ProviderEntry StagedEntry(SetupWizardModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        WizardProviderChoice provider =
            model.Provider
            ?? throw new InvalidDataException(
                "setup: the provider step has no answer; no provider entry can be built."
            );
        return StagedEntry(provider, model.Credential);
    }

    /// <summary>The entry one provider answer, its credential and its model commit, built without
    /// writing anything. The one-argument overload is the walk's own provider; a provider added
    /// inline from a usage step or collected in an earlier provider run is built here too, so its
    /// check and its commit see one entry.</summary>
    internal static ProviderEntry StagedEntry(
        WizardProviderChoice provider,
        WizardCredentialChoice? credential
    )
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (provider.Custom)
        {
            return CustomEntry(provider, credential);
        }

        ProviderPreset? preset = ProviderPresets.Find(provider.PresetKey);
        if (preset is null)
        {
            throw new InvalidDataException(
                $"setup: provider '{provider.Id}' is not a preset; its entry cannot be written."
            );
        }

        // The family the walk answered: the preset's own default unless the flag or the wizard
        // resolved another one for this model (#569). A declared override writes the family's URL
        // and placement on the entry, so the entry declares the family the profile names.
        if (!ProviderApiFamilies.TryParse(provider.ApiFamily, out ProviderApiFamily chosen))
        {
            throw new InvalidDataException(
                $"setup: API family '{provider.ApiFamily}' is not a supported family; "
                    + $"supported families: {ProviderApiFamilies.Supported()}."
            );
        }

        bool overridesFamily = chosen != preset.DefaultFamily;
        return CreateEntry(
            provider.Id,
            preset.Id,
            preset.BaseUrl,
            CredentialReferenceOf(provider, credential),
            provider.ModelsUrl ?? preset.ModelsUrl,
            familyUrl: overridesFamily ? preset.UrlFor(chosen) ?? preset.BaseUrl : null,
            family: chosen,
            placement: overridesFamily ? preset.PlacementFor(chosen) : null,
            region: provider.Region
        );
    }

    private static void CommitTypedCustomEntry(
        WizardProviderChoice provider,
        WizardModelChoice modelChoice,
        CommitPaths paths
    )
    {
        if (
            !CustomProvider.TryCreate(
                provider.Id,
                provider.Endpoint!.AbsoluteUri,
                modelChoice.ModelId,
                catalogName: null,
                out CustomProvider? custom,
                out string? error,
                provider.ApiFamily,
                provider.Placement,
                provider.ModelsUrl?.AbsoluteUri,
                null,
                provider.Region
            ) || custom is null
        )
        {
            throw new InvalidDataException(
                $"setup: the custom provider entry was refused: {error}"
            );
        }

        SettingsStore.WriteProvider(custom, paths.SettingsPath);
    }

    private static void SaveEntry(ProviderEntry entry, CommitPaths paths)
    {
        // The staged copy was seeded from the target under the target claim, so it is the document
        // this commit merges into: an entry another process saved while the walk was answered
        // survives instead of being replaced by the walk's pre-commit reading.
        IEnumerable<ProviderEntry> kept = ProviderStore
            .ReadEntries(paths.ProviderStorePath)
            .Where(existing => !string.Equals(existing.Name, entry.Name, StringComparison.Ordinal));
        ProviderStore.Save(
            paths.ProviderStorePath,
            [.. kept.Append(entry).OrderBy(candidate => candidate.Name, StringComparer.Ordinal)]
        );
    }

    /// <summary>
    /// The provider entry the walk writes. Name, kind and base URL come from the answer; the
    /// credential reference is the staged reference (<c>env:&lt;VARIABLE&gt;</c> or
    /// <c>auth:&lt;name&gt;</c>) or the provider id's stored key; the optional region is the
    /// answer's region, kept as the entry's metadata. An unknown family is refused by name before
    /// anything is written.
    /// </summary>
    private static ProviderEntry CreateEntry(
        string name,
        string kind,
        Uri baseUrl,
        string credentialReference,
        Uri? modelsUrl,
        Uri? familyUrl = null,
        ProviderApiFamily family = ProviderApiFamily.OpenAiChat,
        CredentialPlacement? placement = null,
        string? region = null
    )
    {
        Dictionary<ProviderApiFamily, Uri>? baseUrls = familyUrl is not null
            ? new Dictionary<ProviderApiFamily, Uri> { [family] = familyUrl }
            : null;
        Dictionary<ProviderApiFamily, CredentialPlacement>? placements =
            placement is not null && familyUrl is not null
                ? new Dictionary<ProviderApiFamily, CredentialPlacement> { [family] = placement }
                : null;
        bool valid = ProviderEntry.TryCreate(
            name,
            kind,
            baseUrl,
            credentialReference,
            headers: null,
            region: region,
            baseUrls,
            modelsUrl,
            placements,
            out ProviderEntry? entry,
            out string? error
        );
        if (!valid || entry is null)
        {
            throw new InvalidDataException(
                $"setup: the provider entry '{name}' was refused: {error}"
            );
        }

        return entry;
    }

    /// <summary>
    /// The custom entry: the family's own URL keyed per family, the credential placement the walk
    /// chose, its models URL, and the credential reference if the step picked one.
    /// </summary>
    private static ProviderEntry CustomEntry(
        WizardProviderChoice provider,
        WizardCredentialChoice? credential
    )
    {
        if (!ProviderApiFamilies.TryParse(provider.ApiFamily, out ProviderApiFamily family))
        {
            throw new InvalidDataException(
                $"setup: API family '{provider.ApiFamily}' is not a supported family; "
                    + $"supported families: {ProviderApiFamilies.Supported()}."
            );
        }

        return CreateEntry(
            provider.Id,
            CustomProvider.KindFor(family),
            provider.Endpoint!,
            CredentialReferenceOf(provider, credential),
            provider.ModelsUrl,
            familyUrl: provider.Endpoint,
            family: family,
            placement: provider.Placement,
            region: provider.Region
        );
    }

    /// <summary>
    /// The credential reference the entry carries: the staged reference when the step picked one,
    /// otherwise the provider id's key in the auth store (even for a custom provider — the custom
    /// entry a typed key pairs with stores no reference of its own, and its loaded shape is
    /// <c>auth:&lt;id&gt;</c>).
    /// </summary>
    private static string CredentialReferenceOf(
        WizardProviderChoice provider,
        WizardCredentialChoice? credential
    ) =>
        credential is { Key: null } referenced && IsReference(referenced.Reference)
            ? referenced.Reference
            : $"auth:{provider.Id}";

    private static bool IsReference(string value) =>
        value.StartsWith("env:", StringComparison.Ordinal)
        || value.StartsWith("auth:", StringComparison.Ordinal);

    // --- profiles and usages ---

    private static void WriteProfileAndUsages(
        SetupWizardModel model,
        IReadOnlyList<WizardProviderRun> runs,
        CommitPaths paths
    )
    {
        WizardDecisionsChoice? decisions = model.Decisions;
        foreach (WizardProviderRun run in runs)
        {
            if (run.ChatProfile is not { } chat)
            {
                continue;
            }

            if (run.Provider == model.Provider && model.IsCarried(SetupStep.ChatProfile))
            {
                continue;
            }

            // The same shared rule the wizard step and the next startup apply: no writer may
            // persist a chat usage on a decision-only family.
            UsageProfileValidation.RequireChatFamily(
                chat.Name,
                chat.ModelId,
                ChatFamily(run) ?? run.Model?.ApiFamily,
                SetupState.ChatUsage
            );

            // The chat profile's routing description is part of the same profile entry, so one
            // writer pass carries both (settings: profiles.<name>.description). The family is
            // written when the walk resolved one the entry would not resolve by itself (#569),
            // so the family the model check used is the family the profile later resolves.
            SettingsStore.WriteProfile(
                ToModelProfile(
                    chat.Name,
                    run.Provider,
                    chat.ModelId,
                    chat.ReasoningLevel,
                    Description(decisions, chat.Name),
                    apiFamily: ChatFamily(run)
                ),
                paths.SettingsPath
            );
            if (run.Provider != model.Provider || model.BindChatUsage)
            {
                SettingsStore.WriteUsage("chat", chat.Name, paths.SettingsPath);
            }

            if (run.FastProfile)
            {
                SettingsStore.WriteUsage("fast", chat.Name, paths.SettingsPath);
            }
        }

        if (model.FastUsage is { } fast && !model.IsCarried(SetupStep.FastProfile))
        {
            if (!fast.ReuseChatProfile)
            {
                // A model on another provider (or another model of this one) needs its own
                // profile; the reuse route binds the chat profile and writes no second entry.
                SettingsStore.WriteProfile(
                    new ModelProfile(
                        fast.ProfileName,
                        fast.ProviderId,
                        fast.ModelId,
                        ReasoningLevel: fast.ReasoningLevel,
                        ApiFamily: ProfileFamily(fast.ProviderId, fast.ApiFamily)
                    ),
                    paths.SettingsPath
                );
            }

            SettingsStore.WriteUsage("fast", fast.ProfileName, paths.SettingsPath);
        }

        if (model.ChatUsageBinding is { } bound)
        {
            SettingsStore.WriteUsage(SetupState.ChatUsage, bound, paths.SettingsPath);
        }

        if (model.DecisionProfile is { } decision && !model.IsCarried(SetupStep.DecisionProfile))
        {
            SettingsStore.WriteProfile(
                new ModelProfile(
                    decision.ProfileName,
                    decision.ProviderId,
                    decision.ModelId,
                    ApiFamily: decision.Native
                        ? ProviderApiFamilies.SystemOne
                        : ProfileFamily(decision.ProviderId, decision.ApiFamily),
                    Description: Description(decisions, decision.ProfileName)
                ),
                paths.SettingsPath
            );
        }
    }

    /// <summary>
    /// The chat profile's <c>apiFamily</c>: the family the walk resolved for the model, written
    /// only when it differs from what the provider entry resolves on its own — the preset's
    /// default family, or the custom entry's one — so an ordinary single-family walk writes
    /// nothing extra while an override is recorded where it applies.
    /// </summary>
    internal static string? ChatFamily(SetupWizardModel model)
    {
        if (model.Provider is not { } provider || model.Model is not { } choice)
        {
            return null;
        }

        return ProfileFamily(provider.PresetKey, choice.ApiFamily);
    }

    /// <summary>One collected run's chat family: the model the run recorded, resolved the way
    /// <see cref="ChatFamily(SetupWizardModel)"/> resolves the active run's.</summary>
    private static string? ChatFamily(WizardProviderRun run) =>
        run.Model is { } choice ? ProfileFamily(run.Provider.PresetKey, choice.ApiFamily) : null;

    /// <summary>A usage profile's family, written only when it differs from the default family the
    /// named provider's entry resolves on its own; a provider that is not a preset keeps its own
    /// declared family and writes nothing extra.</summary>
    private static string? ProfileFamily(string providerId, string apiFamily)
    {
        string? declared = ProviderPresets.Find(providerId) is { } preset
            ? ProviderApiFamilies.Name(preset.DefaultFamily)
            : null;
        return string.Equals(apiFamily, declared, StringComparison.Ordinal) ? null : apiFamily;
    }

    private static ModelProfile ToModelProfile(
        string name,
        WizardProviderChoice provider,
        string modelId,
        string? reasoningLevel = null,
        string? description = null,
        string? apiFamily = null
    ) =>
        new(
            name,
            provider.Id,
            modelId,
            ReasoningLevel: reasoningLevel,
            ApiFamily: apiFamily,
            Description: description
        );

    private static string? Description(WizardDecisionsChoice? decisions, string profileName) =>
        decisions?.RoutingDescriptions.TryGetValue(profileName, out string? description) == true
        && !string.IsNullOrWhiteSpace(description)
            ? description
            : null;

    private static void WriteDecisions(
        SetupWizardModel model,
        IReadOnlyList<WizardProviderRun> runs,
        CommitPaths paths
    )
    {
        WizardUsageChoice? decision = model.DecisionProfile;
        if (decision is not null && !model.IsCarried(SetupStep.DecisionProfile))
        {
            // Bind without enabling any consumer (spec: The full walk, step 7).
            SettingsStore.WriteDecisionsKey("profile", decision.ProfileName, paths.SettingsPath);
        }

        if (model.Decisions is not { } choices)
        {
            // Accepting the defaults writes nothing for the layer.
            return;
        }

        if (choices.Approvals is { } approvals)
        {
            SettingsStore.WriteDecisionsKey("approvals", approvals, paths.SettingsPath);
        }

        if (choices.Routing is { } routing)
        {
            SettingsStore.WriteDecisionsKey("routing", routing, paths.SettingsPath);
            // The profiles the multi-select confirmed, in the order they were offered; the walk's
            // candidates are only the fallback for a choice staged without them.
            IReadOnlyList<string> routingProfiles =
                choices.RoutingProfiles.Count > 0
                    ? choices.RoutingProfiles
                    : RoutingProfiles(model, runs, model.DecisionProfile);
            var list = new JsonArray([.. routingProfiles.Select(name => (JsonNode)name)]);
            SettingsStore.WriteDecisionsKey("routingProfiles", list, paths.SettingsPath);
        }

        if (choices.Tool is { } tool)
        {
            SettingsStore.WriteDecisionsKey("tool", tool, paths.SettingsPath);
        }

        if (choices.Questions is { } questions)
        {
            SettingsStore.WriteDecisionsKey("questions", questions, paths.SettingsPath);
        }
    }

    /// <summary>The routing bound: the walk's chat and decision profiles, plus this walk's other
    /// configured profiles the settings already name; empty means routing cannot turn on here.</summary>
    private static IReadOnlyList<string> RoutingProfiles(
        SetupWizardModel model,
        IReadOnlyList<WizardProviderRun> runs,
        WizardUsageChoice? decision
    )
    {
        List<string> names = [];
        if (
            model.ChatProfile is { } carried
            && !names.Contains(carried.Name, StringComparer.Ordinal)
        )
        {
            names.Add(carried.Name);
        }

        foreach (WizardProviderRun run in runs)
        {
            if (run.ChatProfile is { } chat && !names.Contains(chat.Name, StringComparer.Ordinal))
            {
                names.Add(chat.Name);
            }
        }

        if (decision is not null)
        {
            names.Add(decision.ProfileName);
        }

        foreach (string existing in model.ExistingRoutingProfiles)
        {
            if (!names.Contains(existing, StringComparer.Ordinal))
            {
                names.Add(existing);
            }
        }

        return names;
    }
}
