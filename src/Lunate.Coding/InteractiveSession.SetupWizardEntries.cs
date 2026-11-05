using Lunate.Ai;

namespace Lunate.Coding;

// The configured install's entry menu and its scoped entry walks (design §8): /setup on a
// configured install offers add a provider, add a profile, change the chat profile, edit the
// decisions layer, or walk everything. Each entry walks only the steps it names, prefilled with
// the current values, and writes only what it collected — the entries reuse the same step
// drivers, summary, confirm and commit the full walk runs (one wizard, one writer set).

internal sealed partial class InteractiveSession
{
    /// <summary><c>/setup</c>'s dispatch: a first run keeps the full walk, a configured install
    /// opens the entry menu (design §8). Refused while a turn runs, like the other state-changers
    /// in the busy-refusal list.</summary>
    internal void HandleSetupCommand()
    {
        if (RefusedWhileBusy())
        {
            return;
        }

        if (_wizard is not null)
        {
            Notify("the setup wizard is already open", NoticeLevel.Warning);
            return;
        }

        if (SetupState.Check(_settings, _providerEntries) is not SetupState.Reason.NotApplicable)
        {
            OpenSetupWizard();
            return;
        }

        var wizard = new SetupWizardModel
        {
            ExistingRoutingProfiles = [.. _settings.Decisions.RoutingProfiles],
        };
        _wizard = wizard;
        _turnActive = true;
        LoginTask = _ = RunSetupMenuAsync(wizard, _lifetime.Token);
    }

    private async Task RunSetupMenuAsync(SetupWizardModel wizard, CancellationToken ct) =>
        await RunWizardTaskAsync(
            wizard,
            async token =>
            {
                string? answer = await PickAsync(
                        "Setup — what do you want to configure?",
                        ["Add a provider", "Add a profile", "Change the chat profile", "Edit the decisions layer", "Walk everything"],
                        ["provider", "profile", "chat", "decisions", "walk"],
                        PickerKind.WizardChoice,
                        token
                    )
                    .ConfigureAwait(false);
                if (answer is null)
                {
                    Notify("setup closed — nothing was changed", NoticeLevel.Info);
                    return;
                }

                WizardEntry entry = answer switch
                {
                    "provider" => WizardEntry.AddProvider,
                    "profile" => WizardEntry.AddProfile,
                    "chat" => WizardEntry.ChangeChatProfile,
                    "decisions" => WizardEntry.EditDecisions,
                    _ => WizardEntry.WalkEverything,
                };
                wizard.Begin(entry);
                PrefillEntry(wizard, entry);
                MarkEntrySkips(wizard);
                Notify(
                    entry switch
                    {
                        WizardEntry.AddProvider => "setup — add a provider; Esc ends this run",
                        WizardEntry.AddProfile => "setup — add a profile; Esc ends this run",
                        WizardEntry.ChangeChatProfile =>
                            "setup — change the chat profile; Esc ends this run",
                        WizardEntry.EditDecisions =>
                            "setup — edit the decisions layer; Esc ends this run",
                        _ =>
                            "setup: configure a provider, a model and a profile — Esc at any prompt "
                                + "ends the wizard, and nothing is written before the summary confirms",
                    },
                    NoticeLevel.Info
                );
                await WalkAsync(wizard, token).ConfigureAwait(false);
            },
            ct
        )
            .ConfigureAwait(false);

    /// <summary>The steps the entry never walks and never carried are named skipped: the summary
    /// shows every skipped step, and no entry invents a value for them (spec: the full walk).</summary>
    private static void MarkEntrySkips(SetupWizardModel wizard)
    {
        foreach (SetupStep step in SetupWizardModel.Order)
        {
            if (
                step is SetupStep.Summary
                || wizard.Entry is WizardEntry.WalkEverything
                || wizard.IsCarried(step)
            )
            {
                continue;
            }

            if (step < wizard.FirstWalked || step > wizard.LastWalked)
            {
                wizard.Skip(step);
            }
        }
    }

    /// <summary>The prefill: the current configuration's provider, credential, model and chat
    /// profile the entry will show as current values and reuse. Carried answers are context —
    /// the commit never writes them back (an entry writes only what it collected).</summary>
    private void PrefillEntry(SetupWizardModel wizard, WizardEntry entry)
    {
        ModelProfile? current = CurrentChatProfile();
        ProviderEntry? providerEntry = ProviderEntryFor(current);
        switch (entry)
        {
            case WizardEntry.AddProvider:
                break;
            case WizardEntry.AddProfile:
                if (providerEntry is { } addEntry)
                {
                    wizard.CarryContext(
                        CarriedProvider(addEntry),
                        new WizardCredentialChoice(addEntry.CredentialReference),
                        null,
                        null
                    );
                    _listing = CatalogListing(addEntry.Name);
                }

                break;
            case WizardEntry.ChangeChatProfile:
                if (providerEntry is { } changeEntry && current is { } profile)
                {
                    wizard.CarryContext(
                        CarriedProvider(changeEntry),
                        new WizardCredentialChoice(changeEntry.CredentialReference),
                        CarriedModel(profile, changeEntry),
                        null
                    );
                }

                break;
            case WizardEntry.EditDecisions:
                if (current is { } decisionsProfile)
                {
                    wizard.CarryContext(null, null, null, CarriedChat(decisionsProfile));
                }

                break;
        }
    }

    private ModelProfile? CurrentChatProfile() =>
        _settings.Usages.TryGetValue(SetupState.ChatUsage, out string? name)
        && _settings.Profiles.TryGetValue(name, out ModelProfile? profile)
            ? profile
            : null;

    /// <summary>The provider the entry's carried context rides: the current chat profile's
    /// configured provider entry, or the sole provider entry when no chat profile is bound.</summary>
    private ProviderEntry? ProviderEntryFor(ModelProfile? current)
    {
        if (current?.Provider is { } wanted)
        {
            return _providerEntries.FirstOrDefault(entry =>
                string.Equals(entry.Name, wanted, StringComparison.Ordinal)
            );
        }

        return _providerEntries.Count is 1 ? _providerEntries[0] : null;
    }

    /// <summary>The provider entry as the wizard's provider choice: a preset-named entry rides
    /// its preset; anything else carries its own family URL, placement and models URL.</summary>
    private static WizardProviderChoice CarriedProvider(ProviderEntry entry)
    {
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

        ProviderApiFamily family =
            entry.BaseUrls is { Count: > 0 } table ? table.Keys.First() : entry.DefaultFamily;
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

    /// <summary>The settings profile's model as the carried model choice; its declared reasoning
    /// style comes from the catalog, exactly the discovery listing's own metadata source.</summary>
    private WizardModelChoice CarriedModel(ModelProfile profile, ProviderEntry entry)
    {
        ModelInfo? known = _catalog.Find(profile.Model);
        ProviderApiFamily family = entry.DefaultFamily;
        return new WizardModelChoice(profile.Model, ProviderApiFamilies.Name(family), known?.Reasoning);
    }

    /// <summary>The settings profile as the carried chat profile; the walk only names its kind.</summary>
    private static WizardProfileChoice CarriedChat(ModelProfile profile) =>
        new(profile.Name, profile.Model, profile.ReasoningLevel);

    /// <summary>The model listing an entry walk shows when no connection test ran this run: the
    /// catalog's entries for the carried provider, the same shape discovery's listing has.</summary>
    private IReadOnlyList<WizardModelListing> CatalogListing(string providerId) =>
        [
            .. _catalog
                .Models.Where(model =>
                    string.Equals(model.Provider, providerId, StringComparison.Ordinal)
                )
                .Select(model => new WizardModelListing(
                    model.Id,
                    model.ApiFamily,
                    model.Reasoning,
                    model.ContextWindow
                )),
        ];

    /// <summary>"Change the chat profile"'s answer: the configured profiles, the current binding
    /// highlighted. Choosing the current one changes nothing and ends the run.</summary>
    private async Task<WizardMove> ChatUsageStepAsync(
        SetupWizardModel wizard,
        CancellationToken ct
    )
    {
        List<string> names = [.. _settings.Profiles.Keys];
        if (names.Count == 0)
        {
            Notify(
                "no profile is configured yet; the entry menu offers adding a profile",
                NoticeLevel.Warning
            );
            return WizardMove.Abort;
        }

        string current = _settings.Usages.GetValueOrDefault(SetupState.ChatUsage) ?? names[0];
        List<string> labels = [];
        foreach (string name in names)
        {
            labels.Add(name == current ? $"{name} (current)" : name);
        }

        string? chosen = await PickAsync(
                "Profile to use for chat",
                labels,
                names,
                PickerKind.WizardChoice,
                ct,
                current
            )
            .ConfigureAwait(false);
        if (chosen is null)
        {
            return WizardMove.Abort;
        }

        if (chosen == current)
        {
            // The entry's change is the binding itself; keeping the current profile writes
            // nothing and the walk's abort notice says so.
            return WizardMove.Abort;
        }

        wizard.RecordChatUsageBinding(chosen);
        return WizardMove.Advance;
    }
}
