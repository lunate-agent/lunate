using Lunate.Ai;

namespace Lunate.Coding;

internal static partial class SetupWizardManifest
{
    internal static IReadOnlyList<SetupUnchanged> Unchanged(SetupWizardModel wizard)
    {
        ArgumentNullException.ThrowIfNull(wizard);
        List<SetupUnchanged> unchanged = [];
        if (!StagesProvider(wizard))
        {
            unchanged.Add(
                new("provider", State(wizard, SetupStep.Provider) + ChangeHint("providers.json"))
            );
        }

        if (!StagesCredential(wizard))
        {
            unchanged.Add(
                new(
                    "credential",
                    State(wizard, SetupStep.Credential)
                        + " · lunate auth login · ~/.lunate/auth.json"
                )
            );
        }

        if (wizard.Model is null || wizard.IsCarried(SetupStep.Model))
        {
            unchanged.Add(
                new("model", State(wizard, SetupStep.Model) + " · /setup · ~/.lunate/settings.json")
            );
        }

        if (wizard.ConnectionTest is null || wizard.IsSkipped(SetupStep.ConnectionTest))
        {
            unchanged.Add(
                new(
                    "connection test",
                    State(wizard, SetupStep.ConnectionTest) + " · /setup · no file written"
                )
            );
        }

        if (wizard.ModelCheck is null || wizard.IsSkipped(SetupStep.ModelCheck))
        {
            unchanged.Add(
                new(
                    "model check",
                    State(wizard, SetupStep.ModelCheck) + " · /setup · no file written"
                )
            );
        }

        if (
            wizard.DecisionProfile is not null
            && (wizard.DecisionCheck is null || wizard.IsSkipped(SetupStep.DecisionProfile))
        )
        {
            unchanged.Add(
                new(
                    "decision check",
                    State(wizard, SetupStep.DecisionProfile) + " · /setup · no file written"
                )
            );
        }

        if (!StagesChat(wizard))
        {
            unchanged.Add(
                new(
                    "chat profile",
                    State(wizard, SetupStep.ChatProfile) + ChangeHint("settings.json")
                )
            );
        }

        if (!StagesFast(wizard))
        {
            unchanged.Add(
                new(
                    "fast usage",
                    State(wizard, SetupStep.FastProfile) + ChangeHint("settings.json")
                )
            );
        }

        if (!StagesDecisionProfile(wizard))
        {
            unchanged.Add(
                new(
                    "decision profile",
                    State(wizard, SetupStep.DecisionProfile) + ChangeHint("settings.json")
                )
            );
        }

        foreach (string setting in DefaultDecisionSettings(wizard))
        {
            unchanged.Add(new(setting, "default (unset) · /settings · ~/.lunate/settings.json"));
        }

        // Quick's detailed-only settings are left at their defaults (spec: Quick and detailed
        // modes): the review names each one and the command that changes it later, unless a
        // derived entry above already names it.
        foreach (WizardDefault left in wizard.LeftAtDefault)
        {
            if (
                unchanged.Any(entry =>
                    string.Equals(entry.Label, left.Setting, StringComparison.Ordinal)
                )
            )
            {
                continue;
            }

            unchanged.Add(new(left.Setting, left.Value + " · " + left.ChangeWith));
        }

        return unchanged;
    }

    private static IReadOnlyList<string> DefaultDecisionSettings(SetupWizardModel wizard)
    {
        List<string> settings = [];
        if (wizard.Decisions?.Approvals is null)
        {
            settings.Add("decisions.approvals");
        }

        if (wizard.Decisions?.Routing is null)
        {
            settings.Add("decisions.routing");
            settings.Add("routing descriptions");
        }

        if (wizard.Decisions?.Tool is null)
        {
            settings.Add("decisions.tool");
        }

        if (wizard.Decisions?.Questions is null)
        {
            settings.Add("decisions.questions");
        }

        return settings;
    }

    private static bool StagesProvider(SetupWizardModel wizard) =>
        wizard.Provider is not null && !wizard.IsCarried(SetupStep.Provider);

    private static bool StagesCredential(SetupWizardModel wizard) =>
        wizard.Credential is not null && !wizard.IsCarried(SetupStep.Credential);

    private static bool StagesChat(SetupWizardModel wizard) =>
        (wizard.ChatProfile is not null && !wizard.IsCarried(SetupStep.ChatProfile))
        || wizard.ChatUsageBinding is not null;

    private static bool StagesFast(SetupWizardModel wizard) =>
        wizard.FastProfile
        && wizard.ChatProfile is not null
        && !wizard.IsCarried(SetupStep.ChatProfile);

    private static bool StagesDecisionProfile(SetupWizardModel wizard) =>
        wizard.DecisionProfile is not null && !wizard.IsCarried(SetupStep.DecisionProfile);

    private static string State(SetupWizardModel wizard, SetupStep step) =>
        wizard.IsCarried(step) ? "unchanged"
        : wizard.IsSkipped(step) ? "skipped"
        : "not set";

    private static string ChangeHint(string fileName) => " · /setup · ~/.lunate/" + fileName;
}
