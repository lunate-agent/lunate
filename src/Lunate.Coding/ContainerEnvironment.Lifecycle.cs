using System.ComponentModel;
using System.Globalization;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding;

/// <summary>Container lifecycle, engine calls and teardown for the session environment.</summary>
internal sealed partial class ContainerEnvironment
{
    /// <summary>Removes every container this session created; idempotent and bounded per engine call.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        string[] roots;
        lock (_containers)
        {
            roots = [.. _containers.Keys];
        }

        foreach (string root in roots)
        {
            string? name;
            lock (_containers)
            {
                _containers.Remove(root, out name);
            }

            if (name is not null)
            {
                Remove(name);
            }
        }

        _prepare.Dispose();
    }

    private async Task<string> EnsureAsync(string root, GuardCommand guard, CancellationToken ct)
    {
        lock (_containers)
        {
            if (_containers.TryGetValue(root, out string? existing))
            {
                return existing;
            }
        }

        await _prepare.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            lock (_containers)
            {
                if (_containers.TryGetValue(root, out string? existing))
                {
                    return existing;
                }
            }

            string name = _newName();
            ProcessRunResult create = await RunEngineAsync(
                    Guarded(guard, CreateArguments(root, name)),
                    ct
                )
                .ConfigureAwait(false);
            if (create.ExitCode != 0)
            {
                State = "failed: " + Failure("create", root, create);
                throw new ContainerEnvironmentException(Failure("create", root, create));
            }

            ProcessRunResult start = await RunEngineAsync(Guarded(guard, ["start", name]), ct)
                .ConfigureAwait(false);
            if (start.ExitCode != 0)
            {
                Remove(name);
                State = "failed: " + Failure("start", root, start);
                throw new ContainerEnvironmentException(Failure("start", root, start));
            }

            lock (_containers)
            {
                _containers[root] = name;
            }

            State = "ready";
            return name;
        }
        finally
        {
            _prepare.Release();
        }
    }

    private async Task KillGroupOrRemoveAsync(
        string root,
        string name,
        PgidHolder pgid,
        CancellationToken ct
    )
    {
        int id = pgid.Value;
        if (id > 0)
        {
            await RunKillAsync(name, "TERM", id, ct).ConfigureAwait(false);
            try
            {
                await Task.Delay(KillGrace, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }

            await RunKillAsync(name, "KILL", id, ct).ConfigureAwait(false);
            if (!await GroupAliveAsync(name, id, ct).ConfigureAwait(false))
            {
                return;
            }
        }

        // No pgid, or the group survived: the container cannot be reused safely. Remove it and let
        // the next call create a fresh one.
        lock (_containers)
        {
            _containers.Remove(root);
        }

        Remove(name);
        State = "starting";
    }

    private async Task<ProcessRunResult> RunKillAsync(
        string name,
        string signal,
        int pgid,
        CancellationToken ct
    )
    {
        string script = string.Create(CultureInfo.InvariantCulture, $"kill -{signal} -{pgid}");
        return await RunEngineAsync(
                new ProcessRunRequest(
                    _enginePath!,
                    ["exec", name, "sh", "-c", script],
                    "/",
                    Timeout: KillBudget,
                    OutputCapBytes: OutputCap
                ),
                ct
            )
            .ConfigureAwait(false);
    }

    private async Task<bool> GroupAliveAsync(string name, int pgid, CancellationToken ct)
    {
        string script = string.Create(CultureInfo.InvariantCulture, $"kill -0 -{pgid}");
        ProcessRunResult result = await RunEngineAsync(
                new ProcessRunRequest(
                    _enginePath!,
                    ["exec", name, "sh", "-c", script],
                    "/",
                    Timeout: KillBudget,
                    OutputCapBytes: OutputCap
                ),
                ct
            )
            .ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    private async Task<ProcessRunResult> RunEngineAsync(
        ProcessRunRequest request,
        CancellationToken ct
    )
    {
        try
        {
            return await _run(request, ct, onCancel: null, onFirstStdoutLine: null)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception
                    is Win32Exception
                        or InvalidOperationException
                        or IOException
                        or PlatformNotSupportedException
                        or ArgumentException
            )
        {
            throw new ContainerEnvironmentException(
                $"the {_selection.Engine} engine could not {request.Arguments.FirstOrDefault() ?? "run"}: "
                    + $"{exception.Message}; check that the engine is installed and running"
            );
        }
    }

    private void Remove(string name)
    {
        try
        {
            _run(
                    new ProcessRunRequest(
                        _enginePath!,
                        ["rm", "-f", name],
                        "/",
                        Timeout: KillBudget,
                        OutputCapBytes: OutputCap
                    ),
                    CancellationToken.None,
                    onCancel: null,
                    onFirstStdoutLine: null
                )
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception exception)
            when (exception is not OutOfMemoryException and not OperationCanceledException)
        {
            // Teardown is best-effort; a container the engine already removed is the normal case.
        }
    }

    private static async Task<ProcessRunResult> DefaultRun(
        ProcessRunRequest request,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? onCancel,
        Action<string>? onFirstStdoutLine
    ) =>
        await new ProcessRunner()
            .RunAsync(request, cancellationToken, onCancel, onFirstStdoutLine)
            .ConfigureAwait(false);
}
