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
/// The scan walks the whole repository, fixture projects included, so a fixture that declares a
/// second framework fails it like any other project. Only the directories that hold somebody else's
/// project or a record — an eval task under <c>eval/tasks</c>, a spike under <c>docs/dev/spikes</c> —
/// are data and stay out, together with build output. A test that needs a multi-target input writes
/// that shape into a temporary copy, so no project file in the tree declares one.
/// </remarks>
public sealed class TargetFrameworkTests
{
    private const string TargetFramework = "net11.0";

    private static readonly string[] DataDirectories =
    [
        Path.Combine("docs", "dev", "spikes"),
        Path.Combine("eval", "tasks"),
    ];

    [Fact]
    public void Every_project_targets_net11_without_multi_targeting()
    {
        string repository = RepositoryRoot.Find();
        string[] violations = [.. ProjectsIn(repository).SelectMany(ViolationsOf)];

        Assert.True(
            violations.Length == 0,
            FormattableString.Invariant(
                $"the repository targets {TargetFramework} only:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}"
            )
        );
    }

    [Fact]
    public void The_scan_reads_the_fixture_projects()
    {
        string repository = RepositoryRoot.Find();
        string fixtures = Path.Combine(repository, "tests", "fixtures");
        string[] projects = [.. ProjectsIn(repository)];

        Assert.Contains(projects, project =>
            project.StartsWith(fixtures + Path.DirectorySeparatorChar, StringComparison.Ordinal)
        );
    }

    [Fact]
    public void A_multi_targeting_fixture_fails_the_scan()
    {
        using var temp = new TempDirectory();
        string fixture = Path.Combine(temp.Root, "tests", "fixtures", "multi-target");
        Directory.CreateDirectory(fixture);
        File.WriteAllText(
            Path.Combine(fixture, "MultiTarget.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\">"
                + "<PropertyGroup><TargetFrameworks>net11.0;net11.0-windows</TargetFrameworks></PropertyGroup>"
                + "</Project>"
        );

        string[] violations = [.. ProjectsIn(temp.Root).SelectMany(ViolationsOf)];

        string violation = Assert.Single(violations);
        Assert.Contains("MultiTarget.csproj", violation, StringComparison.Ordinal);
        Assert.Contains("net11.0;net11.0-windows", violation, StringComparison.Ordinal);
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

    private static IEnumerable<string> ProjectsIn(string repository) =>
        Directory
            .EnumerateFiles(repository, "*.csproj", SearchOption.AllDirectories)
            .Where(project => !IsData(repository, project))
            .OrderBy(project => project, StringComparer.Ordinal);

    private static bool IsData(string repository, string project)
    {
        string relative = Path.GetRelativePath(repository, project);
        string[] segments = relative.Split(Path.DirectorySeparatorChar);
        return segments.Any(segment => segment is "bin" or "obj" or ".git" or "node_modules")
            || DataDirectories.Any(directory =>
                relative.StartsWith(
                    directory + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal
                )
            );
    }
}
