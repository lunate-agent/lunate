namespace Lunate.Coding;

/// <summary>
/// The one refusal ACP gives a container selection. <c>--env</c> is already a usage error with
/// <c>--acp</c>; the user-scope <c>environment</c> setting reaches the same selection without a flag,
/// and ACP keeps tool commands on the host in this change. The serve start refuses it exactly like
/// the flag and the per-session harness refuses it again, because serving a session whose commands
/// would run host-side is the silent fallback the selection forbids (spec: A container environment
/// is selected by flag or user setting — "A selected environment SHALL NOT fall back silently").
/// </summary>
internal static class AcpExecutionEnvironmentRefusal
{
    /// <summary>The exit code of the refusal; the flag's usage error uses the same one.</summary>
    internal const int ExitCode = 2;

    /// <summary>Names the setting, the reason, and the two modes that can run the selection.</summary>
    internal const string Text =
        "the 'environment' setting selects a container environment, which --acp cannot run: ACP "
        + "keeps tool commands on the host. Unset 'environment' in the user settings file, or run "
        + "the interactive or print mode for container-backed tool commands";

    /// <summary>Whether the resolved settings (launch applied) carry a container selection.</summary>
    internal static bool Applies(AgentSettings settings) => settings.Environment is not null;
}

/// <summary>
/// A session's settings name a container environment while ACP serves it. The serve start refuses
/// that before serving; this exception is the per-session answer, because settings resolve again for
/// every <c>session/new</c>. It derives from <see cref="InvalidOperationException"/> so the existing
/// composition-failure handling reports the refusal and answers the editor with it instead of
/// composing a harness that would run tool commands on the host.
/// </summary>
internal sealed class AcpEnvironmentRefusedException()
    : InvalidOperationException(AcpExecutionEnvironmentRefusal.Text);
