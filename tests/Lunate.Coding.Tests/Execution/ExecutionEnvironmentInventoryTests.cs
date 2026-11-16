using System.Text.RegularExpressions;
using Lunate.TestSupport;

namespace Lunate.Coding.Tests;

/// <summary>
/// The process-start inventory: every direct start under <c>src/</c> is classified as either a tool
/// command behind the execution environment or host support that stays local. A new start fails
/// this test until it is classified, which is the structural version of "no tool silently bypasses
/// the layer".
/// </summary>
public sealed class ExecutionEnvironmentInventoryTests
{
    /// <summary>
    /// Direct host/local process starts. Each is deliberately outside the tool-command seam: the
    /// platform batch spawn and interactive session (the local implementation), the process-table
    /// read behind the local tree kill, and the editor, clipboard and guard helper processes. The
    /// list names files, not paths: a feature folder moves a file without changing its classification
    /// (#695), and a name that is no longer unique fails at the read.
    /// </summary>
    private static readonly string[] HostSupportStarts =
    [
        "ClipboardWriter.cs",
        "DraftEditor.cs",
        "GuardProbe.cs",
        "LocalExecutionEnvironment.Session.cs",
        "ProcessRunner.cs",
        "WindowsChildProcess.cs",
    ];

    [Fact]
    public void Direct_process_starts_are_only_the_classified_host_support_sites()
    {
        string[] actual = SourceFiles()
            .Where(path => StartsAProcess(path))
            .Select(FileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(HostSupportStarts, actual);
    }

    [Fact]
    public void Tool_child_spawns_only_happen_inside_the_local_environment()
    {
        string[] actual = SourceFiles()
            .Where(path =>
                File.ReadLines(path)
                    .Any(line =>
                        !IsComment(line)
                        && Regex.IsMatch(line, @"\bChildProcess\.Start\s*\(", RegexOptions.None)
                    )
            )
            .Select(FileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["LocalExecutionEnvironment.cs"], actual);
    }

    [Fact]
    public void Tool_commands_name_the_seam_and_host_support_names_local()
    {
        string Read(string file) =>
            File.ReadAllText(RepositoryRoot.FindFile("src/Lunate.Coding", file));

        Assert.Contains("IExecutionEnvironment", Read("BashTool.cs"), StringComparison.Ordinal);
        Assert.Contains(
            "IExecutionEnvironment",
            Read("RipgrepGrepEngine.cs"),
            StringComparison.Ordinal
        );
        Assert.Contains("_environment", Read("BashTool.cs"), StringComparison.Ordinal);
        Assert.Contains("RunAsync", Read("BashTool.cs"), StringComparison.Ordinal);
        Assert.Contains("_environment", Read("RipgrepGrepEngine.cs"), StringComparison.Ordinal);
        Assert.Contains("RunAsync", Read("RipgrepGrepEngine.cs"), StringComparison.Ordinal);
        Assert.Contains(
            "_environment.RunAsync(request, cancellationToken)",
            Read("ProcessRunner.cs"),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "_environment.StartSessionAsync(request, cancellationToken)",
            Read("ProcessRunner.cs"),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "ProcessRunner.Local",
            Read("BashChangeTracker.cs"),
            StringComparison.Ordinal
        );
        Assert.Contains("ProcessRunner.Local", Read("ClipboardImage.cs"), StringComparison.Ordinal);
    }

    private static IEnumerable<string> SourceFiles()
    {
        string root = Path.Combine(RepositoryRoot.Find(), "src");
        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
                && !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal
                )
            );
    }

    private static bool StartsAProcess(string path) =>
        File.ReadLines(path)
            .Any(line =>
                !IsComment(line)
                && Regex.IsMatch(
                    line,
                    @"\bProcess\.Start\s*\(|new\s+ProcessStartInfo\s*\(",
                    RegexOptions.None
                )
            );

    private static bool IsComment(string line) =>
        line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    private static string FileName(string path) => Path.GetFileName(path);
}
