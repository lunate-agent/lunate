namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// The core process capability (<c>core/process</c>): runs one child process to completion with a
/// timeout, an output cap and a tree kill. On timeout or cancellation the process tree is killed
/// children-first: on POSIX descendants are enumerated with <c>ps</c> and SIGKILLed before their
/// parents (the framework's tree kill SIGSTOPs the tree first and has wedged macOS CI); Windows
/// keeps the framework's job-object path as the fallback. A process that cannot start throws the
/// platform's start exception (<see cref="System.ComponentModel.Win32Exception"/>,
/// <see cref="System.IO.IOException"/> and friends); the caller renders the teaching error.
/// </summary>
public interface IProcessRunner
{
    /// <summary>Runs the request to completion. The result carries exit code, output and flags.</summary>
    Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Starts one interactive child whose stdin, stdout and stderr stay open for line exchange
    /// until the caller stops it; see <see cref="IProcessSession"/>. Start failures throw the
    /// platform's start exception, like <see cref="RunAsync"/>.
    /// </summary>
    Task<IProcessSession> StartSessionAsync(
        ProcessSessionRequest request,
        CancellationToken cancellationToken
    );
}
