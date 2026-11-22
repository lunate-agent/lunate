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

    /// <summary>
    /// The refusal for a session's placeholder client: the configured chat binding's own message
    /// when startup refused it (it already names the profile, model, family and both repair
    /// commands), and the configure-it text otherwise. The caller still throws it from the deferred
    /// factory, so no client is created and no request is sent either way.
    /// </summary>
    internal static SetupRequiredException ForPlaceholder(string? invalidChatBinding) =>
        invalidChatBinding is { } binding
            ? new SetupRequiredException(binding)
            : new SetupRequiredException();
}

/// <summary>
/// A chat model could not be resolved: every caller reports the supplied refusal and exits
/// <see cref="SetupRefusal.ExitCode"/> instead of the pre-0.3 "set 'model'" guidance. The
/// argument-less shape keeps <see cref="SetupRefusal.Text"/>; the placeholder passes the refused
/// binding's own message. It derives from <see cref="InvalidOperationException"/> so the paths that
/// already treat a resolution failure as a configuration error keep doing so.
/// </summary>
internal sealed class SetupRequiredException(string? refusal = null)
    : InvalidOperationException(refusal ?? SetupRefusal.Text);
