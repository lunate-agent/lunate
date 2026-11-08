using System.Globalization;
using Lunate.Ai;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding;

/// <summary>
/// The engine command shapes and the small parsers behind the container environment: create (mount
/// only the workspace at its host path), exec (session marker plus the pgid wrapper) and the
/// wrapper's line handling.
/// </summary>
internal sealed partial class ContainerEnvironment
{
    private ProcessRunRequest Guarded(GuardCommand guard, IReadOnlyList<string> arguments) =>
        new(
            guard.FileName,
            [.. guard.ArgumentsPrefix, .. arguments],
            "/",
            Timeout: EngineBudget,
            OutputCapBytes: OutputCap
        );

    private IReadOnlyList<string> CreateArguments(string root, string name) =>
        [
            "create",
            "--name",
            name,
            "-v",
            string.Create(CultureInfo.InvariantCulture, $"{root}:{root}"),
            "-w",
            root,
            "-e",
            SessionVariable(),
            _selection.Image,
            "sleep",
            "2147483647",
        ];

    private ProcessRunRequest ExecRequest(
        string root,
        string name,
        string command,
        GuardCommand guard,
        TimeSpan timeout
    )
    {
        const string wrapper = "echo LUNATE_PGRP:$(cut -d' ' -f5 /proc/$$/stat); exec sh -c \"$1\"";
        return new ProcessRunRequest(
            guard.FileName,
            [
                .. guard.ArgumentsPrefix,
                "exec",
                "-e",
                SessionVariable(),
                "-w",
                root,
                name,
                "sh",
                "-c",
                wrapper,
                "lunate-sh",
                command,
            ],
            root,
            Timeout: timeout,
            OutputCapBytes: OutputCap
        );
    }

    private static string SessionVariable() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{LunateHome.SessionVariable}={LunateHome.SessionMarker}"
        );

    private static string CanonicalRoot(string workspaceRoot)
    {
        string root = PathCanonical.Canonicalize(workspaceRoot);
        if (!Directory.Exists(root))
        {
            throw new ContainerEnvironmentException(
                $"the workspace '{workspaceRoot}' does not exist; run a command inside an existing workspace"
            );
        }

        if (root.Contains(':', StringComparison.Ordinal))
        {
            throw new ContainerEnvironmentException(
                $"the workspace path '{root}' cannot be mounted in a container because it contains ':', "
                    + "which the engine's volume syntax treats as a field separator; use a path without ':'"
            );
        }

        return root;
    }

    private static bool TryPgid(string line, out int pgid)
    {
        pgid = 0;
        return line.StartsWith(PgidPrefix, StringComparison.Ordinal)
            && int.TryParse(
                line.AsSpan(PgidPrefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out pgid
            )
            && pgid > 0;
    }

    private static ProcessRunResult StripPgidLine(ProcessRunResult result, int pgid)
    {
        if (pgid <= 0)
        {
            return result;
        }

        string marker = string.Create(CultureInfo.InvariantCulture, $"{PgidPrefix}{pgid}");
        string stdout = result.Stdout;
        if (!stdout.StartsWith(marker, StringComparison.Ordinal))
        {
            return result;
        }

        int newline = stdout.IndexOf('\n');
        return result with { Stdout = newline < 0 ? string.Empty : stdout[(newline + 1)..] };
    }

    private string Failure(string action, string root, ProcessRunResult result)
    {
        string detail = string.IsNullOrWhiteSpace(result.Stderr)
            ? string.Create(CultureInfo.InvariantCulture, $"exit code {result.ExitCode}")
            : result.Stderr.Trim();
        return $"the {_selection.Engine} engine could not {action} the session container for "
            + $"'{_selection.Image}' at '{root}': {detail}; check that the engine is running and the "
            + "image name is valid";
    }
}
