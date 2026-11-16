using System.Text.RegularExpressions;
using Lunate.TestSupport;

namespace Lunate.Coding.Tests;

/// <summary>
/// The layout rule the 0.4 solution-layout change settled: a folder groups files by feature and never
/// adds or changes a namespace segment, and a test project mirrors the folders of the project it
/// tests. The scan reads text, like <see cref="TargetFrameworkTests"/> and the source-file-names
/// rule, so it needs no Roslyn reference and stays fast.
/// </summary>
public sealed partial class LayoutRuleTests
{
    [Fact]
    public void No_folder_adds_a_namespace_segment()
    {
        string root = RepositoryRoot.Find();

        string[] violations = Directory
            .GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file))
            .SelectMany(file => FolderNamespaceViolations(root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Every_test_folder_mirrors_a_source_folder()
    {
        string root = RepositoryRoot.Find();

        string[] violations = Directory
            .GetDirectories(Path.Combine(root, "tests"))
            .Select(project =>
                (
                    Source: Path.Combine(root, "src", Path.GetFileNameWithoutExtension(project)),
                    Tests: project
                )
            )
            .Where(pair => Directory.Exists(pair.Source))
            .SelectMany(pair =>
                Folders(pair.Tests)
                    .Where(folder => !Directory.Exists(Path.Combine(pair.Source, folder)))
                    .Select(folder =>
                        $"{Relative(root, Path.Combine(pair.Tests, folder))} has no mirror under {Relative(root, pair.Source)}"
                    )
            )
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> FolderNamespaceViolations(string root, string file)
    {
        using StreamReader reader = File.OpenText(file);
        while (reader.ReadLine() is { } line)
        {
            Match match = NamespacePattern().Match(line);
            if (!match.Success)
            {
                continue;
            }

            string project = ProjectDirectory(file);
            string folder = Path.GetRelativePath(project, Path.GetDirectoryName(file)!);
            if (folder == ".")
            {
                yield break;
            }

            foreach (
                string segment in folder.Split(
                    Path.DirectorySeparatorChar,
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                string ns = match.Groups[1].Value;
                if (ns.EndsWith($".{segment}", StringComparison.Ordinal))
                {
                    yield return $"{Relative(root, file)} declares {ns}; the folder '{segment}' must not add a namespace segment";
                }
            }

            yield break;
        }
    }

    private static string ProjectDirectory(string file)
    {
        for (
            DirectoryInfo? directory = new(Path.GetDirectoryName(file)!);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (directory.GetFiles("*.csproj").Length > 0)
            {
                return directory.FullName;
            }
        }

        return Path.GetDirectoryName(file)!;
    }

    private static IEnumerable<string> Folders(string project) =>
        Directory
            .GetDirectories(project, "*", SearchOption.AllDirectories)
            .Select(directory => Path.GetRelativePath(project, directory))
            .Where(folder => !IsGenerated(folder))
            .Where(folder =>
                !folder
                    .Split(Path.DirectorySeparatorChar)
                    .Any(segment =>
                        string.Equals(segment, "fixtures", StringComparison.OrdinalIgnoreCase)
                    )
            );

    private static bool IsGenerated(string path) =>
        path.Split(Path.DirectorySeparatorChar)
            .Any(segment =>
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)
            );

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    [GeneratedRegex(@"^\s*namespace\s+([A-Za-z_][\w.]*)", RegexOptions.CultureInvariant)]
    private static partial Regex NamespacePattern();
}
