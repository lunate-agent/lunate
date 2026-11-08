using Lunate.Ai;
using Lunate.Tui;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace Lunate.Coding;

// The setup wizard's driver (design §2): the walk runs inside the session's live area on the same
// interaction stack the auth picker uses — pickers for choices, the masked prompt for text and
// credentials, the question components for the decisions switches — as one off-pump task. Esc at
// any prompt ends the wizard and leaves every file untouched; a check failure offers going back to
// the earlier step, whose answers the model retains. No step writes a file.

internal sealed partial class InteractiveSession
{
    /// <summary>The walk's staged state, non-null exactly while the wizard is open.</summary>
    private SetupWizardModel? _wizard;

    /// <summary>The picked value of an open wizard picker; null means the picker dismissed it.</summary>
    private TaskCompletionSource<string?>? _wizardPick;

    /// <summary>The discovery listing the connection test collected; empty when it skipped or failed.</summary>
    private IReadOnlyList<WizardModelListing> _listing = [];

    private enum WizardMove
    {
        /// <summary>The step answered; walk on to the next.</summary>
        Advance,

        /// <summary>The step asked to go back to the previous one.</summary>
        Back,

        /// <summary>The step refused its answer and asks itself again; the walk holds.</summary>
        Stay,

        /// <summary>Forward replays the steps already answered and stops at the first that needs an
        /// answer, or at the summary.</summary>
        Forward,

        /// <summary>The walk ends with nothing written.</summary>
        Abort,
    }

    /// <summary>The custom provider row's picker value; a leading NUL can never collide with an id.</summary>
    internal const string WizardCustomValue = "\0custom";

    /// <summary>The "type a key" row's picker value in the credential step.</summary>
    internal const string WizardTypedValue = "\0typed";

    /// <summary>The "name a reference" row's picker value in the credential step.</summary>
    internal const string WizardReferenceValue = "\0reference";

    /// <summary>The explicit skip row shared by wizard steps that collect an optional answer.</summary>
    internal const string WizardSkipValue = "\0skip";

    /// <summary>The custom row's label, exactly the auth picker's wording.</summary>
    internal const string WizardCustomLabel = "Custom OpenAI-compatible endpoint…";

    /// <summary>The picker/wask-abort sentinel: an Esc inside a switch question, distinct from
    /// "keep the default".</summary>
    internal const string WizardAbortValue = "\0abort";

    /// <summary>The picker's back verb (<c>b</c> or Left): the walk returns to the previous step
    /// with every answer kept.</summary>
    internal const string WizardBackValue = "\0back";

    /// <summary>The picker's forward verb (<c>f</c> or Right): replay the steps already answered and
    /// stop at the first that still needs one.</summary>
    internal const string WizardForwardValue = "\0forward";

    /// <summary>
    /// Opens the walk: a fresh machine's first run, <c>lunate setup</c> on a terminal, or
    /// <c>/setup</c>'s "walk everything". One wizard at a time; nothing is written before the
    /// summary confirms.
    /// </summary>
    internal void OpenSetupWizard()
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

        var wizard = new SetupWizardModel
        {
            // The decisions step's routing ask draws from the profiles the settings file already
            // configures plus the walk's own new ones; the binding lands at the commit.
            ExistingRoutingProfiles = [.. _settings.Decisions.RoutingProfiles],
        };
        _wizard = wizard;
        _turnActive = true;
        LoginTask = _ = RunWizardAsync(wizard, _lifetime.Token);
    }

    private async Task RunWizardAsync(SetupWizardModel wizard, CancellationToken ct) =>
        await RunWizardTaskAsync(
                wizard,
                async token =>
                {
                    Notify(
                        "setup: configure a provider, a model and a profile — Esc at any prompt ends the "
                            + "wizard, and nothing is written before the summary confirms"
                    );
                    await WalkAsync(wizard, token).ConfigureAwait(false);
                },
                ct
            )
            .ConfigureAwait(false);

    /// <summary>The run's shared envelope: the notification paths the walk body throws into, and
    /// the one cleanup every entry passes through.</summary>
    private async Task RunWizardTaskAsync(
        SetupWizardModel wizard,
        Func<CancellationToken, Task> body,
        CancellationToken ct
    )
    {
        try
        {
            await body(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The session is quitting: nobody is reading a cancelled wizard's report.
        }
        catch (Exception exception)
        {
            Notify($"setup failed: {exception.Message}", NoticeLevel.Error);
        }
        finally
        {
            _wizard = null;
            _wizardPick = null;
            _turnActive = false;
            LoginTask = null;
        }
    }

    private async Task WalkAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        wizard.Enter(wizard.FirstWalked);
        SetupStep? headed = null;
        while (true)
        {
            while (wizard.Step is not SetupStep.Summary)
            {
                // The path header names the stage and the position before the step asks (spec: The
                // walk is navigable). It is one line and fits the 60-column pin, and it is only
                // re-emitted when the step changes so a step's own notice survives a Stay.
                if (headed != wizard.Step)
                {
                    Notify(wizard.HeaderLine);
                    headed = wizard.Step;
                }

                WizardMove move = await RunStepAsync(wizard, ct).ConfigureAwait(false);

                if (move == WizardMove.Abort)
                {
                    Notify("setup cancelled — nothing was changed", NoticeLevel.Info);
                    return;
                }

                if (move == WizardMove.Stay)
                {
                    continue;
                }

                Navigate(wizard, move);
            }

            RenderSummary(wizard);
            WizardMove confirm = await ConfirmAsync(
                    "Write this configuration? (Enter writes; Esc cancels)",
                    ct
                )
                .ConfigureAwait(false);
            if (confirm == WizardMove.Back)
            {
                // Back at the summary re-opens the walk at its last step; every answer is kept and
                // Forward comes straight back here.
                wizard.Enter(wizard.LastWalked);
                headed = null;
                Notify($"setup — back to {wizard.LastWalked}; every answer is kept");
                continue;
            }

            if (confirm != WizardMove.Advance)
            {
                Notify("setup cancelled — nothing was written", NoticeLevel.Info);
                return;
            }

            break;
        }

        CommitWizard(wizard);
        await ReconcileAsync(wizard, ct).ConfigureAwait(false);
    }

    /// <summary>Runs the current step and returns the move it asked for.</summary>
    private Task<WizardMove> RunStepAsync(SetupWizardModel wizard, CancellationToken ct) =>
        wizard.Step switch
        {
            SetupStep.Provider => ProviderStepAsync(wizard, ct),
            SetupStep.Credential => CredentialStepAsync(wizard, ct),
            SetupStep.ConnectionTest => ConnectionTestStepAsync(wizard, ct),
            SetupStep.Model => ModelStepAsync(wizard, ct),
            SetupStep.ModelCheck => ModelCheckStepAsync(wizard, ct),
            SetupStep.ChatProfile => wizard.Entry is WizardEntry.ChangeChatProfile
                ? ChatUsageStepAsync(wizard, ct)
                : ChatProfileStepAsync(wizard, ct),
            SetupStep.FastProfile => FastStepAsync(wizard, ct),
            SetupStep.DecisionProfile => DecisionStepAsync(wizard, ct),
            SetupStep.Decisions => DecisionsStepAsync(wizard, ct),
            _ => Task.FromResult(WizardMove.Advance),
        };

    /// <summary>Applies a move to the walk's position: Back one step, Forward over the answered
    /// steps, Advance one step. Stay and Abort are handled by the loop.</summary>
    private void Navigate(SetupWizardModel wizard, WizardMove move)
    {
        if (move == WizardMove.Back)
        {
            wizard.Enter(wizard.PreviousStep());
            return;
        }

        if (move == WizardMove.Forward)
        {
            SetupStep target = wizard.FirstUnansweredAfter(wizard.Step);
            foreach (SetupStep kept in wizard.ReplayedSteps(wizard.Step, target))
            {
                Notify($"setup — keeping {kept.ToString().ToLowerInvariant()}; b goes back to it");
            }

            wizard.Enter(target);
            return;
        }

        wizard.Enter(wizard.NextStep());
    }

    /// <summary>Step 1: the preset list in providers.json order, then the custom row; a custom
    /// provider answers id, endpoint, family, placement, region and optional models URL.</summary>
    /// <summary>
    /// The picker's navigation verb, or null for an ordinary answer. Null (an aborted pick) is the
    /// walk's abort and is checked by the caller first (spec: The walk is navigable).
    /// </summary>
    private static WizardMove? VerbOf(string? picked) =>
        picked switch
        {
            WizardBackValue => WizardMove.Back,
            WizardForwardValue => WizardMove.Forward,
            _ => null,
        };

    private async Task<WizardMove> ProviderStepAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        List<string> labels = [];
        List<string> values = [];
        foreach (ProviderPreset preset in ProviderPresets.All)
        {
            labels.Add($"{preset.Id} — {preset.Name} (region not specified)");
            values.Add(preset.Id);
        }

        labels.Add(WizardCustomLabel);
        values.Add(WizardCustomValue);
        labels.Add("Skip provider setup");
        values.Add(WizardSkipValue);

        string? picked = await PickAsync(
                "Select provider",
                labels,
                values,
                PickerKind.WizardProvider,
                ct
            )
            .ConfigureAwait(false);
        {
            if (picked is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(picked) is { } verb)
            {
                return verb;
            }
        }

        if (picked == WizardSkipValue)
        {
            wizard.Skip(SetupStep.Provider);
            return WizardMove.Advance;
        }

        if (picked != WizardCustomValue)
        {
            if (ProviderPresets.Find(picked) is not { } preset)
            {
                Notify(
                    $"provider '{picked}' is unknown; the list names the presets — pick one",
                    NoticeLevel.Warning
                );
                return WizardMove.Stay;
            }

            wizard.RecordProvider(
                new WizardProviderChoice(
                    preset.Id,
                    Custom: false,
                    Region: null,
                    Endpoint: null,
                    ProviderApiFamilies.Name(preset.DefaultFamily),
                    preset.PlacementFor(preset.DefaultFamily),
                    preset.ModelsUrl
                )
            );
            return WizardMove.Advance;
        }

        WizardProviderChoice? custom = await AskCustomProviderAsync(wizard, ct)
            .ConfigureAwait(false);
        if (custom is null)
        {
            return WizardMove.Abort;
        }

        wizard.RecordProvider(custom);
        return WizardMove.Advance;
    }

    /// <summary>The custom path's collection: id, endpoint, family (default openai-chat),
    /// placement (default bearer; x-api-key answers an Anthropic family), region, models URL.
    /// An unknown family or an unusable URL is refused by name before anything later runs.</summary>
    private async Task<WizardProviderChoice?> AskCustomProviderAsync(
        SetupWizardModel wizard,
        CancellationToken ct
    )
    {
        while (true)
        {
            string? id = await PromptAsync(
                    "Custom provider id (no spaces or '/'; must not shadow a preset)",
                    ct
                )
                .ConfigureAwait(false);
            if (id is null)
            {
                return null;
            }

            id = id.Trim();
            if (
                id.Length == 0
                || id.Any(char.IsWhiteSpace)
                || id.Contains('/')
                || ProviderPresets.Find(id) is not null
            )
            {
                Notify(
                    $"the id '{id}' is refused: it must be non-empty, without spaces or '/', and "
                        + "not name a built-in preset — the custom path asks again",
                    NoticeLevel.Warning
                );
                continue;
            }

            WizardProviderChoice? endpointChoice = await AskCustomEndpointAsync(wizard, id, ct)
                .ConfigureAwait(false);
            if (endpointChoice is null)
            {
                return null;
            }

            return endpointChoice;
        }
    }

    private async Task<WizardProviderChoice?> AskCustomEndpointAsync(
        SetupWizardModel wizard,
        string id,
        CancellationToken ct
    )
    {
        while (true)
        {
            string? endpointText = await PromptAsync("Custom endpoint URL", ct)
                .ConfigureAwait(false);
            if (endpointText is null)
            {
                return null;
            }

            if (
                !CustomProvider.TryParseEndpoint(
                    endpointText,
                    out Uri? endpoint,
                    out string? endpointError
                )
            )
            {
                Notify(
                    $"the endpoint was refused: {endpointError} — the custom path asks again",
                    NoticeLevel.Warning
                );
                continue;
            }

            string? family = await PickAsync(
                    "Custom API family (Enter picks openai-chat)",
                    [.. ProviderApiFamilies.All.Select(ProviderApiFamilies.Name)],
                    [.. ProviderApiFamilies.All.Select(ProviderApiFamilies.Name)],
                    PickerKind.WizardChoice,
                    ct,
                    "openai-chat"
                )
                .ConfigureAwait(false);
            if (family is null)
            {
                return null;
            }

            if (!ProviderApiFamilies.TryParse(family, out ProviderApiFamily parsedFamily))
            {
                Notify(
                    $"API family '{family}' is not a supported family; supported families: {ProviderApiFamilies.Supported()} — the custom path asks again",
                    NoticeLevel.Warning
                );
                continue;
            }

            string? placement = await PickAsync(
                    "Credential placement (Enter picks the default)",
                    ["bearer", "x-api-key"],
                    ["bearer", "x-api-key"],
                    PickerKind.WizardChoice,
                    ct,
                    parsedFamily == ProviderApiFamily.AnthropicMessages ? "x-api-key" : "bearer"
                )
                .ConfigureAwait(false);
            if (placement is null)
            {
                return null;
            }

            if (
                !CredentialPlacement.TryParseCompact(
                    placement,
                    out CredentialPlacement? parsedPlacement,
                    out _
                )
            )
            {
                Notify(
                    $"placement '{placement}' is not a placement: declare 'bearer' or 'x-api-key' — "
                        + "the custom path asks again",
                    NoticeLevel.Warning
                );
                continue;
            }

            string? region = await PromptAsync(
                    "Region (eu, us or local; Enter leaves it unset)",
                    ct
                )
                .ConfigureAwait(false);
            if (region is null)
            {
                return null;
            }

            string? declaredRegion = region.Trim() is { Length: > 0 } trimmedRegion
                ? trimmedRegion
                : null;
            if (declaredRegion is not null && !ProviderEntry.IsKnownRegion(declaredRegion))
            {
                Notify(
                    $"region '{declaredRegion}' is not a region: declare 'eu', 'us' or 'local', or "
                        + "press Enter to leave it unset — the custom path asks again",
                    NoticeLevel.Warning
                );
                continue;
            }

            string? modelsUrl = await PromptAsync(
                    "Models URL for discovery (optional; Enter skips)",
                    ct
                )
                .ConfigureAwait(false);
            if (modelsUrl is null)
            {
                return null;
            }

            Uri? modelsUri = null;
            if (modelsUrl.Trim().Length > 0)
            {
                if (
                    !CustomProvider.TryParseEndpoint(
                        modelsUrl,
                        out modelsUri,
                        out string? modelsError
                    )
                )
                {
                    Notify(
                        $"the models URL was refused: {modelsError} — the custom path asks again",
                        NoticeLevel.Warning
                    );
                    continue;
                }
            }

            return new WizardProviderChoice(
                id,
                Custom: true,
                Region: declaredRegion,
                endpoint,
                family,
                parsedPlacement!,
                modelsUri
            );
        }
    }

    /// <summary>Step 2: the credential — the provider's standard variable first when it is set
    /// (storing an <c>env:</c> reference), then a stored key, then the masked prompt, then a
    /// reference form, then skip. The value never echoes, logs, or reaches a store file.</summary>
    private async Task<WizardMove> CredentialStepAsync(
        SetupWizardModel wizard,
        CancellationToken ct
    )
    {
        if (wizard.Provider is not { } provider)
        {
            wizard.Skip(SetupStep.Credential);
            return WizardMove.Advance;
        }

        List<string> labels = [];
        List<string> values = [];

        if (provider.Custom)
        {
            // The custom path has no standard variable: a typed key, a stored name or a reference.
        }
        else if (ProviderPresets.Find(provider.Id) is { } preset)
        {
            string variable = preset.KeyVariable;
            string? value = _environment(variable);
            string? aliasValue = preset.KeyVariableAlias is { } alias ? _environment(alias) : null;
            string? chosenVariable =
                !string.IsNullOrEmpty(value) ? variable
                : !string.IsNullOrEmpty(aliasValue) ? preset.KeyVariableAlias
                : null;
            if (chosenVariable is not null)
            {
                // The environment key is the first offer: the credential is never stored.
                labels.Add($"use {chosenVariable} from the environment");
                values.Add($"env:{chosenVariable}");
            }
        }

        foreach ((string name, _) in _auth.Keys)
        {
            labels.Add($"use the stored key '{name}'");
            values.Add($"auth:{name}");
        }

        labels.Add("enter a key (it is masked and stored only in the auth store)");
        values.Add(WizardTypedValue);
        labels.Add("name an env: or auth: reference instead");
        values.Add(WizardReferenceValue);
        labels.Add("Skip credential setup");
        values.Add(WizardSkipValue);

        string? credential = await PickAsync(
                $"Credential for {provider.Id}",
                labels,
                values,
                PickerKind.WizardChoice,
                ct
            )
            .ConfigureAwait(false);
        {
            if (credential is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(credential) is { } verb)
            {
                return verb;
            }
        }

        if (credential == WizardSkipValue)
        {
            wizard.Skip(SetupStep.Credential);
            return WizardMove.Advance;
        }

        if (credential is WizardTypedValue)
        {
            string? key = await SecretPromptAsync("API key for " + provider.Id, ct)
                .ConfigureAwait(false);
            if (key is null)
            {
                return WizardMove.Abort;
            }

            // The credential value stages in memory only; the commit stores it in the auth store.
            wizard.RecordCredential(new WizardCredentialChoice(provider.Id, key.Trim()));
            return WizardMove.Advance;
        }

        if (credential is WizardReferenceValue)
        {
            string? reference = await PromptAsync(
                    "Credential reference (env:<VAR> or auth:<name>)",
                    ct
                )
                .ConfigureAwait(false);
            if (reference is null)
            {
                return WizardMove.Abort;
            }

            reference = reference.Trim();
            if (!IsCredentialReference(reference))
            {
                Notify(
                    "a credential reference is 'env:<VARIABLE>' or 'auth:<name>'; the credential "
                        + "step asks again",
                    NoticeLevel.Warning
                );
                return WizardMove.Stay;
            }

            wizard.RecordCredential(new WizardCredentialChoice(reference));
            return WizardMove.Advance;
        }

        wizard.RecordCredential(new WizardCredentialChoice(credential));
        return WizardMove.Advance;
    }

    private static bool IsCredentialReference(string value)
    {
        static bool HasNamedTarget(string value, string prefix) =>
            value.StartsWith(prefix, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(value[prefix.Length..]);

        return HasNamedTarget(value, "env:") || HasNamedTarget(value, "auth:");
    }

    /// <summary>Step 3: one real discovery request. A failure names the shape and the go-back is
    /// the only retry; success keeps the listing for the model step. No automatic retry.</summary>
    private async Task<WizardMove> ConnectionTestStepAsync(
        SetupWizardModel wizard,
        CancellationToken ct
    )
    {
        if (wizard.Provider is null || wizard.Credential is null)
        {
            wizard.Skip(SetupStep.ConnectionTest);
            wizard.RecordConnectionTest(new WizardCheckResult(WizardProgress.Skipped));
            _listing = [];
            return WizardMove.Advance;
        }

        string? action = await PickAsync(
                "Connection test (one discovery request; skippable)",
                ["Run the connection test", "Skip the connection test"],
                ["run", "skip"],
                PickerKind.WizardChoice,
                ct,
                "run"
            )
            .ConfigureAwait(false);
        {
            if (action is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(action) is { } verb)
            {
                return verb;
            }
        }

        if (action == "skip")
        {
            wizard.Skip(SetupStep.ConnectionTest);
            wizard.RecordConnectionTest(new WizardCheckResult(WizardProgress.Skipped));
            _listing = [];
            return WizardMove.Advance;
        }

        Notify("connection test: one discovery request to the provider's models URL…");
        string? credentialKey(string name) => _auth.TryGet(name);
        WizardDiscoveryResult result = await WizardDiscovery
            .RunAsync(
                wizard.Provider!,
                wizard.Credential,
                _environment,
                credentialKey,
                _options.DiscoveryTransport,
                ct
            )
            .ConfigureAwait(false);
        if (result.Passed)
        {
            wizard.RecordConnectionTest(new WizardCheckResult(WizardProgress.Passed));
            _listing = result.Models;
            return WizardMove.Advance;
        }

        Notify(
            $"connection test failed: {ConnectionShape(result)}. Nothing was written; the go-back "
                + "re-opens the credential step with the earlier answers retained, and there is no "
                + "automatic retry",
            NoticeLevel.Warning
        );
        string? after = await PickAsync(
                "After the failed connection test",
                ["Back to the credential step", "Skip the test for now"],
                ["back", "skip"],
                PickerKind.WizardChoice,
                ct
            )
            .ConfigureAwait(false);
        {
            if (after is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(after) is { } verb)
            {
                return verb;
            }
        }

        if (after == "back")
        {
            return WizardMove.Back;
        }

        wizard.RecordConnectionTest(new WizardCheckResult(WizardProgress.Skipped));
        _listing = [];
        return WizardMove.Advance;
    }

    private static string ConnectionShape(WizardDiscoveryResult result) =>
        result.FailureKind switch
        {
            WizardFailureKind.Network => "network",
            WizardFailureKind.Authentication => "authentication",
            WizardFailureKind.Rejected => "rejected status",
            _ => "undecipherable response",
        };

    /// <summary>Step 4: the model from the listing (type-ahead over ids, the resolved family shown
    /// beside each) or manual entry when discovery was skipped or unavailable.</summary>
    private async Task<WizardMove> ModelStepAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        if (wizard.Provider is not { } provider)
        {
            wizard.Skip(SetupStep.Model);
            return WizardMove.Advance;
        }

        List<string> labels = [];
        List<string> values = [];
        foreach (WizardModelListing entry in _listing)
        {
            labels.Add($"{entry.Id} ({entry.ApiFamily ?? provider.ApiFamily})");
            values.Add(entry.Id);
        }

        labels.Add("enter a model id manually");
        values.Add(WizardTypedValue);
        labels.Add("Skip model setup");
        values.Add(WizardSkipValue);

        string? chosen = await PickAsync(
                _listing.Count == 0
                    ? "Model (discovery listed nothing — enter an id manually)"
                    : "Model (type to search the listing)",
                labels,
                values,
                PickerKind.WizardModel,
                ct
            )
            .ConfigureAwait(false);
        {
            if (chosen is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(chosen) is { } verb)
            {
                return verb;
            }
        }

        if (chosen == WizardSkipValue)
        {
            wizard.Skip(SetupStep.Model);
            return WizardMove.Advance;
        }

        string modelId;
        string familyName;
        if (chosen is WizardTypedValue)
        {
            string? manual = await PromptAsync("Model id", ct).ConfigureAwait(false);
            if (manual is null)
            {
                return WizardMove.Abort;
            }

            modelId = manual.Trim();
            familyName = provider.ApiFamily;
            if (modelId.Length == 0)
            {
                wizard.Skip(SetupStep.Model);
                return WizardMove.Advance;
            }
        }
        else
        {
            WizardModelListing entry = _listing[values.IndexOf(chosen)];
            modelId = entry.Id;
            familyName = entry.ApiFamily ?? provider.ApiFamily;
            wizard.RecordModel(new WizardModelChoice(modelId, familyName, entry.Reasoning));
            return WizardMove.Advance;
        }

        wizard.RecordModel(new WizardModelChoice(modelId, familyName));
        return WizardMove.Advance;
    }

    /// <summary>Step 5: one minimal real request through the resolved family — the cost stated
    /// before it runs, skippable; a failure names the model, the family and the response, and the
    /// go-back re-opens the model step. Discovery success alone never marks a model verified.</summary>
    private async Task<WizardMove> ModelCheckStepAsync(
        SetupWizardModel wizard,
        CancellationToken ct
    )
    {
        if (wizard.Model is null)
        {
            wizard.Skip(SetupStep.ModelCheck);
            wizard.RecordModelCheck(new WizardCheckResult(WizardProgress.Skipped));
            return WizardMove.Advance;
        }

        WizardModelChoice choice = wizard.Model!;
        ModelInfo checkModel = CheckModel(wizard, choice);
        string cost = CostCalculator.Compute(checkModel, inputTokens: 8, outputTokens: 1)
            is decimal estimate
            ? CostCalculator.Format(estimate)
            : "not available (no published price)";
        string? answered = await PickAsync(
                $"Model check: estimated cost {cost}; one short request to {choice.ModelId} ({choice.ApiFamily}) — skippable",
                ["Send the model check", "Skip the model check"],
                ["send", "skip"],
                PickerKind.WizardChoice,
                ct,
                "skip"
            )
            .ConfigureAwait(false);
        {
            if (answered is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(answered) is { } verb)
            {
                return verb;
            }
        }

        if (answered == "skip")
        {
            wizard.RecordModelCheck(new WizardCheckResult(WizardProgress.Skipped));
            return WizardMove.Advance;
        }

        WizardCheckResult check = await RunModelCheckAsync(wizard, ct).ConfigureAwait(false);
        wizard.RecordModelCheck(check);
        if (check.Outcome == WizardProgress.Passed)
        {
            Notify($"model check passed: {choice.ModelId} ({choice.ApiFamily})");
            return WizardMove.Advance;
        }

        Notify(
            $"the model check failed for {choice.ModelId} ({choice.ApiFamily}): "
                + $"{check.Detail ?? "the request was refused"}. You can go back to the model "
                + "step or leave the model unverified",
            NoticeLevel.Warning
        );
        string? after = await PickAsync(
                "After the failed model check",
                ["Back to the model step", "Skip (the model stays unverified)"],
                ["back", "skip"],
                PickerKind.WizardChoice,
                ct
            )
            .ConfigureAwait(false);
        {
            if (after is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(after) is { } verb)
            {
                return verb;
            }
        }

        return after == "back" ? WizardMove.Back : WizardMove.Advance;
    }

    /// <summary>
    /// The minimal request through the shared check helper, so the wizard and the flag path
    /// (<c>lunate setup</c>) verify exactly the same request shape.
    /// </summary>
    private Task<WizardCheckResult> RunModelCheckAsync(
        SetupWizardModel wizard,
        CancellationToken ct
    ) =>
        WizardModelCheck.RunAsync(
            wizard,
            _catalog,
            _factory,
            _auth,
            _environment,
            () => ResolveWizardCredential(wizard),
            SessionRequestContext(),
            ct
        );

    /// <summary>The request's model: a known catalog entry stays itself; the custom path builds
    /// the model on the entry's endpoint, placement and models URL; a preset rides its kind.</summary>
    private ModelInfo CheckModel(SetupWizardModel wizard, WizardModelChoice choice) =>
        WizardModelCheck.ModelFor(wizard, choice, _catalog);

    private string? ResolveWizardCredential(SetupWizardModel wizard)
    {
        if (wizard.Credential is not { } credential)
        {
            return null;
        }

        if (credential.Key is { Length: > 0 } key)
        {
            return key;
        }

        if (credential.Reference.StartsWith("env:", StringComparison.Ordinal))
        {
            return _environment(credential.Reference["env:".Length..]);
        }

        return credential.Reference.StartsWith("auth:", StringComparison.Ordinal)
            ? _auth.TryGet(credential.Reference["auth:".Length..])
            : null;
    }

    /// <summary>Step 6: chat profile — the name validated as the settings parser parses one, and
    /// the reasoning levels the chosen model actually declares (the question is skipped entirely
    /// for a model without a reasoning style).</summary>
    private async Task<WizardMove> ChatProfileStepAsync(
        SetupWizardModel wizard,
        CancellationToken ct
    )
    {
        if (wizard.Model is null)
        {
            wizard.Skip(SetupStep.ChatProfile);
            return WizardMove.Advance;
        }

        WizardModelChoice choice = wizard.Model!;
        string? name = await PromptAsync("Profile name (no spaces or '/')", ct)
            .ConfigureAwait(false);
        if (name is null)
        {
            return WizardMove.Abort;
        }

        name = name.Trim();
        if (name.Length == 0)
        {
            wizard.Skip(SetupStep.ChatProfile);
            return WizardMove.Advance;
        }

        if (name.Any(char.IsWhiteSpace) || name.Contains('/'))
        {
            Notify(
                "a profile name must have no whitespace or '/'; the profile step asks again",
                NoticeLevel.Warning
            );
            return WizardMove.Stay;
        }

        if (_settings.Profiles.ContainsKey(name) || wizard.DecisionProfile?.ProfileName == name)
        {
            Notify(
                $"profile '{name}' is already configured; a confirmed summary replaces it",
                NoticeLevel.Info
            );
        }

        ModelInfo model = CheckModel(wizard, choice);
        // The model declares its reasoning style in the discovery listing's per-model metadata
        // when it served one; the catalog's entries carry the same declaration for known models.
        string? declared = choice.Reasoning ?? model.Reasoning;
        string? level = null;
        if (declared is "effort" or "budget")
        {
            string? selected = await PickAsync(
                    $"Thinking level for {choice.ModelId}",
                    ["low", "medium", "high"],
                    ["low", "medium", "high"],
                    PickerKind.WizardChoice,
                    ct
                )
                .ConfigureAwait(false);
            {
                if (selected is null)
                {
                    return WizardMove.Abort;
                }

                if (VerbOf(selected) is { } verb)
                {
                    return verb;
                }
            }

            level = selected;
        }

        wizard.RecordChatProfile(new WizardProfileChoice(name, choice.ModelId, level));
        return WizardMove.Advance;
    }

    /// <summary>Step 7a: binding the chat profile as the <c>fast</c> usage — skip is the default,
    /// the summary names the binding when it is staged.</summary>
    private async Task<WizardMove> FastStepAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        if (wizard.ChatProfile is null)
        {
            wizard.RecordFastProfile(false);
            return WizardMove.Advance;
        }

        string? answered = await PickAsync(
                wizard.ChatProfile is { } chat
                    ? $"Also use profile '{chat.Name}' as the fast usage?"
                    : "Bind a fast usage?",
                ["Skip", "Yes, bind it as fast"],
                ["skip", "fast"],
                PickerKind.WizardChoice,
                ct,
                "skip"
            )
            .ConfigureAwait(false);
        {
            if (answered is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(answered) is { } verb)
            {
                return verb;
            }
        }

        if (answered == "fast" && wizard.ChatProfile is null)
        {
            Notify(
                "the fast usage needs a chat profile; the walk returns to the profile step",
                NoticeLevel.Warning
            );
            wizard.Enter(SetupStep.ChatProfile);
            return WizardMove.Back;
        }

        wizard.RecordFastProfile(answered == "fast");
        if (answered == "skip")
        {
            // The default is listed as the selected row: skip invents no value.
            wizard.Skip(SetupStep.FastProfile);
        }

        return WizardMove.Advance;
    }

    /// <summary>Step 7b: a decision profile — the preset's pinned Jev version as the native
    /// <c>systemone</c> profile when the provider exposes that family, an adapter labelled as such
    /// otherwise; the binding (<c>decisions.profile</c>) enables no consumer.</summary>
    private async Task<WizardMove> DecisionStepAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        if (wizard.Provider is null || wizard.Model is null)
        {
            wizard.RecordDecisionProfile(null);
            return WizardMove.Advance;
        }

        bool native = ProviderExposesSystemOne(wizard);
        IReadOnlyList<string> jevVersions = JevVersions(wizard);
        List<string> labels = [];
        List<string> values = [];
        foreach (string version in jevVersions)
        {
            labels.Add(
                wizard.Provider?.Id == "opencode-go"
                    ? $"{version} (native Jev — billed to your OpenCode pay-as-you-go balance, not the Go plan; no box questions)"
                    : $"{version} (native Jev)"
            );
            values.Add(version);
        }

        if (!native)
        {
            labels.Add($"{wizard.Model?.ModelId ?? wizard.Provider!.Id} (adapter)");
            values.Add(WizardTypedValue);
        }

        labels.Add("adapt the chat model (adapter)");
        values.Add(WizardCustomValue);
        labels.Add("Skip");
        values.Add("skip");

        string? answered = await PickAsync(
                native
                    ? "Decision profile (the provider exposes the native Jev decision profile)"
                    : "Decision profile (this provider serves no native Jev profile; the adapter label applies)",
                labels,
                values,
                PickerKind.WizardModel,
                ct,
                "skip"
            )
            .ConfigureAwait(false);
        {
            if (answered is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(answered) is { } verb)
            {
                return verb;
            }
        }

        if (answered == "skip")
        {
            wizard.RecordDecisionProfile(null);
            return WizardMove.Advance;
        }

        string modelId = answered switch
        {
            WizardCustomValue => wizard.Model!.ModelId,
            WizardTypedValue => wizard.Model!.ModelId,
            _ => answered,
        };
        bool isNative = native && answered is not (WizardCustomValue or WizardTypedValue);
        string? pinned = isNative ? modelId : null;
        wizard.RecordDecisionProfile(
            new WizardDecisionChoice("decisions", modelId, isNative, pinned)
        );
        return await DecisionCheckAsync(wizard, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Step 7b's check: one tiny noul through the decision client the profile will use — the
    /// native System One transport for a native profile, the adapter's chat client otherwise. The
    /// cost is stated before it runs and the check is skippable; a skip or a failure leaves the
    /// decision profile unverified in the summary (spec: The full walk, step 5).
    /// </summary>
    private async Task<WizardMove> DecisionCheckAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        WizardDecisionChoice decision = wizard.DecisionProfile!;
        ModelInfo checkModel = WizardModelCheck.DecisionModelFor(wizard, decision, _catalog);
        string cost = CostCalculator.Compute(checkModel, inputTokens: 8, outputTokens: 1)
            is decimal estimate
            ? CostCalculator.Format(estimate)
            : "not available (no published price)";
        string kind = decision.Native ? "native" : "adapter";
        string? answered = await PickAsync(
                $"Decision check: estimated cost {cost}; one tiny noul to {decision.ModelId} ({kind}) — skippable",
                ["Send the decision check", "Skip the decision check"],
                ["send", "skip"],
                PickerKind.WizardChoice,
                ct,
                "skip"
            )
            .ConfigureAwait(false);
        {
            if (answered is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(answered) is { } verb)
            {
                return verb;
            }
        }

        if (answered == "skip")
        {
            wizard.RecordDecisionCheck(new WizardCheckResult(WizardProgress.Skipped));
            return WizardMove.Advance;
        }

        WizardCheckResult check = await RunDecisionCheckAsync(wizard, ct).ConfigureAwait(false);
        wizard.RecordDecisionCheck(check);
        if (check.Outcome == WizardProgress.Passed)
        {
            Notify($"decision check passed: {decision.ModelId} ({kind})");
            return WizardMove.Advance;
        }

        Notify(
            $"the decision check failed for {decision.ModelId} ({kind}): "
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
        {
            if (after is null)
            {
                return WizardMove.Abort;
            }

            if (VerbOf(after) is { } verb)
            {
                return verb;
            }
        }

        if (after == "again")
        {
            wizard.RecordDecisionProfile(null);
            return WizardMove.Stay;
        }

        return WizardMove.Advance;
    }

    /// <summary>The shared decision-check helper, so the wizard and the flag path send the same
    /// request through the same resolution.</summary>
    private Task<WizardCheckResult> RunDecisionCheckAsync(
        SetupWizardModel wizard,
        CancellationToken ct
    ) =>
        WizardModelCheck.RunDecisionAsync(
            wizard,
            _catalog,
            _factory,
            _auth,
            _environment,
            () => ResolveWizardCredential(wizard),
            ct
        );

    private bool ProviderExposesSystemOne(SetupWizardModel wizard) =>
        wizard.Provider is { } provider
        && (
            provider.Custom is false
            && (
                ProviderPresets.Find(provider.Id)?.Families.Contains(ProviderApiFamily.SystemOne)
                ?? false
            )
        );

    private IReadOnlyList<string> JevVersions(SetupWizardModel wizard) =>
        [
            .. _catalog
                .Models.Where(model =>
                    model.Provider == wizard.Provider!.Id
                    && model.ApiFamily == ProviderApiFamilies.SystemOne
                )
                .Select(model => model.Id),
        ];
}
