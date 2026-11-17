using Lunate.TestSupport;

namespace Lunate.Coding.Tests;

/// <summary>
/// The layout rule the 0.4 solution-layout change settled: a folder groups files by feature and never
/// adds a namespace segment, every feature folder of a source project is mirrored by its test
/// project, and a test folder always names a source folder. The scans themselves live in
/// <see cref="LayoutRule"/>, so the regression cases below can feed them a synthetic repository.
/// </summary>
public sealed class LayoutRuleTests
{
    [Fact]
    public void No_folder_adds_a_namespace_segment()
    {
        IReadOnlyList<string> violations = LayoutRule.NamespaceViolations(RepositoryRoot.Find());

        Assert.Empty(violations);
    }

    [Fact]
    public void Every_source_folder_has_a_mirrored_test_folder()
    {
        IReadOnlyList<string> violations = LayoutRule.MissingTestFolders(RepositoryRoot.Find());

        Assert.Empty(violations);
    }

    [Fact]
    public void Every_test_folder_mirrors_a_source_folder()
    {
        IReadOnlyList<string> violations = LayoutRule.FoldersWithoutSource(RepositoryRoot.Find());

        Assert.Empty(violations);
    }

    [Fact]
    public void The_namespace_scan_rejects_a_folder_segment_that_reappears()
    {
        using var repository = new TempDirectory();
        string project = Path.Combine(repository.Root, "src", "Lunate.Example");
        WriteProject(project);
        Write(Path.Combine(project, "Mcp", "Client", "Client.cs"), "namespace Lunate.Example.Mcp;");
        Write(Path.Combine(project, "Tools", "Read.cs"), "namespace Lunate.Example;");

        IReadOnlyList<string> violations = LayoutRule.NamespaceViolations(repository.Root);

        string violation = Assert.Single(violations);
        Assert.Contains("Mcp/Client/Client.cs", violation, StringComparison.Ordinal);
        Assert.Contains("declares Lunate.Example.Mcp", violation, StringComparison.Ordinal);
        Assert.Contains("the folder 'Mcp'", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void The_namespace_scan_checks_every_declaration_in_a_file()
    {
        using var repository = new TempDirectory();
        string project = Path.Combine(repository.Root, "src", "Lunate.Example");
        WriteProject(project);
        Write(
            Path.Combine(project, "Mcp", "Client.cs"),
            """
            namespace Lunate.Example
            {
                internal static class Outer { }
            }

            namespace Lunate.Example.Mcp
            {
                internal static class Client { }
            }
            """
        );

        IReadOnlyList<string> violations = LayoutRule.NamespaceViolations(repository.Root);

        string violation = Assert.Single(violations);
        Assert.Contains("Mcp/Client.cs", violation, StringComparison.Ordinal);
        Assert.Contains("declares Lunate.Example.Mcp", violation, StringComparison.Ordinal);
        Assert.Contains("the folder 'Mcp'", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void The_mirror_scan_rejects_a_source_folder_without_a_test_folder()
    {
        using var repository = new TempDirectory();
        string project = Path.Combine(repository.Root, "src", "Lunate.Example");
        string tests = Path.Combine(repository.Root, "tests", "Lunate.Example.Tests");
        WriteProject(project);
        WriteProject(tests);
        Write(Path.Combine(project, "Remote", "Runner.cs"), "namespace Lunate.Example;");
        Write(Path.Combine(project, "Host", "Host.cs"), "namespace Lunate.Example;");
        Write(Path.Combine(tests, "Host", "HostTests.cs"), "namespace Lunate.Example.Tests;");

        IReadOnlyList<string> violations = LayoutRule.MissingTestFolders(repository.Root);

        string violation = Assert.Single(violations);
        Assert.Contains("src/Lunate.Example/Remote", violation, StringComparison.Ordinal);
        Assert.DoesNotContain("Host", violation, StringComparison.Ordinal);
        Assert.Empty(LayoutRule.FoldersWithoutSource(repository.Root));
    }

    [Fact]
    public void The_mirror_scan_rejects_a_test_folder_without_a_source_folder()
    {
        using var repository = new TempDirectory();
        string project = Path.Combine(repository.Root, "src", "Lunate.Example");
        string tests = Path.Combine(repository.Root, "tests", "Lunate.Example.Tests");
        WriteProject(project);
        WriteProject(tests);
        Write(Path.Combine(project, "Host", "Host.cs"), "namespace Lunate.Example;");
        Write(Path.Combine(tests, "Host", "HostTests.cs"), "namespace Lunate.Example.Tests;");
        Write(Path.Combine(tests, "Remote", "RemoteTests.cs"), "namespace Lunate.Example.Tests;");

        IReadOnlyList<string> violations = LayoutRule.FoldersWithoutSource(repository.Root);

        string violation = Assert.Single(violations);
        Assert.Contains("tests/Lunate.Example.Tests/Remote", violation, StringComparison.Ordinal);
        Assert.DoesNotContain("Host", violation, StringComparison.Ordinal);
        Assert.Empty(LayoutRule.MissingTestFolders(repository.Root));
    }

    private static void WriteProject(string directory)
    {
        Directory.CreateDirectory(directory);
        Write(Path.Combine(directory, Path.GetFileName(directory) + ".csproj"), "<Project />");
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text + "\n");
    }
}
