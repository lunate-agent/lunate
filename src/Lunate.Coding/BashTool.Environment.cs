using System.Globalization;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding;

public sealed partial class BashTool : ITool
{
    /// <summary>
    /// The container half of the tool: the same guard refusal/hint and result rendering as the host
    /// path, but the command runs through the engine's <c>exec</c> in the session container. The
    /// guard wraps the engine client; it never claims to constrain the inner command, and the
    /// change scan stays on the host so mounted writes are still published.
    /// </summary>
    private async Task<ToolResult> ExecuteInEnvironmentAsync(
        ContainerEnvironment environment,
        string command,
        ToolContext ctx,
        CancellationToken ct
    )
    {
        if (environment.Unavailable is { } unavailable)
        {
            return Error(unavailable);
        }

        if (environment.EngineShell is not { } engine)
        {
            return Error(environment.Unavailable ?? "the container environment is unavailable");
        }

        GuardCommand launch = _guardEnabled
            ? _guard.Wrap(engine, _workspace.WorktreeRoot)
            : new GuardCommand(engine.ExecutablePath, [], "none", Wrapped: false, Notice: null);

        if (_guardEnabled && _guard.Refusal(command, launch) is { } refusal)
        {
            return new ToolResult(refusal, IsError: true);
        }

        ProcessRunResult run;
        try
        {
            run = await environment
                .RunAsync(_workspace.WorktreeRoot, command, _timeout, launch, ct)
                .ConfigureAwait(false);
        }
        catch (ContainerEnvironmentException exception)
        {
            return Error(exception.Message);
        }
        catch (OperationCanceledException)
        {
            await PublishChangesAsync(ctx).ConfigureAwait(false);
            throw;
        }

        await PublishChangesAsync(ctx).ConfigureAwait(false);
        NoticeOnce(launch);
        string? hint = _guardEnabled
            ? _guard.Hint(command, run.Stderr, launch)
            : "bash guard is disabled by settings";
        string stdout = WithCapMarker(run.Stdout, run.StdoutCapped);
        string stderr = WithCapMarker(run.Stderr, run.StderrCapped);
        if (run.TimedOut)
        {
            return Result(stdout, stderr, ContainerTimeoutFooter(), hint, isError: true);
        }

        int exitCode = run.ExitCode!.Value;
        return Result(
            stdout,
            stderr,
            ExitFooter(exitCode),
            hint,
            exitCode != 0,
            new BashDetails(exitCode)
        );
    }

    private string ContainerTimeoutFooter() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"timed out after {_timeout.TotalSeconds:0.###}s; container process group killed"
        );
}
