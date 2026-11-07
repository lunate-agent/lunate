using Lunate.TestSupport;

namespace Lunate.Protocols.Tests;

/// <summary>
/// #472 — the allow-list scrubber builds the environment of Lunate's managed extension hosts, and
/// the stdio host is the only one of those spawns that may turn inheritance on. The test greps
/// Lunate's source the way a reviewer would: every file that touches child-environment plumbing is
/// one of the two named builders below and neither names a second allow-list, inheritance stays
/// off, and the SDK's own curated allow-list is not adopted behind our backs.
/// </summary>
public sealed class ChildEnvironmentGuardTests
{
    /// <summary>
    /// The scrubbed builder: the extension hosts' environment comes out of
    /// <c>EnvironmentScrubber.Build</c>, minimal base plus declared names, never the parent's
    /// whole environment.
    /// </summary>
    private const string ScrubbedBuilderFile = "McpServerHost.cs";

    /// <summary>
    /// The inherited builder: the tool child. <see cref="ProcessRunRequest"/>'s public contract is
    /// that withholding an explicit environment leaves the child the parent's environment — the
    /// bash tool is the user's shell and must see the user's environment (a scrubbed allow-list
    /// would half-break every user toolchain that keeps state outside the scrubber's base list,
    /// on every machine). The class comment on <c>ChildProcess.cs</c> names
    /// <c>EnvironmentScrubber</c> so this file and the guard cannot be decoupled by accident.
    /// </summary>
    private const string InheritedBuilderFile = "ChildProcess.cs";

    [Fact]
    public void The_child_environment_builders_are_pinned_and_the_scrubbed_one_names_the_scrubber()
    {
        (string Path, string Content)[] plumbing = CSharpSources()
            .Where(source =>
                source.Content.Contains("EnvironmentVariables", StringComparison.Ordinal)
            )
            .ToArray();

        Assert.NotEmpty(plumbing);
        Assert.Equal(
            new[] { InheritedBuilderFile, ScrubbedBuilderFile },
            plumbing.Select(source => Path.GetFileName(source.Path)).Order().ToArray()
        );

        string scrubbed = plumbing.Single(source =>
            Path.GetFileName(source.Path) == ScrubbedBuilderFile
        ).Content;

        // The formatter may split the call across lines, so match the member path, not the line.
        Assert.Matches(@"EnvironmentScrubber\s*\.\s*Build", scrubbed);

        // The inherited builder names the scrubber in its own contract comment, so the boundary
        // between the two builders survives a refactor that edits either file in isolation.
        string inherited = plumbing.Single(source =>
            Path.GetFileName(source.Path) == InheritedBuilderFile
        ).Content;
        Assert.Contains("EnvironmentScrubber", inherited, StringComparison.Ordinal);
        Assert.Contains("inherits the caller's environment", inherited, StringComparison.Ordinal);
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
