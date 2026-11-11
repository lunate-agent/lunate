using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding;

internal static partial class SetupWizardManifest
{
    internal static IReadOnlyList<string> Render(SetupWizardModel wizard, int width = DefaultWidth)
    {
        ArgumentNullException.ThrowIfNull(wizard);
        width = Math.Max(1, width);
        List<string> lines = [];
        if (wizard.Entry is WizardEntry.WalkEverything)
        {
            lines.AddRange(
                WrapLine(
                    wizard.IsQuick
                        ? "setup mode: quick — provider, credential, model and chat profile"
                        : "setup mode: detailed — every setting was asked",
                    width,
                    0
                )
            );
        }

        AddChecks(wizard, lines, width);

        IReadOnlyList<SetupWrite> writes = Writes(wizard);
        foreach (SetupFile file in Files())
        {
            SetupWrite[] entries = [.. writes.Where(write => write.File == file)];
            if (entries.Length == 0)
            {
                continue;
            }

            lines.Add(FileName(file));
            string? group = null;
            foreach (SetupWrite entry in entries)
            {
                if (!string.Equals(group, entry.Group, StringComparison.Ordinal))
                {
                    group = entry.Group;
                    lines.AddRange(WrapLine("  " + group, width, 4));
                }

                lines.AddRange(Fact("    ", entry.Label, entry.Value, width));
            }
        }

        IReadOnlyList<SetupUnchanged> unchanged = Unchanged(wizard);
        if (unchanged.Count > 0)
        {
            lines.Add("left at default / unchanged");
            foreach (SetupUnchanged entry in unchanged)
            {
                lines.AddRange(Fact("  ", entry.Label, entry.Value, width));
            }
        }

        return lines;
    }

    internal static IReadOnlyList<string> RenderReceipt(
        SetupWizardModel wizard,
        int width = DefaultWidth
    )
    {
        ArgumentNullException.ThrowIfNull(wizard);
        width = Math.Max(1, width);
        IReadOnlyList<SetupWrite> writes = Writes(wizard);
        if (writes.Count == 0)
        {
            return ["setup written: nothing — every step was skipped", "back to the session"];
        }

        List<string> lines = ["setup written"];
        foreach (SetupFile file in Files())
        {
            SetupWrite[] entries = [.. writes.Where(write => write.File == file)];
            if (entries.Length == 0)
            {
                continue;
            }

            string items = string.Join(
                ", ",
                entries.Select(entry => entry.Group).Distinct(StringComparer.Ordinal)
            );
            string line = $"{FileName(file)} — {items}";
            if (CellText.Width(line) <= width)
            {
                lines.Add(line);
            }
            else
            {
                lines.Add($"{FileName(file)} —");
                lines.AddRange(WrapLine("  " + items, width, 4));
            }
        }

        lines.AddRange(WrapLine("change later: /setup · /settings · lunate auth login", width, 2));
        lines.Add("back to the session");
        return lines;
    }

    private static void AddChecks(SetupWizardModel wizard, List<string> lines, int width)
    {
        List<string> checks = [];
        IReadOnlyList<WizardProviderRun> runs = wizard.AllProviderRuns;
        bool multiple = runs.Count > 1;
        foreach (WizardProviderRun run in runs)
        {
            string suffix = multiple ? $" ({run.Provider.Id})" : string.Empty;
            AddCheck(checks, "connection test" + suffix, run.ConnectionTest);
            AddCheck(checks, "model check" + suffix, run.ModelCheck, "the model");
        }

        if (wizard.FastUsage is { ReuseChatProfile: false })
        {
            AddCheck(checks, "fast check", wizard.FastCheck, "the fast profile", notRun: true);
        }

        if (wizard.DecisionProfile is not null || wizard.DecisionCheck is not null)
        {
            AddCheck(
                checks,
                "decision check",
                wizard.DecisionCheck,
                "the decision profile",
                notRun: true
            );
        }

        if (checks.Count > 0)
        {
            lines.Add("checks (nothing is written)");
            foreach (string check in checks)
            {
                lines.AddRange(WrapLine("  " + check, width, 4));
            }
        }
    }

    private static void AddCheck(
        List<string> checks,
        string label,
        WizardCheckResult? result,
        string? subject = null,
        bool notRun = false
    )
    {
        switch (result?.Outcome)
        {
            case WizardProgress.Passed:
                checks.Add($"{label}: passed");
                break;
            case WizardProgress.Skipped:
                checks.Add($"{label}: skipped — {Unverified(subject)}");
                break;
            case WizardProgress.Failed:
                checks.Add($"{label}: failed ({result.Detail}) — {Unverified(subject)}");
                break;
            default:
                if (notRun)
                {
                    checks.Add($"{label}: not run — {Unverified(subject)}");
                }

                break;
        }
    }

    private static string Unverified(string? subject) =>
        subject is null ? "unverified" : subject + " stays unverified";

    private static string FileName(SetupFile file) =>
        file switch
        {
            SetupFile.Providers => "providers.json",
            SetupFile.Auth => "auth.json",
            _ => "settings.json",
        };

    private static IEnumerable<SetupFile> Files() =>
        [SetupFile.Providers, SetupFile.Auth, SetupFile.Settings];

    private static string Kind(WizardProviderChoice provider) =>
        provider.Custom
            ? Family(provider) is { } family
                ? CustomProvider.KindFor(ParseFamily(family))
                : provider.Id
            : provider.Id;

    private static string? Family(WizardProviderChoice provider) =>
        string.IsNullOrWhiteSpace(provider.ApiFamily) ? null : provider.ApiFamily;

    private static Uri? BaseUrl(WizardProviderChoice provider)
    {
        if (provider.Custom)
        {
            return provider.Endpoint;
        }

        ProviderPreset? preset = ProviderPresets.Find(provider.Id);
        if (preset is null)
        {
            return provider.Endpoint;
        }

        return
            Family(provider) is { } family && TryParseFamily(family, out ProviderApiFamily parsed)
            ? preset.UrlFor(parsed) ?? preset.BaseUrl
            : preset.BaseUrl;
    }

    private static Uri? ModelsUrl(WizardProviderChoice provider) =>
        provider.ModelsUrl
        ?? (provider.Custom ? null : ProviderPresets.Find(provider.Id)?.ModelsUrl);

    private static string Placement(WizardProviderChoice provider) =>
        provider.Placement.ToCompact();

    private static ProviderApiFamily ParseFamily(string family) =>
        TryParseFamily(family, out ProviderApiFamily parsed)
            ? parsed
            : ProviderApiFamily.OpenAiChat;

    private static bool TryParseFamily(string family, out ProviderApiFamily parsed) =>
        ProviderApiFamilies.TryParse(family, out parsed);

    private static string? ChatFamily(WizardProviderRun run) =>
        run.Model is { } model
        && !string.Equals(
            model.ApiFamily,
            DeclaredFamily(run.Provider.Id),
            StringComparison.Ordinal
        )
            ? model.ApiFamily
            : null;

    /// <summary>A usage profile's API family, named only when it differs from the family the
    /// provider preset declares — the same rule the chat profile uses.</summary>
    private static string? UsageFamily(WizardUsageChoice usage) =>
        string.Equals(usage.ApiFamily, DeclaredFamily(usage.ProviderId), StringComparison.Ordinal)
            ? null
            : usage.ApiFamily;

    private static string? DeclaredFamily(string providerId) =>
        ProviderPresets.Find(providerId) is { } preset
            ? ProviderApiFamilies.Name(preset.DefaultFamily)
            : null;

    private static string? Description(SetupWizardModel wizard, string profile) =>
        wizard.Decisions?.RoutingDescriptions.TryGetValue(profile, out string? description) == true
        && !string.IsNullOrWhiteSpace(description)
            ? description
            : null;

    private static IReadOnlyList<string> RoutingProfiles(
        SetupWizardModel wizard,
        WizardUsageChoice? decision
    )
    {
        List<string> names = [];
        if (wizard.ChatProfile is { } chat)
        {
            names.Add(chat.Name);
        }

        if (decision is not null)
        {
            names.Add(decision.ProfileName);
        }

        foreach (string existing in wizard.ExistingRoutingProfiles)
        {
            if (!names.Contains(existing, StringComparer.Ordinal))
            {
                names.Add(existing);
            }
        }

        return names;
    }

    private static IEnumerable<string> Fact(string indent, string label, string value, int width)
    {
        string one = $"{indent}{label}: {value}";
        if (CellText.Width(one) <= width)
        {
            yield return one;
            yield break;
        }

        string labelLine = $"{indent}{label}:";
        if (CellText.Width(labelLine) > width)
        {
            foreach (
                string part in CellText.Wrap(
                    labelLine.TrimStart(),
                    Math.Max(1, width - indent.Length)
                )
            )
            {
                yield return indent + part;
            }
        }
        else
        {
            yield return labelLine;
        }

        foreach (string part in CellText.Wrap(value, Math.Max(1, width - indent.Length - 2)))
        {
            yield return indent + "  " + part;
        }
    }

    private static IEnumerable<string> WrapLine(string text, int width, int indent)
    {
        if (CellText.Width(text) <= width)
        {
            yield return text;
            yield break;
        }

        foreach (string part in CellText.Wrap(text.TrimStart(), Math.Max(1, width - indent)))
        {
            yield return new string(' ', indent) + part;
        }
    }
}
