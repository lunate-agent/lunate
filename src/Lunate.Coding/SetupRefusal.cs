using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The one configure-it refusal the machine modes share (spec: The wizard trigger — "Non-interactive
/// contexts (print mode, ACP, piped stdin, CI, <c>TERM=dumb</c>) SHALL never open the wizard; the
/// configure-it refusal SHALL name <c>lunate setup</c> and its flags and exit 2"). Print mode, ACP
/// startup and a piped interactive start ask <see cref="Applies"/> before they compose anything and
/// print <see cref="Text"/>; the missing-model path in <see cref="HarnessFactory.ResolveModel"/>
/// raises <see cref="SetupRequiredException"/> with the same text, so no path can point a user at a
/// settings key instead of the command that writes it.
/// </summary>
internal static class SetupRefusal
{
    /// <summary>The exit code the spec requires for a machine that needs setup.</summary>
    internal const int ExitCode = 2;

    /// <summary>Names the command and its flags; one string, so the three modes cannot drift.</summary>
    internal const string Text =
        "no chat model is configured: run `lunate setup` on a terminal to open the setup wizard, or "
        + "`lunate setup --provider <preset-id> --model <model-id> --credential env:<VARIABLE>` to "
        + "configure it without a terminal (`--key-stdin` pipes a key, `--profile <name>` names the "
        + "chat profile, `--endpoint <url>` declares a custom provider)";

    /// <summary>
    /// Whether the machine has no chat answer at all: no <c>usages.chat</c> profile and no
    /// <c>model</c> default. A machine that names either keeps its previous behaviour — print mode
    /// and ACP resolve the <c>model</c> default and report a later failure on their own.
    /// </summary>
    internal static bool Applies(AgentSettings settings) => !SetupState.HasChatAnswer(settings);
}

/// <summary>
/// A chat model was resolved on a machine with no chat answer: every caller reports
/// <see cref="SetupRefusal.Text"/> and exits <see cref="SetupRefusal.ExitCode"/> instead of the
/// pre-0.3 "set 'model'" guidance. It derives from <see cref="InvalidOperationException"/> so the
/// paths that already treat a resolution failure as a configuration error keep doing so.
/// </summary>
internal sealed class SetupRequiredException() : InvalidOperationException(SetupRefusal.Text);
