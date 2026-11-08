using System.IO;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding;

/// <summary>
/// One child process started with captured standard streams and no terminal. A tool-call child
/// must never own the terminal: its stdin is at end-of-file and it runs in its own session with no
/// controlling terminal, so it cannot open <c>/dev/tty</c> and reconfigure Lunate's raw mode.
/// </summary>
internal abstract class ChildProcess : IExecutionProcess
{
    /// <summary>The child's process id, for the tree kill.</summary>
    public abstract int Id { get; }

    public abstract StreamReader StandardOutput { get; }

    public abstract StreamReader StandardError { get; }

    /// <summary>The child's stdin writer; null when stdin is already at end-of-file.</summary>
    public abstract StreamWriter? StandardInput { get; }

    /// <summary>Waits for the child to exit and returns its exit code.</summary>
    public abstract Task<int> WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Kills the whole process tree and reaps the child.</summary>
    public abstract Task KillTreeAsync();

    /// <summary>The local environment's cancel hook: the existing tree kill.</summary>
    public ValueTask CancelAsync() => new(KillTreeAsync());

    public abstract void Dispose();

    public static ChildProcess Start(ProcessRunRequest request) =>
        OperatingSystem.IsWindows()
            ? WindowsChildProcess.Spawn(request)
            : UnixChildProcess.Spawn(request);

    /// <summary>
    /// The tool child's environment: the builder's <see cref="ChildEnvironmentPolicy.Inherited"/>
    /// policy — the caller's replacement when one is given, otherwise the caller's own
    /// environment — with <see cref="ProcessRunRequest.ExtraEnvironment"/> applied on top and
    /// <see cref="CredentialVariables"/> stripped from everything inherited.
    /// </summary>
    /// <remarks>
    /// The bash tool is the user's shell, so the user's own environment is what it must see. What it
    /// must not see is a credential Lunate resolved: a model-chosen command reading the user's
    /// provider key out of the environment is the leak the bash guard's <c>auth.json</c> read-deny
    /// closes for files. The stripping itself is the builder's (one implementation); this method
    /// only names the policy and the names. <c>ChildEnvironmentGuardTests</c> pins both.
    /// </remarks>
    internal static IReadOnlyDictionary<string, string> BuildEnvironment(ProcessRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnvironmentScrubber.Build(
            ChildEnvironmentPolicy.Inherited,
            set: request.ExtraEnvironment,
            strip: CredentialVariables.All,
            inherited: request.Environment
        );
    }
}
