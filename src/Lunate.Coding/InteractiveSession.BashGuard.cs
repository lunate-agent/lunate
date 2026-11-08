using Lunate.Tui;

namespace Lunate.Coding;

internal sealed partial class InteractiveSession
{
    private BashGuard? _bashGuard;
    private BashGuardStatus _bashGuardStatus = BashGuardStatus.Disabled;
    private ContainerEnvironment? _containerEnvironment;

    /// <summary>
    /// Builds the selected environment without creating a container: creation is lazy on the first
    /// command, and the header only reads the selection and readiness. A malformed value was
    /// already refused by the settings/launch parser.
    /// </summary>
    private static ContainerEnvironment? CreateContainerEnvironment(string? value) =>
        ExecutionEnvironmentSelection.FromValidated(value) is { } selection
            ? new ContainerEnvironment(selection)
            : null;

    private void ResolveBashGuard()
    {
        if (!_settings.BashGuard)
        {
            _bashGuard = null;
            _bashGuardStatus = BashGuardStatus.Disabled;
            return;
        }

        _bashGuard = new BashGuard(
            GuardTarget.Resolve(),
            _options.BashGuardProbe ?? GuardProbe.Current
        );
        // The guard wraps the host half. In container mode that half is the engine's exec client;
        // the inner command is limited by the container mount, never by this guard.
        if (_containerEnvironment?.EngineShell is { } engine)
        {
            _bashGuard.Wrap(engine, _workspace.WorktreeRoot);
        }
        else if (new ShellResolver().Resolve() is { } shell)
        {
            _bashGuard.Wrap(shell, _workspace.WorktreeRoot);
        }

        _bashGuardStatus = _bashGuard.Status;
    }
}
