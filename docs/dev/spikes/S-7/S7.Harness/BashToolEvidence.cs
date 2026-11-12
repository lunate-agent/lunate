using System.Globalization;
using System.Text.RegularExpressions;
using Lunate.Agent;
using Lunate.Coding;

namespace S7;

/// <summary>
/// The bash-tool evidence: the unmodified <see cref="BashTool"/> runs its command in the container
/// through a host shim, so the guard wrap, the output cap, the timeout/tree kill and the change
/// scan all stay in the product path. What the spike measures is which of those still do what they
/// promise when the last hop is <c>exec</c> instead of a local shell.
/// </summary>
internal static partial class BashToolEvidence
{
    private const string ContainerWritten = ".s7-evidence/container-bash.txt";
    private const string ScanProbe = "s7-scan-probe.txt";

    internal static async Task<int> RunAsync(SpikeOptions options, EvidenceWriter writer)
    {
        var engine = new Engine(options);
        var shell = new ContainerShell(options);

        writer.Section("the shim the bash tool is pointed at");
        writer.Key("path", shell.Path);
        foreach (string line in shell.Write().ReplaceLineEndings("\n").Split('\n'))
        {
            writer.Line("  " + line);
        }

        var workspace = new Workspace(options.Workspace);
        // The change scan baselines on the first call, so this run's own probes must not exist yet:
        // a leftover from an earlier run would enter the baseline and read as "nothing changed".
        foreach (string probe in new[] { ContainerWritten, ScanProbe })
        {
            string path = options.Full(probe);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        var sink = new RecordingSink();
        ToolContext ctx = Tooling.Context(options, sink);
        var bash = new BashTool(workspace, shell.Resolver());

        writer.Section("where the command ran");
        ToolResult where = await bash.ExecuteAsync(
            Tooling.BashArgs(
                "echo \"kernel=$(uname -s) arch=$(uname -m) host=$(hostname)\"; "
                    + "pwd; echo \"uid=$(id -u)\"; echo \"session=${LUNATE_SESSION-unset}\""
            ),
            ctx,
            CancellationToken.None
        );
        writer.Line(where.Output);
        writer.Check(
            "the command ran on Linux, not on the host",
            where.Output.Contains("kernel=Linux", StringComparison.Ordinal),
            OneLine(where.Output)
        );
        writer.Check(
            "the working directory is the same absolute path on both sides",
            where.Output.Contains(options.Workspace, StringComparison.Ordinal),
            options.Workspace
        );
        writer.Line(
            "NOTE the session marker: LUNATE_SESSION is set by the tool on the host process it "
                + "spawns; exec does not forward it, so the container sees it unset."
        );

        writer.Section("exit codes and streams");
        ToolResult exitCode = await bash.ExecuteAsync(
            Tooling.BashArgs("echo out-line; echo err-line >&2; exit 7"),
            ctx,
            CancellationToken.None
        );
        writer.Line(exitCode.Output);
        writer.Check(
            "a non-zero container exit is the tool's exit code",
            exitCode.IsError && exitCode.Output.Contains("exit code: 7", StringComparison.Ordinal),
            $"isError={exitCode.IsError}"
        );

        writer.Section("a container write, seen by the host and by the change scan");
        writer.Key("git common dir", workspace.GitCommonDir ?? "(none: the scan would not run)");
        ToolResult containerBashWrite = await bash.ExecuteAsync(
            Tooling.BashArgs(
                $"mkdir -p .s7-evidence && printf 'bash-in-container\\n' > {ContainerWritten} "
                    + $"&& printf 'the scan should see this\\n' > {ScanProbe}"
            ),
            ctx,
            CancellationToken.None
        );
        writer.Line(OneLine(containerBashWrite.Output));
        bool onHost = File.Exists(options.Full(ContainerWritten));
        writer.Check(
            "the host sees the file the container's bash wrote",
            onHost,
            onHost ? File.ReadAllText(options.Full(ContainerWritten)).Trim() : "missing"
        );
        writer.Key("change sink", sink.Describe());
        writer.Line(
            "NOTE the scan sees `s7-scan-probe.txt` (a new file at the workspace root) but not "
                + $"`{ContainerWritten}`: `git status -unormal` collapses an untracked directory "
                + "into one entry, so a file created inside an already-untracked directory is "
                + "invisible to the bash change scan. That is host-side git behaviour and is the "
                + "same with or without a container."
        );
        writer.Check(
            "the host-side change scan publishes a container-written file",
            sink.Changes.Any(change =>
                change.RelativePath.EndsWith(ScanProbe, StringComparison.Ordinal)
                && change.Source == "bash"
            ),
            sink.Describe()
        );

        writer.Section("cancel: does the tool's process tree kill reach the container?");
        // A distinctive duration, so the container's own keep-alive `sleep 3600` never reads as a
        // survivor of the cancelled command.
        const string sleepCommand = "echo \"pgid=$(ps -o pgid= -p $$ | tr -d ' ')\"; sleep 4271";
        const string pgrepSurvivors = "pgrep -ax sleep | grep ' sleep 4271$' || true";
        var shortTimeout = new BashTool(workspace, shell.Resolver(), TimeSpan.FromSeconds(2));
        ToolResult timedOut = await shortTimeout.ExecuteAsync(
            Tooling.BashArgs(sleepCommand),
            ctx,
            CancellationToken.None
        );
        writer.Line(timedOut.Output);
        writer.Check(
            "the tool reports the timeout",
            timedOut.IsError && timedOut.Output.Contains("timed out", StringComparison.Ordinal),
            OneLine(timedOut.Output)
        );

        string survivors = engine.ExecText(options.Container, pgrepSurvivors);
        writer.Key(
            "container processes after the host-side tree kill",
            survivors.Length == 0 ? "(none)" : survivors
        );
        string? pgid = Pgid().Match(timedOut.Output) is { Success: true } match
            ? match.Groups["pgid"].Value
            : null;
        writer.Key("container-side process group", pgid ?? "(not in the tool output)");

        if (pgid is not null)
        {
            engine.ExecText(options.Container, $"bash -c 'kill -TERM -- -{pgid}' || true");
        }
        else
        {
            engine.ExecText(options.Container, "pkill -f 'sleep 4271' || true");
        }

        string after = engine.ExecText(options.Container, pgrepSurvivors);
        writer.Check(
            "the container-side process group dies when the runner kills it by pgid",
            after.Length == 0,
            after.Length == 0 ? "(none left)" : after
        );
        writer.Check(
            "the pgid to kill is reportable from the tool's own output",
            pgid is not null,
            pgid ?? "the command's first line did not reach the result"
        );

        writer.Section("the bash guard with a container environment");
        string stateRoot = Path.Combine(Path.GetTempPath(), "s7-lunate-home");
        Environment.SetEnvironmentVariable("LUNATE_HOME", stateRoot);
        var guarded = new BashTool(workspace, shell.Resolver());
        ToolResult refused = await guarded.ExecuteAsync(
            Tooling.BashArgs(
                $"mkdir -p {Engine.Quote(stateRoot)} && "
                    + $"echo probe > {Engine.Quote(Path.Combine(stateRoot, "probe.txt"))}"
            ),
            ctx,
            CancellationToken.None
        );
        writer.Line(refused.Output);
        writer.Key("refused before spawn", refused.IsError ? "yes" : "no");
        bool presentInside = engine
            .ExecText(
                options.Container,
                $"test -e {Engine.Quote(stateRoot)} && echo present || echo absent"
            )
            .Contains("present", StringComparison.Ordinal);
        writer.Key(
            "is the guard's state root reachable inside the container",
            presentInside ? "yes" : "no"
        );
        writer.Check(
            "in a container the guard's protection is structural: the state root is not mounted",
            !presentInside,
            $"{stateRoot} inside {options.Container}"
        );
        Environment.SetEnvironmentVariable("LUNATE_HOME", null);

        ResolvedShell? resolved = shell.Resolver().Resolve();
        writer.Section("what the resolver made of the shim");
        writer.Key(
            "resolved",
            resolved is null
                ? "none"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{resolved.DisplayName} ({resolved.Kind}): {resolved.ExecutablePath} {string.Join(' ', resolved.ArgumentsPrefix)}"
                )
        );
        return writer.Finish();
    }

    private static string OneLine(string text) => FileToolEvidence.OneLine(text);

    [GeneratedRegex(@"pgid=(?<pgid>\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex Pgid();
}
