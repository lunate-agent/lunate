using System.Globalization;
using System.Text;

namespace S7;

/// <summary>
/// The container CLI as the spike drives it: the argv shape a container tool runner would have to
/// produce. Nothing here is product code — that is the point of the evidence: these are the exact
/// calls #627 has to make from inside the tool loop.
/// </summary>
internal sealed class Engine(SpikeOptions options)
{
    internal SpikeOptions Options => options;

    /// <summary>Whether the CLI exists on PATH at all.</summary>
    internal bool Available() => Proc.Run("which", options.Engine).Ok;

    internal ProcResult Version() => Proc.Run(options.Engine, "version");

    internal ProcResult Inspect(string container) =>
        Proc.Run(
            options.Engine,
            "inspect",
            container,
            "--format",
            "{{.State.Running}} {{json .Mounts}} {{json .Config.WorkingDir}}"
        );

    internal ProcResult Images() =>
        Proc.Run(
            options.Engine,
            "images",
            "--format",
            "{{.Repository}}:{{.Tag}} {{.Size}} {{.ID}}"
        );

    internal ProcResult MachineList() =>
        Proc.Run(
            options.Engine,
            "machine",
            "list",
            "--format",
            "{{.Name}} {{.VMType}} {{.CPUs}}cpus {{.Memory}} {{.DiskSize}}"
        );

    /// <summary>Whether the named container is running right now.</summary>
    internal bool Running(string container)
    {
        ProcResult result = Proc.Run(
            options.Engine,
            "ps",
            "--filter",
            $"name=^{container}$",
            "--format",
            "{{.Names}}"
        );
        return result.Ok && result.StdoutTrimmed.Split('\n').Contains(container);
    }

    /// <summary>
    /// The workhorse: one command inside the running container, in the workspace directory, with
    /// stdin closed — the same three fields <c>BashTool</c> hands to the process runner (file name,
    /// argument prefix, working directory), only pointed at the container instead of the host.
    /// </summary>
    internal ProcResult Exec(string container, string command, string? workingDirectory = null) =>
        Proc.Run(
            options.Engine,
            [
                "exec",
                "-i",
                "-w",
                workingDirectory ?? options.Workspace,
                container,
                "/bin/bash",
                "-c",
                command,
            ],
            workingDirectory: options.Workspace,
            timeout: TimeSpan.FromSeconds(120)
        );

    /// <summary>Runs a command in the container and keeps stdout only, trimmed.</summary>
    internal string ExecText(string container, string command)
    {
        ProcResult result = Exec(container, command);
        return result.StdoutTrimmed;
    }

    /// <summary>The container's sha256 of one workspace file — the byte-for-byte comparison.</summary>
    internal string Sha256InContainer(string container, string relativePath)
    {
        ProcResult result = Exec(container, $"sha256sum {Quote(relativePath)}");
        if (!result.Ok || result.StdoutTrimmed.Length == 0)
        {
            return $"<sha256sum failed: exit {result.ExitCode} {result.StderrTrimmed()}>";
        }

        return result.StdoutTrimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    /// <summary>Single-quote a path for the container's shell.</summary>
    internal static string Quote(string value)
    {
        var builder = new StringBuilder("'");
        foreach (char character in value)
        {
            if (character == '\'')
            {
                builder.Append("'\\''");
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.Append('\'').ToString();
    }

    internal static string Invariant(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}

internal static class ProcResultText
{
    internal static string StderrTrimmed(this ProcResult result) => result.Stderr.TrimEnd();
}
