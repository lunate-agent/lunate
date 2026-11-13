namespace Lunate.Coding;

/// <summary>
/// The routing profile list one staged decisions answer resolves to. The commit writes it and the
/// pre-write manifest names it, so the review can never advertise a profile the commit will not
/// write (spec: The file manifest and the receipt).
/// </summary>
internal sealed partial class SetupWizardModel
{
    /// <summary>
    /// The profiles routing binds when it turns on: the multi-select's confirmed list, in the order
    /// it offered them, when the choice carries one. A choice staged without them falls back to the
    /// walk's candidates — the carried chat profile, this walk's other provider chat profiles, the
    /// decision profile, and the settings file's existing names. Empty means routing cannot turn on
    /// here.
    /// </summary>
    public IReadOnlyList<string> ConfirmedRoutingProfiles(WizardDecisionsChoice? choices)
    {
        if (choices is { RoutingProfiles.Count: > 0 })
        {
            return choices.RoutingProfiles;
        }

        List<string> names = [];
        if (ChatProfile is { } carried)
        {
            names.Add(carried.Name);
        }

        foreach (WizardProviderRun run in AllProviderRuns)
        {
            if (run.ChatProfile is { } chat && !names.Contains(chat.Name, StringComparer.Ordinal))
            {
                names.Add(chat.Name);
            }
        }

        if (DecisionProfile is { } decision)
        {
            names.Add(decision.ProfileName);
        }

        foreach (string existing in ExistingRoutingProfiles)
        {
            if (!names.Contains(existing, StringComparer.Ordinal))
            {
                names.Add(existing);
            }
        }

        return names;
    }
}
