using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The walk's depth (spec: Quick and detailed modes): Quick is the default fast path — provider,
/// credential, checks, model and chat profile — and leaves the detailed-only steps at their
/// defaults; Detailed asks every setting. Both walk the same ten-step list.
/// </summary>
internal enum SetupWizardMode
{
    Quick,
    Detailed,
}

/// <summary>
/// One setting a Quick walk left at its default: the setting's name, the value it kept and the
/// command that changes it later. The review renders one line per record (spec: Quick and detailed
/// modes — Quick must not hide a default).
/// </summary>
internal sealed record WizardDefault(string Setting, string Value, string ChangeWith);

// The mode half of the wizard's state machine: which step comes next, what the quick path leaves
// at its defaults, and the fact that a mode switch never restarts the walk.

internal sealed partial class SetupWizardModel
{
    /// <summary>The walk's depth; Quick is the first-run default (spec: Quick and detailed modes).</summary>
    public SetupWizardMode Mode { get; private set; } = SetupWizardMode.Quick;

    /// <summary>
    /// Whether this walk is the full walk in Quick mode. Scoped entries keep their detailed step
    /// range: the mode choice belongs to the first-run (and "walk everything") walk.
    /// </summary>
    public bool IsQuick => Mode is SetupWizardMode.Quick && Entry is WizardEntry.WalkEverything;

    private static readonly IReadOnlyList<SetupStep> DetailedOnlySteps =
    [
        SetupStep.FastProfile,
        SetupStep.DecisionProfile,
        SetupStep.Decisions,
    ];

    /// <summary>
    /// Switches the walk's depth without touching a collected answer: the next step selection
    /// follows the new mode, so an answer already given in Detailed stays staged when the walk
    /// continues in Quick (spec: Switching mode keeps answers).
    /// </summary>
    public void ToggleMode() =>
        Mode = Mode is SetupWizardMode.Quick ? SetupWizardMode.Detailed : SetupWizardMode.Quick;

    /// <summary>Names the mode explicitly; used by tests and the first-step switch.</summary>
    public void RecordMode(SetupWizardMode mode) => Mode = mode;

    /// <summary>Whether the current mode leaves this step at its default instead of asking it.</summary>
    public bool IsLeftAtDefault(SetupStep step) =>
        IsQuick && DetailedOnlySteps.Contains(step) && !HasAnswer(step);

    private bool HasAnswer(SetupStep step) =>
        step switch
        {
            SetupStep.FastProfile => FastProfile,
            SetupStep.DecisionProfile => DecisionProfile is not null,
            SetupStep.Decisions => Decisions is not null,
            _ => true,
        };

    /// <summary>
    /// The next step after <paramref name="step"/> for the current mode: Quick skips the
    /// detailed-only steps it has not answered; Detailed walks the entire list.
    /// </summary>
    public SetupStep Next(SetupStep step)
    {
        int index = -1;
        for (var current = 0; current < Order.Count; current++)
        {
            if (Order[current] == step)
            {
                index = current;
                break;
            }
        }

        for (int next = index + 1; next < Order.Count; next++)
        {
            SetupStep candidate = Order[next];
            if (candidate is SetupStep.Summary || !IsLeftAtDefault(candidate))
            {
                return candidate;
            }
        }

        return SetupStep.Summary;
    }

    /// <summary>
    /// Every setting Quick did not ask, with the default it left and the command that changes it
    /// later. Empty for a Detailed walk (spec: Quick and detailed modes).
    /// </summary>
    public IReadOnlyList<WizardDefault> LeftAtDefault
    {
        get
        {
            if (!IsQuick)
            {
                return [];
            }

            List<WizardDefault> defaults = [];
            if (Provider is { Custom: true } provider)
            {
                defaults.Add(new WizardDefault("custom API family", provider.ApiFamily, "/setup"));
                defaults.Add(
                    new WizardDefault(
                        "credential placement",
                        provider.Placement.ToCompact(),
                        "/setup"
                    )
                );
                defaults.Add(
                    new WizardDefault("custom region", provider.Region ?? "unset", "/setup")
                );
                defaults.Add(
                    new WizardDefault(
                        "custom models URL",
                        provider.ModelsUrl?.ToString() ?? "unset",
                        "/setup"
                    )
                );
            }

            if (ChatProfile?.ReasoningLevel is null)
            {
                defaults.Add(new WizardDefault("thinking level", "unset", "/settings"));
            }

            if (!FastProfile)
            {
                defaults.Add(new WizardDefault("fast usage", "not bound", "/setup"));
            }

            if (DecisionProfile is null)
            {
                defaults.Add(new WizardDefault("decision profile", "not configured", "/setup"));
            }

            if (Decisions?.Approvals is null)
            {
                defaults.Add(new WizardDefault("decisions.approvals", "off", "/settings"));
            }

            if (Decisions?.Routing is null)
            {
                defaults.Add(new WizardDefault("decisions.routing", "unset", "/settings"));
                defaults.Add(new WizardDefault("routing descriptions", "none", "/settings"));
            }

            if (Decisions?.Questions is null)
            {
                defaults.Add(new WizardDefault("decisions.questions", "off", "/settings"));
            }

            if (Decisions?.Tool is null)
            {
                defaults.Add(new WizardDefault("decisions.tool", "off", "/settings"));
            }

            return defaults;
        }
    }
}
