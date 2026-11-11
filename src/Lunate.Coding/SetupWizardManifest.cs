using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding;

/// <summary>The three files a confirmed wizard commit can write.</summary>
internal enum SetupFile
{
    Providers,
    Auth,
    Settings,
}

/// <summary>One staged value and the file the commit writes it to. <see cref="Group"/> names the
/// provider, profile, usage or decisions block the value belongs to.</summary>
internal sealed record SetupWrite(SetupFile File, string Group, string Label, string Value);

/// <summary>One setting the walk did not collect (skipped, carried or defaulted), with the command
/// and file that change it later.</summary>
internal sealed record SetupUnchanged(string Label, string Value);

/// <summary>
/// The setup wizard's pre-write manifest and post-write receipt. <see cref="Writes"/> derives every
/// staged value and its target from the same staged model <see cref="SetupWizardCommit"/> commits;
/// the renderers group by file and wrap every fact to the terminal width with
/// <see cref="CellText.Wrap"/>, so the review stays readable at 40 columns. No value here is a
/// secret: a typed key is named by its provider, never printed.
/// </summary>
internal static partial class SetupWizardManifest
{
    internal const int DefaultWidth = 80;

    internal static IReadOnlyList<SetupWrite> Writes(SetupWizardModel wizard)
    {
        ArgumentNullException.ThrowIfNull(wizard);
        List<SetupWrite> writes = [];
        AddProviders(wizard, writes);
        AddInlineProviders(wizard, writes);
        AddProfilesAndUsages(wizard, writes);
        AddDecisions(wizard, writes);
        return writes;
    }

    private static void AddProviders(SetupWizardModel wizard, List<SetupWrite> writes)
    {
        foreach (WizardProviderRun run in wizard.AllProviderRuns)
        {
            // A carried provider is the entry walk's prefill, not a staged write; every sealed run
            // is this run's own answer and is written.
            if (run.Provider == wizard.Provider && wizard.IsCarried(SetupStep.Provider))
            {
                continue;
            }

            SetupFile file = ProviderFile(run);
            string group = "provider " + run.Provider.Id;
            AddProviderFacts(writes, file, group, run.Provider);

            if (
                run.Credential is { Key: null } reference
                && reference.Reference.Length > 0
                && file == SetupFile.Providers
            )
            {
                writes.Add(new(file, group, "credential reference", reference.Reference));
            }
            else if (run.Credential is { Key: not null } && file == SetupFile.Providers)
            {
                writes.Add(new(file, group, "credential reference", $"auth:{run.Provider.Id}"));
            }

            if (file == SetupFile.Settings && run.Model is { } model)
            {
                writes.Add(new(file, group, "model", model.ModelId));
            }

            if (run.Credential is { Key: not null })
            {
                writes.Add(
                    new(
                        SetupFile.Auth,
                        "key " + run.Provider.Id,
                        "stored",
                        "the key you typed — the value is never shown"
                    )
                );
            }
        }
    }

    /// <summary>A provider a usage step added inline: its own providers.json entry — and, for a
    /// typed key, its auth.json key — through the same writers as the walk's own provider.</summary>
    private static void AddInlineProviders(SetupWizardModel wizard, List<SetupWrite> writes)
    {
        foreach (WizardInlineProvider inline in wizard.InlineProviders)
        {
            string group = "provider " + inline.Provider.Id;
            AddProviderFacts(writes, SetupFile.Providers, group, inline.Provider);
            if (inline.Credential is { Key: null } reference && reference.Reference.Length > 0)
            {
                writes.Add(
                    new(SetupFile.Providers, group, "credential reference", reference.Reference)
                );
            }
            else if (inline.Credential is { Key: not null })
            {
                writes.Add(
                    new(
                        SetupFile.Auth,
                        "key " + inline.Provider.Id,
                        "stored",
                        "the key you typed — the value is never shown"
                    )
                );
            }
        }
    }

    /// <summary>The provider entry's own facts: the file-dependent name and kind, the custom
    /// endpoint's family, placement, region and models URL.</summary>
    private static void AddProviderFacts(
        List<SetupWrite> writes,
        SetupFile file,
        string group,
        WizardProviderChoice provider
    )
    {
        if (file == SetupFile.Providers)
        {
            writes.Add(new(file, group, "name", provider.Id));
            writes.Add(new(file, group, "kind", Kind(provider)));
        }

        if (Family(provider) is { } family)
        {
            if (BaseUrl(provider) is { } baseUrl)
            {
                writes.Add(new(file, group, $"base URL ({family})", baseUrl.AbsoluteUri));
            }

            writes.Add(new(file, group, $"credential placement ({family})", Placement(provider)));
        }

        if (provider.Region is { } region)
        {
            writes.Add(new(file, group, "region", region));
        }

        if (ModelsUrl(provider) is { } modelsUrl)
        {
            writes.Add(new(file, group, "models URL", modelsUrl.AbsoluteUri));
        }
    }

    private static void AddProfilesAndUsages(SetupWizardModel wizard, List<SetupWrite> writes)
    {
        foreach (WizardProviderRun run in wizard.AllProviderRuns)
        {
            if (run.ChatProfile is not { } chat)
            {
                continue;
            }

            bool active = run.Provider == wizard.Provider;
            if (active && wizard.IsCarried(SetupStep.ChatProfile))
            {
                continue;
            }

            string group = "profile " + chat.Name;
            writes.Add(new(SetupFile.Settings, group, "profile", chat.Name));
            writes.Add(new(SetupFile.Settings, group, "provider", run.Provider.Id));
            writes.Add(new(SetupFile.Settings, group, "model", chat.ModelId));
            if (chat.ReasoningLevel is { } thinking)
            {
                writes.Add(new(SetupFile.Settings, group, "reasoning level", thinking));
            }

            if (ChatFamily(run) is { } family)
            {
                writes.Add(new(SetupFile.Settings, group, "api family", family));
            }

            if (Description(wizard, chat.Name) is { } description)
            {
                writes.Add(new(SetupFile.Settings, group, "description", description));
            }

            if (!active || wizard.BindChatUsage)
            {
                writes.Add(new(SetupFile.Settings, "usage chat", "profile", chat.Name));
            }

            if (run.FastProfile)
            {
                writes.Add(new(SetupFile.Settings, "usage fast", "profile", chat.Name));
            }
        }

        if (wizard.ChatUsageBinding is { } bound)
        {
            writes.Add(new(SetupFile.Settings, "usage chat", "profile", bound));
        }

        // A fast usage on another profile carries its own provider and model; the reuse answer
        // binds the chat profile and writes no second entry, the way the commit decides.
        if (
            wizard.FastUsage is { } fast
            && !fast.ReuseChatProfile
            && !wizard.IsCarried(SetupStep.FastProfile)
        )
        {
            AddUsageProfile(wizard, fast, SetupFile.Settings, writes);
            writes.Add(new(SetupFile.Settings, "usage fast", "profile", fast.ProfileName));
        }

        if (wizard.DecisionProfile is { } decision && !wizard.IsCarried(SetupStep.DecisionProfile))
        {
            AddUsageProfile(wizard, decision, SetupFile.Settings, writes);
        }
    }

    /// <summary>One usage profile entry: the profile, the provider that serves it and its model, with
    /// the reasoning level, API family and routing description when the commit writes them.</summary>
    private static void AddUsageProfile(
        SetupWizardModel wizard,
        WizardUsageChoice usage,
        SetupFile file,
        List<SetupWrite> writes
    )
    {
        string group = "profile " + usage.ProfileName;
        writes.Add(new(file, group, "profile", usage.ProfileName));
        writes.Add(new(file, group, "provider", usage.ProviderId));
        writes.Add(new(file, group, "model", usage.ModelId));
        if (usage.ReasoningLevel is { } thinking)
        {
            writes.Add(new(file, group, "reasoning level", thinking));
        }

        string? apiFamily = usage.Native
            ? ProviderApiFamilies.Name(ProviderApiFamily.SystemOne)
            : UsageFamily(usage);
        if (apiFamily is { } family)
        {
            writes.Add(new(file, group, "api family", family));
        }

        if (Description(wizard, usage.ProfileName) is { } description)
        {
            writes.Add(new(file, group, "description", description));
        }
    }

    private static void AddDecisions(SetupWizardModel wizard, List<SetupWrite> writes)
    {
        if (wizard.DecisionProfile is { } decision && !wizard.IsCarried(SetupStep.DecisionProfile))
        {
            writes.Add(new(SetupFile.Settings, "decisions", "profile", decision.ProfileName));
        }

        if (wizard.Decisions is not { } choices)
        {
            return;
        }

        if (choices.Approvals is { } approvals)
        {
            writes.Add(new(SetupFile.Settings, "decisions", "approvals", approvals));
        }

        if (choices.Routing is { } routing)
        {
            writes.Add(new(SetupFile.Settings, "decisions", "routing", routing));
            writes.Add(
                new(
                    SetupFile.Settings,
                    "decisions",
                    "routing profiles",
                    string.Join(", ", RoutingProfiles(wizard, wizard.DecisionProfile))
                )
            );
        }

        if (choices.Tool is { } tool)
        {
            writes.Add(new(SetupFile.Settings, "decisions", "tool", tool));
        }

        if (choices.Questions is { } questions)
        {
            writes.Add(new(SetupFile.Settings, "decisions", "questions", questions));
        }
    }

    /// <summary>The file the provider answer reaches: a typed custom key pairs with the settings
    /// section, every other entry is a providers.json entry. The same predicate the commit uses.</summary>
    private static SetupFile ProviderFile(WizardProviderRun run) =>
        run.Provider.Custom && run.Credential is { Key: not null } && run.Model is not null
            ? SetupFile.Settings
            : SetupFile.Providers;
}
