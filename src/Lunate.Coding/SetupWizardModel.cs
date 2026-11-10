using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>The wizard's ten steps, walked in this order (spec: The full walk).</summary>
internal enum SetupStep
{
    Provider,
    Credential,
    ConnectionTest,
    Model,
    ModelCheck,
    ChatProfile,
    FastProfile,
    DecisionProfile,
    Decisions,
    Summary,
}

/// <summary>
/// The entry a wizard run serves: the full walk on a first run, or one of the configured install's
/// entry-menu choices (design §8) — a scoped walk that collects only the steps its entry names and
/// writes only what it collected. The walk always ends at the summary and confirms before a write.
/// </summary>
internal enum WizardEntry
{
    WalkEverything,
    AddProvider,
    AddProfile,
    ChangeChatProfile,
    EditDecisions,
}

/// <summary>A check step's progress: pending, passed, failed with its named reason, or skipped.</summary>
internal enum WizardProgress
{
    Pending,
    Passed,
    Failed,
    Skipped,
}

/// <summary>The outcome of a check step (the connection test, the model check).</summary>
/// <param name="Outcome">The progress at the time the step left.</param>
/// <param name="Detail">A failure's reason — network, authentication, an HTTP status or shape.</param>
internal sealed record WizardCheckResult(WizardProgress Outcome, string? Detail = null);

/// <summary>
/// The provider step's answer: the preset id, or the custom path's own fields. A custom provider
/// carries its endpoint, its API family (a family name the provider families accept), the
/// credential placement the family's wire uses, an optional models URL and its region (shown for
/// presets, asked for custom). The id names the entry everywhere the answers refer to it.
/// </summary>
internal sealed record WizardProviderChoice(
    string Id,
    bool Custom,
    string? Region,
    Uri? Endpoint,
    string ApiFamily,
    CredentialPlacement Placement,
    Uri? ModelsUrl
);

/// <summary>
/// The credential step's answer: either a reference the stores resolve (<c>env:&lt;VARIABLE&gt;</c>
/// for an environment key, <c>auth:&lt;name&gt;</c> for a stored key) or a typed key kept in memory
/// only (<see cref="Key"/>; <see cref="Reference"/> is the provider's own id, the name the key is
/// stored under). The key value never echoes, logs, or reaches
/// <c>providers.json</c>/<c>settings.json</c>.
/// </summary>
internal sealed record WizardCredentialChoice(string Reference, string? Key = null);

/// <summary>
/// The model step's answer: the model id (from discovery's listing, or typed manually when
/// discovery is unavailable or skipped), the resolved API family shown beside it, and the
/// reasoning style the model declares — the discovery listing's per-model metadata when it
/// declared one, falling back to the catalog's. A manually typed id carries none.
/// </summary>
internal sealed record WizardModelChoice(
    string ModelId,
    string ApiFamily,
    string? Reasoning = null
);

/// <summary>
/// The chat profile step's answer: the profile name, the model it runs, and the reasoning level
/// restricted to the levels the chosen model declares (null where the model has none, or when the
/// question was skipped).
/// </summary>
internal sealed record WizardProfileChoice(string Name, string ModelId, string? ReasoningLevel);

/// <summary>
/// The decision-profile step's answer: its profile name, the Jev model it runs, and whether it is
/// the provider's native <c>systemone</c> profile (labelled native, with the preset's pinned
/// version) or an adapter profile (labelled as such in the picker and the summary).
/// </summary>
internal sealed record WizardDecisionChoice(
    string ProfileName,
    string ModelId,
    bool Native,
    string? PinnedVersion
);

/// <summary>
/// The decisions step's answers when the accepted state is not the defaults: the switches' chosen
/// literals (null leaves the key unwritten — the default writes nothing), the routing profiles the
/// walk binds when routing turns on, and the routing descriptions (what the profile is for, what it
/// is not, an example), asked once per routing profile and warned once when one is left blank.
/// </summary>
internal sealed record WizardDecisionsChoice(
    string? Approvals,
    string? Routing,
    string? Tool,
    IReadOnlyList<string> RoutingProfiles,
    IReadOnlyDictionary<string, string> RoutingDescriptions,
    string? Questions = null
);

/// <summary>
/// The setup wizard's state machine (design §2): the current step, the collected answers and the
/// skipped set, staged in memory only — no step writes a file. The driver that feeds it runs inside
/// the session's live area with the picker, masked prompt and question components the session
/// already owns; a step failed by a check goes back to the earlier step without losing later,
/// independent answers, and skipping a step leaves the value it would have configured untouched.
/// </summary>
internal sealed partial class SetupWizardModel
{
    /// <summary>The steps whose answers could change a store, in walk order.</summary>
    internal static readonly IReadOnlyList<SetupStep> Order =
    [
        SetupStep.Provider,
        SetupStep.Credential,
        SetupStep.ConnectionTest,
        SetupStep.Model,
        SetupStep.ModelCheck,
        SetupStep.ChatProfile,
        SetupStep.FastProfile,
        SetupStep.DecisionProfile,
        SetupStep.Decisions,
        SetupStep.Summary,
    ];

    private readonly HashSet<SetupStep> _skipped = [];

    /// <summary>The steps whose answer was carried in from the current configuration: an entry
    /// run's prefill. Carried values drive the later steps and show in the summary, but they are
    /// never staged writes — an entry writes only what it collected (spec: the entry menu).</summary>
    private readonly HashSet<SetupStep> _carried = [];

    /// <summary>The entry the walk serves; the driver asks it for the walked range.</summary>
    public WizardEntry Entry { get; private set; } = WizardEntry.WalkEverything;

    /// <summary>The chat usage an entry walk rebinds (<c>/setup</c>'s "change the chat profile"):
    /// the profile the <c>chat</c> usage points at after this run, written alone — the profile
    /// entries themselves stay as the settings file has them.</summary>
    public string? ChatUsageBinding { get; private set; }

    /// <summary>Whether the chat profile the walk stages also binds the <c>chat</c> usage. The
    /// full walk rebinds; an "add a profile" entry adds the profile without switching the chat
    /// usage (that switch is "change the chat profile"'s own answer).</summary>
    public bool BindChatUsage { get; private set; } = true;

    /// <summary>The step the walk is at now; the driver re-enters steps on go-back.</summary>
    public SetupStep Step { get; private set; } = SetupStep.Provider;

    /// <summary>The provider answer, or null while the step has not been answered.</summary>
    public WizardProviderChoice? Provider { get; private set; }

    /// <summary>The credential answer, or null while the step has not been answered.</summary>
    public WizardCredentialChoice? Credential { get; private set; }

    /// <summary>The connection test's result, or null before it ran (its Pending state).</summary>
    public WizardCheckResult? ConnectionTest { get; private set; }

    /// <summary>The model answer, or null while the step has not been answered.</summary>
    public WizardModelChoice? Model { get; private set; }

    /// <summary>The model check's result, or null before it ran (its Pending state).</summary>
    public WizardCheckResult? ModelCheck { get; private set; }

    /// <summary>The decision-profile check's result — one tiny noul through the decision client —
    /// or null before it ran (its Pending state, or no decision profile in this walk).</summary>
    public WizardCheckResult? DecisionCheck { get; private set; }

    /// <summary>The chat profile answer, or null while the step has not been answered.</summary>
    public WizardProfileChoice? ChatProfile { get; private set; }

    /// <summary>Whether the chat profile should also serve the <c>fast</c> usage (the fast step).</summary>
    public bool FastProfile { get; private set; }

    /// <summary>The decision profile answer, or null when the step skipped or stayed unanswered.</summary>
    public WizardDecisionChoice? DecisionProfile { get; private set; }

    /// <summary>The decisions answers, or null when the defaults were accepted (nothing written).</summary>
    public WizardDecisionsChoice? Decisions { get; private set; }

    /// <summary>The routing profiles the decisions step's ask drew from — the walk's new profiles
    /// plus the settings file's configured ones; set by the driver before the summary blocks on it.</summary>
    public IReadOnlyList<string> ExistingRoutingProfiles { get; set; } = [];

    /// <summary>Whether the step was skipped; a skipped step leaves its value untouched.</summary>
    public bool IsSkipped(SetupStep step) => _skipped.Contains(step);

    /// <summary>The skipped steps, in walk order, for the summary.</summary>
    public IReadOnlyList<SetupStep> SkippedSteps =>
        [.. Order.Where(step => _skipped.Contains(step))];

    /// <summary>Moves the walk to a step — forward through the walk, or back to an earlier one.</summary>
    public void Enter(SetupStep step) => Step = step;

    /// <summary>
    /// Marks the step skipped: no value is invented for it. The summary is the confirm gate, not an
    /// answer, so skipping it is a no-op.
    /// </summary>
    public void Skip(SetupStep step)
    {
        if (step is not SetupStep.Summary)
        {
            _skipped.Add(step);
        }
    }

    /// <summary>Names the entry whose walk this run serves and resets the walk to its first step.
    /// Answered state stays untouched: the prefill (a carried walk's context) arrives before.</summary>
    public void Begin(WizardEntry entry)
    {
        Entry = entry;
        Step = FirstWalked;
    }

    /// <summary>The entry's first walked step; scoped entries begin where their scope begins.</summary>
    internal SetupStep FirstWalked =>
        Entry switch
        {
            WizardEntry.AddProfile => SetupStep.Model,
            WizardEntry.ChangeChatProfile => SetupStep.ChatProfile,
            WizardEntry.EditDecisions => SetupStep.Decisions,
            _ => SetupStep.Provider,
        };

    /// <summary>The entry's last walked step; the walk goes to the summary after it. Quick's full
    /// walk ends at the chat profile because every later step is left at its default.</summary>
    internal SetupStep LastWalked =>
        IsQuick
            ? SetupStep.ChatProfile
            : Entry switch
            {
                WizardEntry.AddProvider => SetupStep.ConnectionTest,
                WizardEntry.AddProfile => SetupStep.DecisionProfile,
                WizardEntry.ChangeChatProfile => SetupStep.ChatProfile,
                WizardEntry.EditDecisions => SetupStep.Decisions,
                _ => SetupStep.Decisions,
            };

    /// <summary>
    /// Carries the current configuration's answers into the walk as an entry run's context. A
    /// carried answer is not a write: the steps the entry walks consume it, the summary shows it,
    /// and the commit never rewrites it (the stores already have it). Re-answering a carried step
    /// un-carries it and the re-answer becomes this run's staged answer.
    /// </summary>
    internal void CarryContext(
        WizardProviderChoice? provider,
        WizardCredentialChoice? credential,
        WizardModelChoice? model,
        WizardProfileChoice? chat
    )
    {
        if (provider is { } carriedProvider)
        {
            Provider = carriedProvider;
            _carried.Add(SetupStep.Provider);
        }

        if (credential is { } carriedCredential)
        {
            Credential = carriedCredential;
            _carried.Add(SetupStep.Credential);
        }

        if (model is { } carriedModel)
        {
            Model = carriedModel;
            _carried.Add(SetupStep.Model);
        }

        if (chat is { } carriedChat)
        {
            ChatProfile = carriedChat;
            _carried.Add(SetupStep.ChatProfile);
        }
    }

    /// <summary>Whether the step's answer was carried from the current configuration, not
    /// collected in this run. The summary names carried steps "current"; the commit skips them.</summary>
    internal bool IsCarried(SetupStep step) => _carried.Contains(step);

    /// <summary>
    /// Stages the chat usage binding an entry walk collected ("change the chat profile"): the
    /// <c>chat</c> usage re-points at a profile the settings file already configures. The profile
    /// entries are not touched by this answer.
    /// </summary>
    public void RecordChatUsageBinding(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        ChatUsageBinding = profileName;
    }

    private void ClearSkipsForWalkedRange(SetupStep answered)
    {
        bool clearing = false;
        foreach (SetupStep candidate in Order)
        {
            if (candidate == answered)
            {
                clearing = true;
            }

            if (clearing && candidate <= LastWalked)
            {
                _skipped.Remove(candidate);
            }
        }
    }

    /// <summary>
    /// Stages the provider answer and clears the answers behind it: a changed provider re-asks the
    /// credential, the checks and the model, all of which the new provider's steps would answer
    /// against different facts. The profile names survive (the driver re-validates them).
    /// </summary>
    public void RecordProvider(WizardProviderChoice choice)
    {
        if (Provider == choice)
        {
            // Re-selecting the same provider (the mode row sends the step around again) invents
            // nothing and must not clear the answers the walk already collected.
            return;
        }

        Provider = choice;
        Credential = null;
        ConnectionTest = null;
        Model = null;
        ModelCheck = null;
        DecisionCheck = null;
        ChatProfile = null;
        _carried.Remove(SetupStep.Provider);
        ClearSkipsForWalkedRange(SetupStep.Provider);
    }

    /// <summary>
    /// Stages the credential answer and clears what depends on it: the connection test, the model
    /// and its check, and the chat profile (whose thinking levels depend on the model).
    /// </summary>
    public void RecordCredential(WizardCredentialChoice choice)
    {
        Credential = choice;
        ConnectionTest = null;
        Model = null;
        ModelCheck = null;
        DecisionCheck = null;
        ChatProfile = null;
        _carried.Remove(SetupStep.Credential);
        ClearSkipsForWalkedRange(SetupStep.Credential);
    }

    public void RecordConnectionTest(WizardCheckResult result) => ConnectionTest = result;

    /// <summary>Stages the model answer and clears the model check and the profile (a thinking
    /// level is chosen from the model's declared styles, so both re-walk).</summary>
    public void RecordModel(WizardModelChoice choice)
    {
        Model = choice;
        ModelCheck = null;
        DecisionCheck = null;
        ChatProfile = null;
        _carried.Remove(SetupStep.Model);
        ClearSkipsForWalkedRange(SetupStep.Model);
    }

    public void RecordModelCheck(WizardCheckResult result) => ModelCheck = result;

    /// <summary>
    /// Stages the decision-profile check's result. A new provider, credential or model answer
    /// clears it with the decision profile that answered it: the check verifies that profile's
    /// model, family and credential, so an answer that changes any of them must re-check.
    /// </summary>
    public void RecordDecisionCheck(WizardCheckResult result) => DecisionCheck = result;

    public void RecordChatProfile(WizardProfileChoice choice)
    {
        ChatProfile = choice;
        // The full walk (and the flag path) binds the chat usage; an "add a profile" entry adds
        // the profile only — the chat usage switch is the entry menu's own answer.
        BindChatUsage = Entry is WizardEntry.WalkEverything;
    }

    public void RecordFastProfile(bool include)
    {
        FastProfile = include;
        if (include)
        {
            _skipped.Remove(SetupStep.FastProfile);
        }
        else
        {
            _skipped.Add(SetupStep.FastProfile);
        }
    }

    public void RecordDecisionProfile(WizardDecisionChoice? choice)
    {
        DecisionProfile = choice;
        DecisionCheck = null;
        if (choice is null)
        {
            _skipped.Add(SetupStep.DecisionProfile);
        }
        else
        {
            _skipped.Remove(SetupStep.DecisionProfile);
        }
    }

    public void RecordDecisions(WizardDecisionsChoice? choice)
    {
        Decisions = choice;
        if (choice is null)
        {
            _skipped.Add(SetupStep.Decisions);
        }
        else
        {
            _skipped.Remove(SetupStep.Decisions);
        }
    }

    /// <summary>
    /// Whether any staged answer would change a store: without one a confirmed summary is a valid
    /// but effectively empty configuration and writes nothing at all (spec: Nothing written before
    /// the summary confirms). Carried values are not staged writes — the stores already have them.
    /// </summary>
    public bool HasStagedWrites =>
        (Provider is not null && !IsCarried(SetupStep.Provider))
        || (Credential is not null && !IsCarried(SetupStep.Credential))
        || (Model is not null && !IsCarried(SetupStep.Model))
        || (ChatProfile is not null && !IsCarried(SetupStep.ChatProfile))
        || (FastProfile && !IsCarried(SetupStep.FastProfile))
        || (DecisionProfile is not null && !IsCarried(SetupStep.DecisionProfile))
        || (Decisions is not null && !IsCarried(SetupStep.Decisions))
        || ChatUsageBinding is not null;
}
