using Lunate.Ai;

namespace Lunate.Coding;

// The fast and decision steps' model pickers (design §2, §3): one flat provider/model list across
// the provider configured in this run, every provider already configured on the machine and every
// provider added inline, plus the sub-walk that adds one without leaving the usage step.

internal sealed partial class InteractiveSession
{
    /// <summary>The inline sub-walk's outcome: staged, unusable (the usage step asks again), or the
    /// wizard's own abort (Esc).</summary>
    private enum InlineOutcome
    {
        Added,
        Unusable,
        Aborted,
    }

    /// <summary>The inline-provider row's picker value; a leading NUL can never collide with an id.</summary>
    private const string WizardInlineValue = "\0inline";

    /// <summary>
    /// Step 7a: the <c>fast</c> usage — a model from any configured provider, the chat profile's own
    /// model (which then also serves fast), or a provider set up inline. Skip is the default and
    /// invents no value.
    /// </summary>
    private async Task<WizardMove> FastStepAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        if (wizard.ChatProfile is null)
        {
            // No chat profile means no fallback for the usage: the step skips, as it did before.
            wizard.RecordFastUsage(null, skipped: true);
            return WizardMove.Advance;
        }

        while (true)
        {
            IReadOnlyList<WizardUsageOptions.Option> options = UsageOptions(
                wizard,
                decision: false
            );
            if (options.Count == 0)
            {
                wizard.RecordFastUsage(null, skipped: true);
                return WizardMove.Advance;
            }

            string? answered = await PickUsageAsync(
                    wizard,
                    options,
                    "fast usage (any configured provider; the chat model stays the default)",
                    ct
                )
                .ConfigureAwait(false);
            if (answered is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(answered) is { } verb)
            {
                return verb;
            }

            if (answered == WizardSkipValue)
            {
                wizard.RecordFastUsage(null, skipped: true);
                return WizardMove.Advance;
            }

            if (answered == WizardInlineValue)
            {
                InlineOutcome outcome = await SetupInlineProviderAsync(wizard, ct)
                    .ConfigureAwait(false);
                if (outcome == InlineOutcome.Aborted)
                {
                    return WizardMove.Abort;
                }

                continue;
            }

            WizardModelRef model = options[OptionIndex(options, answered)].Model;
            if (wizard.ChatProfile is { } chat && IsChatModel(wizard, chat, model))
            {
                // The same model the chat profile already runs: the chat profile also serves the
                // usage, so the chat model check is this usage's check.
                wizard.RecordFastUsage(ReuseUsage(chat, model));
                wizard.RecordFastCheck(
                    wizard.ModelCheck ?? new WizardCheckResult(WizardProgress.Skipped)
                );
                if (wizard.ModelCheck?.Outcome is not WizardProgress.Passed)
                {
                    Notify(
                        "fast usage: the chat model check did not pass, so the fast usage stays "
                            + "unverified",
                        NoticeLevel.Info
                    );
                }

                return WizardMove.Advance;
            }

            WizardMove move = await FastCheckAsync(wizard, model, ct).ConfigureAwait(false);
            if (move == WizardMove.Stay)
            {
                continue;
            }

            return move;
        }
    }

    /// <summary>Step 7a's check: one short completion on the selected model, its cost stated first
    /// and the check skippable; a skip or a failure leaves the fast profile unverified.</summary>
    private async Task<WizardMove> FastCheckAsync(
        SetupWizardModel wizard,
        WizardModelRef model,
        CancellationToken ct
    )
    {
        string detail = Detail(model, kind: null);
        string cost = EstimatedCost(WizardModelCheck.ModelFor(model, _catalog));
        string? answered = await PickAsync(
                $"Fast usage check: estimated cost {cost}; one short request to {detail} — skippable",
                ["Send the fast usage check", "Skip the fast usage check"],
                ["send", "skip"],
                PickerKind.WizardChoice,
                ct,
                "skip"
            )
            .ConfigureAwait(false);
        if (answered is null)
        {
            return WizardMove.Abort;
        }

        if (VerbOf(answered) is { } verb)
        {
            return verb;
        }

        // A picked model carries no thinking level: the step never asks for one, so the profile
        // stores the model and its family only.
        var usage = new WizardUsageChoice(
            FastProfileName(wizard),
            model.Provider.Id,
            model.Model.ModelId,
            model.Model.ApiFamily
        );
        if (answered == "skip")
        {
            wizard.RecordFastUsage(usage);
            wizard.RecordFastCheck(new WizardCheckResult(WizardProgress.Skipped));
            return WizardMove.Advance;
        }

        WizardCheckResult check = await WizardModelCheck
            .RunForAsync(
                model,
                _catalog,
                _factory,
                _auth,
                _environment,
                model.Credential?.Key,
                SessionRequestContext(),
                ct
            )
            .ConfigureAwait(false);
        if (check.Outcome == WizardProgress.Passed)
        {
            Notify($"fast check passed: {detail}");
            wizard.RecordFastUsage(usage);
            wizard.RecordFastCheck(check);
            return WizardMove.Advance;
        }

        Notify(
            $"the fast check failed for {detail}: {check.Detail ?? "the request was refused"}. "
                + "You can choose the fast usage again or keep the profile unverified",
            NoticeLevel.Warning
        );
        string? after = await PickAsync(
                "After the failed fast check",
                ["Keep it (the fast profile stays unverified)", "Choose the fast usage again"],
                ["keep", "again"],
                PickerKind.WizardChoice,
                ct,
                "keep"
            )
            .ConfigureAwait(false);
        if (after is null)
        {
            return WizardMove.Abort;
        }

        if (VerbOf(after) is { } afterVerb)
        {
            return afterVerb;
        }

        if (after == "again")
        {
            return WizardMove.Stay;
        }

        wizard.RecordFastUsage(usage);
        wizard.RecordFastCheck(check);
        return WizardMove.Advance;
    }

    /// <summary>
    /// Step 7b: a decision profile — a native Jev model from any provider that exposes the family,
    /// or an adapter model from any configured provider; the binding (<c>decisions.profile</c>)
    /// enables no consumer.
    /// </summary>
    private async Task<WizardMove> DecisionStepAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        if (wizard.Provider is null || wizard.Model is null)
        {
            // A walk that skipped its provider has no usages to bind: skip is the only answer,
            // as it was before the usages could draw from any provider.
            wizard.RecordDecisionProfile(null);
            return WizardMove.Advance;
        }

        while (true)
        {
            IReadOnlyList<WizardUsageOptions.Option> options = UsageOptions(wizard, decision: true);
            if (options.Count == 0)
            {
                wizard.RecordDecisionProfile(null);
                return WizardMove.Advance;
            }

            bool native = options.Any(option => option.Model.Native);
            string? answered = await PickUsageAsync(
                    wizard,
                    options,
                    native
                        ? "Decision profile (the provider exposes the native Jev decision profile)"
                        : "Decision profile (this provider serves no native Jev profile; the adapter label applies)",
                    ct
                )
                .ConfigureAwait(false);
            if (answered is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(answered) is { } verb)
            {
                return verb;
            }

            if (answered == WizardSkipValue)
            {
                wizard.RecordDecisionProfile(null);
                return WizardMove.Advance;
            }

            if (answered == WizardInlineValue)
            {
                InlineOutcome outcome = await SetupInlineProviderAsync(wizard, ct)
                    .ConfigureAwait(false);
                if (outcome == InlineOutcome.Aborted)
                {
                    return WizardMove.Abort;
                }

                continue;
            }

            WizardModelRef model = options[OptionIndex(options, answered)].Model;
            var usage = new WizardUsageChoice(
                "decisions",
                model.Provider.Id,
                model.Model.ModelId,
                model.Model.ApiFamily,
                Native: model.Native,
                PinnedVersion: model.PinnedVersion
            );
            wizard.RecordDecisionProfile(usage);
            WizardMove move = await DecisionCheckAsync(wizard, model, ct).ConfigureAwait(false);
            if (move == WizardMove.Stay)
            {
                wizard.RecordDecisionProfile(null);
                continue;
            }

            return move;
        }
    }

    /// <summary>
    /// Step 7b's check: one tiny noul through the decision client the profile will use — the
    /// native System One transport for a native profile, the adapter's chat client otherwise. The
    /// cost is stated before it runs and the check is skippable; a skip or a failure leaves the
    /// decision profile unverified in the summary (spec: The full walk, step 5).
    /// </summary>
    private async Task<WizardMove> DecisionCheckAsync(
        SetupWizardModel wizard,
        WizardModelRef model,
        CancellationToken ct
    )
    {
        string kind = model.Native ? "native" : "adapter";
        string detail = Detail(model, kind);
        string cost = EstimatedCost(WizardModelCheck.DecisionModelFor(model, _catalog));
        string? answered = await PickAsync(
                $"Decision check: estimated cost {cost}; one tiny noul to {detail} — skippable",
                ["Send the decision check", "Skip the decision check"],
                ["send", "skip"],
                PickerKind.WizardChoice,
                ct,
                "skip"
            )
            .ConfigureAwait(false);
        if (answered is null)
        {
            return WizardMove.Abort;
        }

        if (VerbOf(answered) is { } verb)
        {
            return verb;
        }

        if (answered == "skip")
        {
            wizard.RecordDecisionCheck(new WizardCheckResult(WizardProgress.Skipped));
            return WizardMove.Advance;
        }

        WizardCheckResult check = await WizardModelCheck
            .RunDecisionForAsync(
                model,
                _catalog,
                _factory,
                _auth,
                _environment,
                model.Credential?.Key,
                SessionRequestContext(),
                ct
            )
            .ConfigureAwait(false);
        wizard.RecordDecisionCheck(check);
        if (check.Outcome == WizardProgress.Passed)
        {
            Notify($"decision check passed: {detail}");
            return WizardMove.Advance;
        }

        Notify(
            $"the decision check failed for {detail}: "
                + $"{check.Detail ?? "the request was refused"}. You can choose the decision "
                + "profile again or keep it unverified",
            NoticeLevel.Warning
        );
        string? after = await PickAsync(
                "After the failed decision check",
                [
                    "Keep it (the decision profile stays unverified)",
                    "Choose the decision profile again",
                ],
                ["keep", "again"],
                PickerKind.WizardChoice,
                ct,
                "keep"
            )
            .ConfigureAwait(false);
        if (after is null)
        {
            return WizardMove.Abort;
        }

        if (VerbOf(after) is { } afterVerb)
        {
            return afterVerb;
        }

        return after == "again" ? WizardMove.Stay : WizardMove.Advance;
    }

    /// <summary>
    /// The inline sub-walk a usage step offers: the provider, credential, connection-test and model
    /// steps on a throwaway model, so the outer answer and every other answer survive. The session's
    /// discovery listing is saved and restored around it; the added provider is staged on the outer
    /// model for the same review and the same commit.
    /// </summary>
    private async Task<InlineOutcome> SetupInlineProviderAsync(
        SetupWizardModel outer,
        CancellationToken ct
    )
    {
        IReadOnlyList<WizardModelListing> savedListing = _listing;
        try
        {
            var inline = new SetupWizardModel();
            while (inline.Step is not SetupStep.Model)
            {
                WizardMove move = inline.Step switch
                {
                    SetupStep.Provider => await ProviderStepAsync(inline, ct).ConfigureAwait(false),
                    SetupStep.Credential => await CredentialStepAsync(inline, ct)
                        .ConfigureAwait(false),
                    _ => await ConnectionTestStepAsync(inline, ct).ConfigureAwait(false),
                };
                if (move == WizardMove.Abort)
                {
                    return InlineOutcome.Aborted;
                }

                if (move == WizardMove.Stay)
                {
                    continue;
                }

                if (move == WizardMove.Back)
                {
                    inline.Enter(BackWithinEntry(inline, inline.Step));
                    continue;
                }

                if (inline.Provider is null)
                {
                    Notify(
                        "the inline provider was skipped; pick an existing provider or set one up",
                        NoticeLevel.Warning
                    );
                    return InlineOutcome.Unusable;
                }

                inline.Enter(inline.Next(inline.Step));
            }

            WizardMove modelMove = await ModelStepAsync(inline, ct).ConfigureAwait(false);
            if (modelMove == WizardMove.Abort)
            {
                return InlineOutcome.Aborted;
            }

            if (inline.Provider is not { } provider || inline.Model is not { } model)
            {
                Notify(
                    "the inline provider has no model yet; a usage needs one — set it up again or "
                        + "pick an existing model",
                    NoticeLevel.Warning
                );
                return InlineOutcome.Unusable;
            }

            outer.RecordInlineProvider(
                new WizardInlineProvider(
                    provider,
                    inline.Credential ?? new WizardCredentialChoice(provider.Id),
                    inline.ConnectionTest ?? new WizardCheckResult(WizardProgress.Skipped),
                    model,
                    _listing
                )
            );
            Notify(
                $"provider '{provider.Id}' added to this setup run; pick its model for the usage",
                NoticeLevel.Info
            );
            return InlineOutcome.Added;
        }
        finally
        {
            _listing = savedListing;
        }
    }

    /// <summary>One usage picker: the flat option rows, the inline-provider row and Skip (the
    /// default, which invents no value).</summary>
    private async Task<string?> PickUsageAsync(
        SetupWizardModel wizard,
        IReadOnlyList<WizardUsageOptions.Option> options,
        string title,
        CancellationToken ct
    )
    {
        (List<string> labels, List<string> values) = WizardUsageOptions.Rows(options);
        labels.Add("Set up another provider inline…");
        values.Add(WizardInlineValue);
        labels.Add("Skip");
        values.Add(WizardSkipValue);
        return await PickAsync(title, labels, values, PickerKind.WizardModel, ct, WizardSkipValue)
            .ConfigureAwait(false);
    }

    private IReadOnlyList<WizardUsageOptions.Option> UsageOptions(
        SetupWizardModel wizard,
        bool decision
    ) => WizardUsageOptions.Build(wizard, _providerEntries, _listing, _catalog, decision);

    private static int OptionIndex(IReadOnlyList<WizardUsageOptions.Option> options, string value)
    {
        for (var index = 0; index < options.Count; index++)
        {
            if (string.Equals(options[index].Value, value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private static WizardUsageChoice ReuseUsage(WizardProfileChoice chat, WizardModelRef model) =>
        new(
            chat.Name,
            model.Provider.Id,
            chat.ModelId,
            model.Model.ApiFamily,
            chat.ReasoningLevel,
            ReuseChatProfile: true
        );

    private static string Detail(WizardModelRef model, string? kind) =>
        WizardUsageOptions.DisplayId(model.Provider.Id, model.Model.ModelId)
        + (kind is null ? $" ({model.Model.ApiFamily})" : $" ({kind})");

    /// <summary>The fast profile's name: <c>fast</c>, unless the chat profile already answers to
    /// that name — then the provider's name keeps the two profiles apart.</summary>
    private static string FastProfileName(SetupWizardModel wizard) =>
        string.Equals(wizard.ChatProfile?.Name, "fast", StringComparison.Ordinal)
            ? $"fast-{wizard.Provider?.Id}"
            : "fast";

    private static bool IsChatModel(
        SetupWizardModel wizard,
        WizardProfileChoice chat,
        WizardModelRef model
    ) =>
        string.Equals(chat.ModelId, model.Model.ModelId, StringComparison.Ordinal)
        && string.Equals(wizard.Provider?.Id, model.Provider.Id, StringComparison.Ordinal);

    private static string EstimatedCost(ModelInfo model) =>
        CostCalculator.Compute(model, inputTokens: 8, outputTokens: 1) is decimal estimate
            ? CostCalculator.Format(estimate)
            : "not available (no published price)";
}
