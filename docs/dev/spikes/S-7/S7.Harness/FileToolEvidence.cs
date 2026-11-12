using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Lunate.Agent;
using Lunate.Coding;

namespace S7;

/// <summary>
/// The file-tool evidence: the real <see cref="WriteTool"/> and <see cref="ReadTool"/> run on the
/// host, and the container reads and writes the same bytes through the bind mount. The question the
/// spike answers — "do file tools run on the host or in the container?" — is answered by measuring
/// both directions of that mount, not by reasoning about it.
/// </summary>
internal static class FileToolEvidence
{
    private const string Written = ".s7-evidence/host-write.txt";
    private const string WrittenByContainer = ".s7-evidence/container-write.txt";

    internal static async Task<int> RunAsync(SpikeOptions options, EvidenceWriter writer)
    {
        var engine = new Engine(options);
        Directory.CreateDirectory(options.EvidenceDirectory);

        var workspace = new Workspace(options.Workspace);
        var sink = new RecordingSink();
        ToolContext ctx = Tooling.Context(options, sink);

        // The content is deliberately not ASCII-only: the mount has to carry bytes, not a locale.
        const string content = "line one\nline two with ünïcode — and an em dash\n";

        writer.Section("host-side write, container-side read (the real write tool)");
        var writeTool = new WriteTool(workspace);
        var stopwatch = Stopwatch.StartNew();
        ToolResult written = await writeTool.ExecuteAsync(
            Tooling.Args(Written, content),
            ctx,
            CancellationToken.None
        );
        double writeMillis = stopwatch.Elapsed.TotalMilliseconds;
        writer.Line($"write tool result ({Num.Ms(writeMillis)} ms): {OneLine(written.Output)}");
        writer.Check("write tool succeeded", !written.IsError, $"isError={written.IsError}");

        string hostSha = HostSha(options.Full(Written));
        string containerSha = engine.Sha256InContainer(options.Container, Written);
        writer.Key("host sha256", hostSha);
        writer.Key("container sha256", containerSha);
        writer.Check(
            "the container sees the host-written bytes",
            hostSha == containerSha,
            $"{hostSha} == {containerSha}"
        );

        string readBack = engine.ExecText(
            options.Container,
            $"cat {Engine.Quote(Written)}; stat -c 'mode=%a uid=%u gid=%g size=%s' {Engine.Quote(Written)}"
        );
        writer.Line("container cat + stat:");
        writer.Line(readBack);

        writer.Section("container-side write, host-side read (the real read tool)");
        ProcResult containerWrite = engine.Exec(
            options.Container,
            $"printf 'written by the container\\nsecond line\\n' > {Engine.Quote(WrittenByContainer)}"
        );
        writer.Check(
            "the container wrote a file",
            containerWrite.Ok,
            $"exit={containerWrite.ExitCode} stderr={OneLine(containerWrite.Stderr)}"
        );

        var readTool = new ReadTool(workspace);
        stopwatch.Restart();
        ToolResult read = await readTool.ExecuteAsync(
            Tooling.Args(WrittenByContainer),
            ctx,
            CancellationToken.None
        );
        double readMillis = stopwatch.Elapsed.TotalMilliseconds;
        writer.Line($"read tool result ({Num.Ms(readMillis)} ms):");
        writer.Line(read.Output);
        writer.Check(
            "the host-side read tool sees the container-written file",
            !read.IsError
                && read.Output.Contains("written by the container", StringComparison.Ordinal),
            $"isError={read.IsError}"
        );

        writer.Section("the workspace boundary on the host (path spelling)");
        var containerPath = $"/workspace/{Written}";
        bool allowed = workspace.TryResolve(containerPath, out _, out string error);
        writer.Check(
            "a container-side path spelling is refused on the host",
            !allowed,
            allowed ? "resolved (unexpected)" : error
        );

        string hostPath = options.Full(Written);
        bool hostAllowed = workspace.TryResolve(hostPath, out _, out string hostError);
        writer.Check(
            "the host absolute path resolves",
            hostAllowed,
            hostAllowed ? hostPath : hostError
        );

        writer.Section("what the file-change sink saw");
        writer.Line($"sink: {sink.Describe()}");
        writer.Line(
            string.Create(
                CultureInfo.InvariantCulture,
                $"file tool cost on this host: write {Num.Ms(writeMillis)} ms, read {Num.Ms(readMillis)} ms"
            )
        );
        return writer.Finish();
    }

    private static string HostSha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    internal static string OneLine(string text)
    {
        string flattened = text.ReplaceLineEndings(" ").Trim();
        return flattened.Length <= 400 ? flattened : flattened[..400] + "…";
    }

    internal static string Invariant(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
