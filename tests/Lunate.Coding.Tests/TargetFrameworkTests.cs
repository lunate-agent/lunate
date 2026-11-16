using System.Xml.Linq;
using Lunate.TestSupport;

namespace Lunate.Coding.Tests;

/// <summary>
/// The repository targets one framework (ADR-0041): every project it builds is <c>net11.0</c>, and
/// no project multi-targets. Nothing needs a <c>net10.0</c> asset — there are no external extension
/// authors yet, and a multi-target clause returns only as a deliberate, breaking decision before the
/// first published library version.
/// </summary>
/// <remarks>
/// The scan covers the folders that hold this repository's projects. A fixture under
/// <c>tests/fixtures</c> models somebody else's repository and is data, so it is skipped; that is
/// also why the multi-target fixture may keep its shape.
/// </remarks>
public sealed class TargetFrameworkTests
{
    private const string TargetFramework = "net11.0";

    private static readonly string[] ProjectRoots =
    [
        "src",
        "tests",
        "extensions",
        "samples",
        "templates",
    ];

    [Fact]
    public void Every_project_targets_net11_without_multi_targeting()
    {
        string repository = RepositoryRoot.Find();
        string[] violations = [.. ReadProjects(repository).SelectMany(ViolationsOf)];

        Assert.True(
            violations.Length == 0,
            FormattableString.Invariant(
                $"the repository targets {TargetFramework} only:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}"
            )
        );
    }

    [Fact]
    public void The_build_property_file_hands_net11_to_every_project()
    {
        string repository = RepositoryRoot.Find();
        XDocument props = XDocument.Load(Path.Combine(repository, "Directory.Build.props"));
        string? declared = props.Descendants("TargetFramework").FirstOrDefault()?.Value;

        Assert.Equal(TargetFramework, declared);
        Assert.Empty(props.Descendants("TargetFrameworks"));
    }

    private static IEnumerable<string> ViolationsOf(string project)
    {
        XDocument document = XDocument.Load(project);
        foreach (XElement frameworks in document.Descendants("TargetFrameworks"))
        {
            yield return $"{project}: multi-targets '{frameworks.Value}'";
        }

        foreach (XElement framework in document.Descendants("TargetFramework"))
        {
            if (!string.Equals(framework.Value.Trim(), TargetFramework, StringComparison.Ordinal))
            {
                yield return $"{project}: targets '{framework.Value.Trim()}'";
            }
        }
    }

    private static IEnumerable<string> ReadProjects(string repository) =>
        ProjectRoots
            .SelectMany(root =>
                Directory.EnumerateFiles(
                    Path.Combine(repository, root),
                    "*.csproj",
                    SearchOption.AllDirectories
                )
            )
            .Where(project => !IsData(repository, project))
            .OrderBy(project => project, StringComparer.Ordinal);

    private static bool IsData(string repository, string project)
    {
        string relative = Path.GetRelativePath(repository, project);
        string[] segments = relative.Split(Path.DirectorySeparatorChar);
        return segments.Any(segment => segment is "bin" or "obj")
            || relative.StartsWith(Path.Combine("tests", "fixtures"), StringComparison.Ordinal);
    }
}
