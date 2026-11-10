using System.Globalization;
using System.Text.RegularExpressions;
using Lunate.TestSupport;

namespace Lunate.Agent.Tests;

/// <summary>
/// Source file names (review finding N-3): a file under <c>src/</c> is named after a type it
/// declares, or after no type at all. The text before the first dot of the base name decides, so a
/// partial-class file such as <c>AgentHarness.Retries.cs</c> is judged by <c>AgentHarness</c>, and an
/// aggregate file such as <c>HookPayloads.cs</c> is judged by a name no type claims. A file named
/// after a type another file owns is what the finding caught: <c>Lunate.Agent/IFileChangeSink.cs</c>
/// held <c>RunFileChangeSink</c> while the contract lives in <c>Lunate.Contracts</c>.
/// </summary>
public sealed class SourceFileNameTests
{
    /// <summary>
    /// A top-level declaration, which every file in <c>src/</c> writes at column 0 (file-scoped
    /// namespaces, no indented namespace block). Nested types are indented, so they never match.
    /// </summary>
    private static readonly Regex TopLevelDeclaration = new(
        @"^(?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|file|new|required)\s+)*(?:class|struct|interface|enum|record(?:\s+(?:class|struct))?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline
    );

    [Fact]
    public void Every_source_file_is_named_after_a_type_it_declares()
    {
        string repositoryRoot = RepositoryRoot.Find();
        SourceFile[] files = ReadSourceFiles(Path.Combine(repositoryRoot, "src"));

        // A wrong root would make this test pass by scanning nothing.
        Assert.True(
            files.Length > 400,
            string.Format(
                CultureInfo.InvariantCulture,
                "expected the src/ tree under '{0}', found {1} files",
                repositoryRoot,
                files.Length
            )
        );

        Assert.Empty(Misnamed(files));
    }

    [Fact]
    public void A_file_named_after_another_files_type_is_reported()
    {
        SourceFile[] files =
        [
            new(
                "Lunate.Agent/IFileChangeSink.cs",
                "namespace A;\ninternal sealed class RunFileChangeSink { }"
            ),
            new(
                "Lunate.Contracts/IFileChangeSink.cs",
                "namespace B;\npublic interface IFileChangeSink { }"
            ),
        ];

        Assert.Equal(
            [
                "Lunate.Agent/IFileChangeSink.cs is named after 'IFileChangeSink', which Lunate.Contracts/IFileChangeSink.cs declares",
            ],
            Misnamed(files)
        );
    }

    [Fact]
    public void A_partial_class_file_is_named_after_the_type_it_declares()
    {
        SourceFile[] files =
        [
            new(
                "AgentHarness.Retries.cs",
                "namespace A;\npublic sealed partial class AgentHarness { }"
            ),
            new("AgentHarness.cs", "namespace A;\npublic sealed partial class AgentHarness { }"),
        ];

        Assert.Empty(Misnamed(files));
    }

    [Fact]
    public void An_aggregate_file_names_no_type_and_passes()
    {
        SourceFile[] files =
        [
            new(
                "HookPayloads.cs",
                "namespace A;\npublic sealed record TurnEndedPayload { }\npublic sealed record CompactingPayload { }"
            ),
            new("TurnEndedPayload.cs", "namespace A;\npublic sealed record TurnEndedPayload { }"),
        ];

        Assert.Empty(Misnamed(files));
    }

    [Fact]
    public void A_delegate_only_file_names_no_type_and_passes()
    {
        SourceFile[] files =
        [
            new(
                "GuardProbe.cs",
                "namespace A;\ninternal delegate (int ExitCode, string Output) GuardRun(string name);"
            ),
        ];

        Assert.Empty(Misnamed(files));
    }

    /// <summary>
    /// The rule: a file's leading base-name segment must be a type it declares, or a name no type in
    /// the set claims. Returns one message per offending file, naming both files involved.
    /// </summary>
    private static string[] Misnamed(IReadOnlyList<SourceFile> files)
    {
        Dictionary<string, string> owners = new(StringComparer.Ordinal);
        foreach (SourceFile file in files)
        {
            foreach (string name in Declared(file.Text))
            {
                owners.TryAdd(name, file.Path);
            }
        }

        var misnamed = new List<string>();
        foreach (SourceFile file in files)
        {
            string leading = Path.GetFileNameWithoutExtension(file.Path).Split('.')[0];
            string[] declared = Declared(file.Text);
            if (
                declared.Contains(leading, StringComparer.Ordinal)
                || !owners.TryGetValue(leading, out string? owner)
            )
            {
                continue;
            }

            misnamed.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is named after '{1}', which {2} declares",
                    file.Path,
                    leading,
                    owner
                )
            );
        }

        return [.. misnamed];
    }

    private static string[] Declared(string text) =>
        [
            .. TopLevelDeclaration
                .Matches(text)
                .Select(match => match.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal),
        ];

    /// <summary>The <c>src/</c> tree, as repository-relative paths so one message reads the same on both hosts.</summary>
    private static SourceFile[] ReadSourceFiles(string sourceRoot)
    {
        var files = new List<SourceFile>();
        foreach (
            string path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
        )
        {
            string relative = Path.GetRelativePath(sourceRoot, path);
            if (
                relative
                    .Split(Path.DirectorySeparatorChar)
                    .Any(segment => segment is "obj" or "bin")
            )
            {
                continue;
            }

            files.Add(new SourceFile(relative.Replace('\\', '/'), File.ReadAllText(path)));
        }

        return [.. files];
    }

    private sealed record SourceFile(string Path, string Text);
}
