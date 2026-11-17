using System.Text.RegularExpressions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The layout rule the 0.4 solution-layout change settled, as a scan: a folder groups files by
/// feature and never adds a namespace segment, and a test project mirrors every feature folder of
/// the project it tests. The scans read text and walk directories, like
/// <see cref="LayeringChecker"/>, so a synthetic repository can be fed to them and the rule itself
/// stays testable.
/// </summary>
internal static partial class LayoutRule
{
    /// <summary>
    /// Every <c>.cs</c> file under <c>src/</c> whose declared namespace repeats a folder segment of
    /// its own path, related to its project directory. The namespace is the assembly's, so a folder
    /// may not claim it — neither as the trailing segment nor anywhere else in the name.
    /// </summary>
    internal static IReadOnlyList<string> NamespaceViolations(string root)
    {
        string source = Path.Combine(root, "src");
        if (!Directory.Exists(source))
        {
            return [];
        }

        return
        [
            .. Directory
                .GetFiles(source, "*.cs", SearchOption.AllDirectories)
                .Where(file => !IsGenerated(file))
                .SelectMany(file => FolderNamespaceViolations(root, file))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Every feature folder of a <c>src/</c> project that has no folder of the same relative path
    /// under its <c>tests/</c> project. A project without a test project is not covered by the
    /// mirror rule.
    /// </summary>
    internal static IReadOnlyList<string> MissingTestFolders(string root)
    {
        List<string> violations = [];
        foreach (string project in Projects(root, "src"))
        {
            string name = Path.GetFileName(project);
            string tests = Path.Combine(root, "tests", name + ".Tests");
            if (!Directory.Exists(tests))
            {
                continue;
            }

            foreach (string folder in Folders(project))
            {
                if (!Directory.Exists(Path.Combine(tests, folder)))
                {
                    violations.Add(
                        $"{Relative(root, Path.Combine(project, folder))} has no mirror under {Relative(root, tests)}"
                    );
                }
            }
        }

        return [.. violations.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Every folder under a <c>tests/</c> project that no folder of the same relative path holds
    /// under its <c>src/</c> project.
    /// </summary>
    internal static IReadOnlyList<string> FoldersWithoutSource(string root)
    {
        List<string> violations = [];
        foreach (string tests in Projects(root, "tests"))
        {
            string name = Path.GetFileName(tests);
            if (!name.EndsWith(".Tests", StringComparison.Ordinal))
            {
                continue;
            }

            string source = Path.Combine(root, "src", name[..^".Tests".Length]);
            if (!Directory.Exists(source))
            {
                continue;
            }

            foreach (string folder in Folders(tests))
            {
                if (!Directory.Exists(Path.Combine(source, folder)))
                {
                    violations.Add(
                        $"{Relative(root, Path.Combine(tests, folder))} has no mirror under {Relative(root, source)}"
                    );
                }
            }
        }

        return [.. violations.Order(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> FolderNamespaceViolations(string root, string file)
    {
        IReadOnlyList<string> namespaces = DeclaredNamespaces(file);
        if (namespaces.Count == 0)
        {
            yield break;
        }

        string project = ProjectDirectory(file);
        string folder = Path.GetRelativePath(project, Path.GetDirectoryName(file)!);
        if (folder == ".")
        {
            yield break;
        }

        string[] folderSegments = folder.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries
        );
        foreach (string ns in namespaces)
        {
            string[] segments = ns.Split('.');
            foreach (string segment in folderSegments)
            {
                if (segments.Contains(segment, StringComparer.Ordinal))
                {
                    yield return $"{Relative(root, file)} declares {ns}; the folder '{segment}' must not be a namespace segment";
                }
            }
        }
    }

    private static IReadOnlyList<string> DeclaredNamespaces(string file)
    {
        List<string> namespaces = [];
        using StreamReader reader = File.OpenText(file);
        while (reader.ReadLine() is { } line)
        {
            Match match = NamespacePattern().Match(line);
            if (match.Success)
            {
                namespaces.Add(match.Groups[1].Value);
            }
        }

        return namespaces;
    }

    private static IEnumerable<string> Projects(string root, string directory)
    {
        string path = Path.Combine(root, directory);
        if (!Directory.Exists(path))
        {
            return [];
        }

        return Directory
            .GetFiles(path, "*.csproj", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file))
            .Select(file => Path.GetDirectoryName(file)!);
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

    internal static bool IsGenerated(string path) =>
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
