using Lunate.TestSupport;

namespace Lunate.Protocols.Tests;

/// <summary>
/// #472 — the allow-list scrubber is the only code path that builds a child environment, and the
/// stdio host is the only one of Lunate's spawns that may turn inheritance on. The test greps
/// Lunate's source the way a reviewer would: every file that touches child-environment plumbing
/// must also reference <c>EnvironmentScrubber</c>, inheritance stays off, and the SDK's own
/// curated allow-list is not adopted behind our backs.
/// </summary>
public sealed class ChildEnvironmentGuardTests
{
    /// <summary>
    /// The one source file allowed to build a child environment in Lunate's own code: the stdio
    /// host. The scrubber itself is checked by its own test class; a new spawn site must move
    /// this pin behind a review, never appear beside it.
    /// </summary>
    private const string BuilderFile = "McpServerHost.cs";

    [Fact]
    public void The_stdio_host_is_the_only_child_environment_builder_and_names_the_scrubber()
    {
        (string Path, string Content)[] plumbing = CSharpSources()
            .Where(source =>
                source.Content.Contains("EnvironmentVariables", StringComparison.Ordinal)
            )
            .ToArray();

        Assert.NotEmpty(plumbing);
        Assert.Equal(
            new[] { BuilderFile },
            plumbing.Select(source => Path.GetFileName(source.Path)).ToArray()
        );

        Assert.Contains("EnvironmentScrubber.Build", plumbing[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Inheritance_stays_off_for_every_child_spawn()
    {
        foreach (
            (string path, string content) in CSharpSources()
                .Where(source =>
                    source.Content.Contains("InheritEnvironmentVariables", StringComparison.Ordinal)
                )
        )
        {
            Assert.Equal("McpServerHost.cs", Path.GetFileName(path));
            Assert.DoesNotContain(
                "InheritEnvironmentVariables = true",
                content,
                StringComparison.Ordinal
            );
            Assert.Contains(
                "InheritEnvironmentVariables = false",
                content,
                StringComparison.Ordinal
            );
        }
    }

    [Fact]
    public void The_sdk_own_curated_allow_list_is_not_adopted()
    {
        // ModelContextProtocol's GetDefaultEnvironmentVariables would fork the scrubber into a
        // second implementation; the one shared list is the contract (#472).
        Assert.Empty(
            CSharpSources()
                .Where(source =>
                    source.Content.Contains(
                        "GetDefaultEnvironmentVariables",
                        StringComparison.Ordinal
                    )
                )
                .Select(source => source.Path)
        );
    }

    /// <summary>Lunate's C# sources under <c>src</c> with repository-relative paths and contents.</summary>
    private static IReadOnlyList<(string Path, string Content)> CSharpSources()
    {
        string root = Path.Combine(RepositoryRoot.Find(), "src");
        char separator = Path.DirectorySeparatorChar;
        return
        [
            .. Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                    path.Contains($"{separator}obj{separator}") is false
                    && path.Contains($"{separator}bin{separator}") is false
                )
                .Select(path =>
                    (
                        $"src/{path[root.Length..].TrimStart(separator).Replace('\\', '/')}",
                        File.ReadAllText(path)
                    )
                ),
        ];
    }
}
