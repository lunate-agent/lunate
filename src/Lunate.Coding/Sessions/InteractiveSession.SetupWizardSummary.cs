using Lunate.Ai;
using Lunate.Tui;
using Microsoft.Extensions.AI;
using Spectre.Console;

namespace Lunate.Coding;

// The setup wizard's summary, commit and the interaction primitives the steps drive (design §5,
// §8): the summary shows every collected value and every skipped step and writes nothing before it
// is confirmed with Enter; the commit is the one pass through the store writers; the reconciliation
// reloads the composition the way the stores now read.

internal sealed partial class InteractiveSession
{
    /// <summary>Step 9: the file manifest — every collected provider, credential, profile, usage
    /// and decisions value under the file it will reach, the check results, and the settings the
    /// walk left unchanged. Nothing is written before the confirm.</summary>
    private void RenderSummary(SetupWizardModel wizard)
    {
        List<string> lines = ["setup — confirm these values (nothing has been written yet):"];
        lines.AddRange(SetupWizardSummary.Render(wizard, _console.Size.Columns));

        _live.WriteScrollback(console =>
        {
            foreach (string line in lines)
            {
                console.Write(new Markup(Markup.Escape(line + "\n")));
            }
        });
    }

    /// <summary>The summary's confirm: Enter writes, Esc cancels, and Back re-opens the walk at its
    /// last step so a wrong answer can be changed instead of restarting (spec: The walk is
    /// navigable).</summary>
    private async Task<WizardMove> ConfirmAsync(string question, CancellationToken ct)
    {
        string? answered = await PickAsync(
                question,
                ["Write the configuration", "Cancel"],
                ["write", "cancel"],
                PickerKind.WizardChoice,
                ct,
                "write"
            )
            .ConfigureAwait(false);
        return answered switch
        {
            "write" or WizardForwardValue => WizardMove.Advance,
            WizardBackValue => WizardMove.Back,
            _ => WizardMove.Abort,
        };
    }

    private void CommitWizard(SetupWizardModel wizard)
    {
        string settingsPath = _options.SettingsPath ?? AuthStore.SettingsFilePath(null);
        string providerPath = AuthStore.ProviderStoreFilePath(_options.AuthPath);
        SetupWizardCommit.Commit(
            wizard,
            new SetupWizardCommit.CommitPaths(settingsPath, providerPath),
            () => _auth
        );
    }

    /// <summary>The walk's one commit notice, whatever its entry: the stores are written now, they
    /// are read at startup, and the running session is not re-composed from them, so the change
    /// lands on the next start. A walk that resolves a chat profile also switches the model, and
    /// that switch announces itself with its own <c>model:</c> line before this one.</summary>
    private void NotifySetupWritten() =>
        Notify(
            "setup written: the files this walk changed are saved now and read at startup; "
                + "this session keeps its current composition, so the new settings apply from "
                + "the next session."
        );

    /// <summary>
    /// After a confirmed commit the composition follows the stores: the settings, provider
    /// entries, catalog and credential store reload, and the chat profile switches the running
    /// session onto the configured model like a <c>/model</c> choice did.
    /// </summary>
    private async Task ReconcileAsync(SetupWizardModel wizard, CancellationToken ct)
    {
        string settingsPath = _options.SettingsPath ?? AuthStore.SettingsFilePath(null);
        _auth = AuthStore.Load(_options.AuthPath, _options.Environment);
        ProviderStore userProviders = ProviderStore.Load(
            AuthStore.ProviderStoreFilePath(_options.AuthPath),
            _auth,
            []
        );
        _providerEntries = userProviders.Entries;
        _settings = SettingsStore.Resolve(settingsPath, _options.Environment);
        _settings = SettingsStore.ApplyLaunch(_settings, _options.Launch);
        ApplyToolEnvironment();
        _catalog = HarnessFactory.LoadCatalog(_settings, _options.ModelsPath);

        // The chat profile a run staged: the full walk's profile (which also bound the usage),
        // and the entry menu's own answer — "change the chat profile" rebinds the usage alone.
        string? chatName =
            wizard.ChatProfile is { } chat && wizard.BindChatUsage
                ? chat.Name
                : wizard.ChatUsageBinding;
        if (chatName is { } bind)
        {
            try
            {
                ResolvedModelProfile resolved = ModelSelectionResolver.ResolveChatModel(
                    bind,
                    _settings,
                    _providerEntries,
                    _catalog
                );
                UsageProfileValidation.RequireChatFamily(resolved, SetupState.ChatUsage);
                _invalidChatBinding = null;
                if (!_providerEntries.Any(entry => entry.Name == resolved.Provider.Name))
                {
                    _providerEntries = [.. _providerEntries, resolved.Provider];
                }

                _settings = ModelSelectionResolver.SelectChatProfile(_settings, resolved);
                await SwitchToModel(resolved.Model, ct).ConfigureAwait(false);
                return;
            }
            catch (Exception exception)
                when (exception
                        is UnknownModelException
                            or InvalidDataException
                            or InvalidOperationException
                            or ArgumentException
                )
            {
                Notify(
                    $"setup committed the stores; the chat profile did not resolve here: {exception.Message}",
                    NoticeLevel.Warning
                );
            }
        }
    }

    // --- the primitives ---

    private async Task<string?> PickAsync(
        string titlePrefix,
        List<string> labels,
        List<string> values,
        PickerKind kind,
        CancellationToken ct,
        string? selected = null,
        bool menu = false
    )
    {
        var pending = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using CancellationTokenRegistration registration = ct.Register(() =>
            pending.TrySetCanceled(ct)
        );
        int current = values.IndexOf(selected ?? string.Empty);
        // The pick travels with the open request: the owner completes it when it confirms or
        // dismisses this picker, so no other thread completes or clears it.
        await OpenPicker(
                kind,
                titlePrefix,
                labels,
                values,
                current < 0 ? 0 : current,
                menu,
                pending
            )
            .ConfigureAwait(false);
        string? picked = await pending.Task.ConfigureAwait(false);
        return picked == WizardAbortValue ? null : picked;
    }

    private Task<string?> PromptAsync(string label, CancellationToken ct) =>
        _secretPrompt.ReadTextAsync(label, ct);

    /// <summary>The walk's one masked read: the credential key typed at the credential step.</summary>
    private Task<string?> SecretPromptAsync(string label, CancellationToken ct) =>
        _secretPrompt.ReadAsync(label, ct);

    /// <summary>One decision switch through the question component: Enter picks the selected row,
    /// Esc aborts (the walk's abort, never the session's). The selection lands on the default.</summary>
    private async Task<string?> AskQuestionAsync(
        string header,
        string question,
        IReadOnlyList<string> options,
        CancellationToken ct
    ) =>
        await AskSelectAsync(header, question, options, multiSelect: false, ct)
            .ConfigureAwait(false)
            is { Count: > 0 } selected
            ? selected[0]
            : WizardAbortValue;

    /// <summary>
    /// One plural answer through the same question component: Space toggles <c>[x]</c>, Enter
    /// confirms the selection and the question shows how many rows are selected. Esc aborts.
    /// </summary>
    private async Task<IReadOnlyList<string>?> AskMultiSelectAsync(
        string header,
        string question,
        IReadOnlyList<string> options,
        CancellationToken ct
    ) =>
        await AskSelectAsync(header, question, options, multiSelect: true, ct)
            .ConfigureAwait(false);

    private async Task<IReadOnlyList<string>?> AskSelectAsync(
        string header,
        string question,
        IReadOnlyList<string> options,
        bool multiSelect,
        CancellationToken ct
    )
    {
        SelectListQuestionModel model = SelectListQuestionModel.Create(
            header,
            question,
            [.. options.Select(option => new SelectListQuestionOption(option, string.Empty))],
            multiSelect,
            null
        );
        var interaction = new SelectListQuestionInteraction(_scheduler);
        interaction.BeginQuestion(model, Interlocked.Read(ref _keySequence));
        var decision = new TaskCompletionSource<SelectListQuestionModel>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _questionPrompt = model;
        _questionInteraction = interaction;
        _questionDecision = decision;
        _live.SetQuestion(model);
        using CancellationTokenRegistration registration = ct.Register(() =>
            decision.TrySetCanceled(ct)
        );
        try
        {
            SelectListQuestionModel answered = await decision.Task.ConfigureAwait(false);
            return answered.Status == QuestionPromptStatus.Submitted
                ? answered.Answer?.SelectedOptions ?? []
                : null;
        }
        finally
        {
            _questionPrompt = null;
            _questionInteraction = null;
            _questionDecision = null;
            _live.SetQuestion(null);
        }
    }
}

/// <summary>
/// The summary's file manifest: one fact per line, grouped by the file the commit writes, wrapped
/// to the terminal width (80/60 by the fixture tests, 40 by the manifest and receipt pins).
/// </summary>
internal static class SetupWizardSummary
{
    /// <summary>The manifest's lines at the given terminal width: file headings, one fact per line,
    /// and the settings the walk left unchanged. Layout is the renderer's; the content is every
    /// staged value and its file (spec: The file manifest and the receipt).</summary>
    internal static IReadOnlyList<string> Render(
        SetupWizardModel wizard,
        int width = SetupWizardManifest.DefaultWidth
    ) => SetupWizardManifest.Render(wizard, width);
}
