using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The configured-state check behind the three wizard entry points (design §1): the session start,
/// the welcome header and <c>lunate setup</c> all ask the same question — does the setup wizard
/// apply, and why — answered from configuration alone. The wizard applies when the user provider
/// store carries no provider entry at all, or the <c>chat</c> usage has no resolvable profile:
/// <c>usages.chat</c> unset, naming an absent profile, or naming a profile that is not configured,
/// and no <c>model</c> default exists. One check, so the header's fix text, the wizard trigger and
/// the CLI can never disagree about what "not configured" means.
/// </summary>
internal static class SetupState
{
    /// <summary>The <c>usages</c> key the chat profile is bound under.</summary>
    internal const string ChatUsage = "chat";

    /// <summary>The reason the wizard applies, or "none" — the machine's word, not prose.</summary>
    internal enum Reason
    {
        /// <summary>Every provider entry and a resolvable chat profile exist; the wizard never opens.</summary>
        NotApplicable,

        /// <summary>The provider store has no entry at all.</summary>
        NoProvider,

        /// <summary>A provider exists but the <c>chat</c> usage resolves no configured profile and no model default.</summary>
        NoChatProfile,
    }

    /// <summary>
    /// The wizard applies when the check says so. A <c>usages.chat</c> profile is resolvable when
    /// it names a profile the settings file configures; a <see cref="AgentSettings.Model"/> default
    /// is the other way a chat model resolves. The provider argument is the resolved provider
    /// entries (the provider store's, including migrated legacy entries), and sources the check
    /// with no network, no clock and no environment read.
    /// </summary>
    public static Reason Check(AgentSettings settings, IReadOnlyList<ProviderEntry> providers)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(providers);

        return providers.Count == 0 ? Reason.NoProvider
            : HasChatAnswer(settings) ? Reason.NotApplicable
            : Reason.NoChatProfile;
    }

    /// <summary>
    /// Whether a chat model resolves at all: <c>usages.chat</c> names a profile the settings file
    /// configures, or a <c>model</c> default exists. The machine modes ask this one question before
    /// they start — a machine without a chat answer is the one the configure-it refusal names.
    /// </summary>
    internal static bool HasChatAnswer(AgentSettings settings) =>
        (
            settings.Usages.TryGetValue(ChatUsage, out string? profileName)
            && settings.Profiles.ContainsKey(profileName)
        ) || !string.IsNullOrWhiteSpace(settings.Model);
}
