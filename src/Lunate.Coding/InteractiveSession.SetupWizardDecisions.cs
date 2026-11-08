using Lunate.Ai;

namespace Lunate.Coding;

// The decisions step's menu (spec: The walk is navigable): one action list that returns to itself
// with the changed value visible, leaves only through Done, and takes the plural routing answer
// through the multi-select question. Esc returns to the step that opened it.

internal sealed partial class InteractiveSession
{
    /// <summary>
    /// Step 8: the decisions settings as a menu that returns to itself after an action, showing the
    /// changed value, with a Done row as the only exit. Esc returns to the previous step. Accepting
    /// every default still writes nothing for the layer (spec: The walk is navigable).
    /// </summary>
    private async Task<WizardMove> DecisionsStepAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        var draft = new DecisionDraft();
        string? revisit = "done";
        while (true)
        {
            List<string> labels = ["Done — write these choices and leave the menu"];
            List<string> values = ["done"];
            foreach (string row in DecisionRows)
            {
                labels.Add($"{DecisionRowLabel(row)} — {draft.Describe(row)}");
                values.Add(row);
            }

            string? picked = await PickAsync(
                    "Decisions settings",
                    labels,
                    values,
                    PickerKind.WizardChoice,
                    ct,
                    revisit,
                    menu: true
                )
                .ConfigureAwait(false);
            if (picked is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(picked) is { } verb)
            {
                return verb;
            }

            if (picked == "done")
            {
                wizard.RecordDecisions(draft.ToChoice());
                return WizardMove.Advance;
            }

            if (wizard.ChatProfile is null)
            {
                Notify(
                    "the decisions settings bind profiles, but no chat profile was configured; choose "
                        + "Done or add a profile first",
                    NoticeLevel.Warning
                );
                revisit = picked;
                continue;
            }

            WizardMove? action = await DecisionActionAsync(draft, picked, wizard, ct)
                .ConfigureAwait(false);
            if (action is { } moved)
            {
                return moved;
            }

            revisit = picked;
        }
    }

    /// <summary>The decisions menu's action rows, in render order.</summary>
    private static readonly string[] DecisionRows = ["approvals", "routing", "questions", "tool"];

    private static string DecisionRowLabel(string row) =>
        row switch
        {
            "approvals" => "approval gate (decisions.approvals)",
            "routing" => "routing (decisions.routing)",
            "questions" => "questions (decisions.questions)",
            _ => "decide tool (decisions.tool)",
        };

    /// <summary>Runs one menu action. Returns null to return to the menu, or the move that leaves
    /// it (a picker's Back/Forward, or an abort).</summary>
    private async Task<WizardMove?> DecisionActionAsync(
        DecisionDraft draft,
        string row,
        SetupWizardModel wizard,
        CancellationToken ct
    )
    {
        switch (row)
        {
            case "approvals":
            {
                string? approvals = await ModeSwitchAsync(
                        "The approval gate (decisions.approvals)",
                        "A decision model screens staged actions; shadow answers are display-only, "
                            + "enforce turns the recommendation into the approval.",
                        ct
                    )
                    .ConfigureAwait(false);
                if (approvals == "\0")
                {
                    return WizardMove.Abort;
                }

                draft.Approvals = approvals;
                return null;
            }

            case "routing":
            {
                string? routing = await ModeSwitchAsync(
                        "Routing (decisions.routing)",
                        "The decision model picks which configured profile answers; routing needs "
                            + "at least two profiles.",
                        ct
                    )
                    .ConfigureAwait(false);
                if (routing == "\0")
                {
                    return WizardMove.Abort;
                }

                if (routing is null)
                {
                    draft.Routing = null;
                    draft.RoutingProfiles = [];
                    draft.RoutingDescriptions.Clear();
                    return null;
                }

                IReadOnlyList<string> candidates = RoutingCandidates(wizard);
                if (candidates.Count < 2)
                {
                    Notify(
                        "routing needs at least two profiles to choose between; the routing key is "
                            + "not written this walk",
                        NoticeLevel.Warning
                    );
                    draft.Routing = null;
                    draft.RoutingProfiles = [];
                    return null;
                }

                // The plural answer: which profiles participate. Space toggles, Enter confirms and
                // the question shows the count while toggling.
                IReadOnlyList<string>? selected = await AskMultiSelectAsync(
                        "Routing profiles",
                        "Which profiles should routing choose between? Space toggles, Enter confirms.",
                        candidates,
                        ct
                    )
                    .ConfigureAwait(false);
                if (selected is null)
                {
                    return WizardMove.Abort;
                }

                if (selected.Count < 2)
                {
                    Notify(
                        $"routing needs at least two profiles to choose between; {selected.Count} "
                            + "selected — the routing key is not written this walk",
                        NoticeLevel.Warning
                    );
                    draft.Routing = null;
                    draft.RoutingProfiles = [];
                    return null;
                }

                draft.RoutingDescriptions.Clear();
                foreach (string profile in selected)
                {
                    string? description = await PromptAsync(
                            $"What is profile '{profile}' for, what is it not, an example (Enter leaves it blank)",
                            ct
                        )
                        .ConfigureAwait(false);
                    if (description is null)
                    {
                        return WizardMove.Abort;
                    }

                    if (description.Trim().Length == 0)
                    {
                        Notify(
                            $"profile '{profile}' has no description; routing warns once about a "
                                + "blank description at composition",
                            NoticeLevel.Warning
                        );
                    }
                    else
                    {
                        draft.RoutingDescriptions[profile] = description.Trim();
                    }
                }

                draft.Routing = routing;
                draft.RoutingProfiles = [.. selected];
                return null;
            }

            case "questions":
            {
                string? questions = await AskQuestionAsync(
                        "Questions (decisions.questions)",
                        "A probability marker appears beside the top option; the user still chooses.",
                        ["off", "suggest"],
                        ct
                    )
                    .ConfigureAwait(false);
                if (questions == WizardAbortValue)
                {
                    return WizardMove.Abort;
                }

                draft.Questions = questions == "off" ? null : questions;
                return null;
            }

            default:
            {
                string? tool = await ToolSwitchAsync(ct).ConfigureAwait(false);
                if (tool == "\0")
                {
                    return WizardMove.Abort;
                }

                draft.Tool = tool;
                return null;
            }
        }
    }

    /// <summary>The decisions menu's staged answers: every row keeps its value across actions, and
    /// leaving the menu with every default records nothing for the layer.</summary>
    private sealed class DecisionDraft
    {
        public string? Approvals { get; set; }

        public string? Routing { get; set; }

        public string? Questions { get; set; }

        public string? Tool { get; set; }

        public IReadOnlyList<string> RoutingProfiles { get; set; } = [];

        public Dictionary<string, string> RoutingDescriptions { get; } =
            new(StringComparer.Ordinal);

        public string Describe(string row) =>
            row switch
            {
                "approvals" => Approvals ?? "off (default)",
                "routing" => Routing is { } routing
                    ? $"{routing} — {string.Join(", ", RoutingProfiles)}"
                    : "unset (default)",
                "questions" => Questions ?? "off (default)",
                _ => Tool ?? "off (default)",
            };

        public WizardDecisionsChoice? ToChoice() =>
            Approvals is null && Routing is null && Questions is null && Tool is null
                ? null
                : new WizardDecisionsChoice(
                    Approvals,
                    Routing,
                    Tool,
                    RoutingProfiles,
                    RoutingDescriptions,
                    Questions
                );
    }

    private static IReadOnlyList<string> RoutingCandidates(SetupWizardModel wizard)
    {
        var names = new List<string>();
        if (wizard.ChatProfile is { } chat)
        {
            names.Add(chat.Name);
        }

        if (wizard.DecisionProfile is { } decision)
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

    /// <summary>One mode switch: the exact literals with the defaults; keeping the default writes
    /// nothing. The coach returns a literal, null for the unwritten default, or "\0" for Esc.</summary>
    private async Task<string?> ModeSwitchAsync(
        string title,
        string description,
        CancellationToken ct
    )
    {
        string? answered = await AskQuestionAsync(
                title,
                description,
                ["off", "shadow", "enforce"],
                ct
            )
            .ConfigureAwait(false);
        return answered == "off" ? null : answered;
    }

    private async Task<string?> ToolSwitchAsync(CancellationToken ct) =>
        await AskQuestionAsync(
                "decisions.tool",
                "Offers the decide tool to the model; only with decision profile resolving as well.",
                ["off", "on"],
                ct
            )
            .ConfigureAwait(false) switch
        {
            "off" => null,
            var answered => answered,
        };
}
