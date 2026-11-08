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
        string sourceRoot = Path.Combine(repositoryRoot, "src");
        Dictionary<string, string[]> declarations = ReadDeclarations(sourceRoot);

        // A wrong root would make this test pass by scanning nothing.
        Assert.True(
            declarations.Count > 400,
            string.Format(
                CultureInfo.InvariantCulture,
                "expected the src/ tree under '{0}', found {1} files",
                sourceRoot,
                declarations.Count
            )
        );

        Dictionary<string, string> owners = [];
        foreach ((string path, string[] names) in declarations)
        {
            foreach (string name in names)
            {
                owners.TryAdd(name, path);
            }
        }

        var misnamed = new List<string>();
        foreach ((string path, string[] names) in declarations)
        {
            string leading = Path.GetFileNameWithoutExtension(path).Split('.')[0];
            if (
                names.Contains(leading, StringComparer.Ordinal)
                || !owners.TryGetValue(leading, out var owner)
            )
            {
                continue;
            }

            misnamed.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is named after '{1}', which {2} declares",
                    Relative(repositoryRoot, path),
                    leading,
                    Relative(repositoryRoot, owner)
                )
            );
        }

        Assert.Empty(misnamed);
    }

    private static Dictionary<string, string[]> ReadDeclarations(string sourceRoot)
    {
        var declarations = new Dictionary<string, string[]>(StringComparer.Ordinal);
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

            string text = File.ReadAllText(path);
            declarations[path] = TopLevelDeclaration
                .Matches(text)
                .Select(match => match.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return declarations;
    }

    /// <summary>Repository-relative and slash-separated, so one message reads the same on both hosts.</summary>
    private static string Relative(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
}
