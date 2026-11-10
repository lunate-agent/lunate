using Lunate.TestSupport;

/// <summary>
/// #472 and the one-builder ruling — Lunate has exactly one file that builds a child environment,
/// <c>EnvironmentScrubber</c>, with two named policies: <c>Scrubbed</c> for the extension hosts
/// (minimal base set, no inheritance) and <c>Inherited</c> for the tool child (the caller's
/// environment minus Lunate's own credentials). The test greps Lunate's source the way a reviewer
/// would: no other file reads the parent environment into a child set, every spawn site names a
/// policy at the call, inheritance stays off, and the SDK's own curated allow-list is not adopted
/// behind our backs.
/// </summary>
public sealed class ChildEnvironmentGuardTests
{
    /// <summary>
    /// The one source file allowed to build a child environment: the shared builder. A new spawn
    /// site must name a policy and come through it, never grow a builder of its own.
    /// </summary>
    private const string BuilderFile = "EnvironmentScrubber.cs";

    /// <summary>What naming a policy looks like at a call site (the formatter may split lines).</summary>
    private const string PolicyMarker = "ChildEnvironmentPolicy.";

    [Fact]
    public void The_shared_builder_is_the_only_child_environment_builder()
    {
        (string Path, string Content)[] plumbing = CSharpSources()
            .Where(source =>
                source.Content.Contains("GetEnvironmentVariables", StringComparison.Ordinal)
            )
            .ToArray();

        Assert.NotEmpty(plumbing);
        Assert.Equal(
            new[] { BuilderFile },
            plumbing.Select(source => Path.GetFileName(source.Path)).Order().ToArray()
        );

        string builder = plumbing[0].Content;
        Assert.Contains(PolicyMarker + "Scrubbed", builder, StringComparison.Ordinal);
        Assert.Contains(PolicyMarker + "Inherited", builder, StringComparison.Ordinal);
    }

    [Fact]
    public void The_tool_pass_through_list_is_read_where_the_policy_is_built()
    {
        // A pass-through list is a policy decision, like the strip list: it is read in the one
        // method that names the tool child's policy, never at a spawn site. A spawn site that read
        // it would decide a child's environment of its own.
        (string Path, string Content)[] readers = CSharpSources()
            .Where(source =>
                source.Content.Contains(
                    "ToolEnvironmentPassthrough.Names",
                    StringComparison.Ordinal
                )
            )
            .ToArray();

        Assert.Equal(
            new[] { "ChildProcess.cs" },
            readers.Select(source => Path.GetFileName(source.Path)).Order().ToArray()
        );
        Assert.Contains(
            "passthrough: ToolEnvironmentPassthrough.Names",
            readers[0].Content,
            StringComparison.Ordinal
        );

        string[] configurers =
        [
            .. CSharpSources()
                .Where(source =>
                    source.Content.Contains(
                        "ToolEnvironmentPassthrough.Configure",
                        StringComparison.Ordinal
                    )
                )
                .Select(source => Path.GetFileName(source.Path)),
        ];

        // Configuring the process-wide list is the frontends' startup job; a spawn site must never
        // own it.
        Assert.DoesNotContain("BashTool.cs", configurers);
        Assert.DoesNotContain("ProcessRunner.cs", configurers);
        Assert.DoesNotContain("ChildProcess.cs", configurers);
        Assert.DoesNotContain("ChildProcess.Unix.cs", configurers);
        Assert.DoesNotContain("ChildProcess.Windows.cs", configurers);
        Assert.Contains("InteractiveSession.ToolEnvironment.cs", configurers);
    }

    [Fact]
    public void No_spawn_site_builds_an_environment_itself()
    {
        // A file that hands a child an environment must name the policy it wants; whether it builds
        // one directly or delegates is then visible at the call, and a new spawn site fails here
        // until it adopts the builder.
        (string Path, string Content)[] spawnSites = CSharpSources()
            .Where(source =>
                source.Content.Contains("EnvironmentScrubber", StringComparison.Ordinal)
                || source.Content.Contains("InheritEnvironmentVariables", StringComparison.Ordinal)
                || source.Content.Contains("EnvironmentVariables =", StringComparison.Ordinal)
                || source.Content.Contains(".Environment.Clear(", StringComparison.Ordinal)
                || source.Content.Contains(".Environment[", StringComparison.Ordinal)
            )
            .ToArray();

        Assert.NotEmpty(spawnSites);
        foreach ((string path, string content) in spawnSites)
        {
            // One of three honest shapes: the file names the policy itself; it takes the tool
            // child's environment from the one method that names it
            // (`ChildProcess.BuildEnvironment`); or it installs a complete set its caller built
            // (`request.Environment`, required non-null on the session request). Anything else is
            // a spawn site deciding a child's environment of its own — and the first test already
            // makes reading the parent environment the builder's alone.
            bool builtHere =
                content.Contains(PolicyMarker, StringComparison.Ordinal)
                || content.Contains("ChildProcess.BuildEnvironment", StringComparison.Ordinal)
                || content.Contains("request.Environment", StringComparison.Ordinal)
                || content.Contains("options.Environment", StringComparison.Ordinal);
            Assert.True(
                builtHere,
                $"{Path.GetFileName(path)} hands a child an environment without naming a policy"
            );
        }
    }

    [Fact]
    public void Every_build_call_names_its_policy()
    {
        // The policy is the first argument with no default, so this pins the shape the compiler
        // already enforces: a call that slips a policy in later would still be caught here.
        foreach (
            (string path, string content) in CSharpSources()
                .Where(source =>
                    source.Content.Contains("EnvironmentScrubber", StringComparison.Ordinal)
                )
        )
        {
            foreach (
                string call in content
                    .Split("EnvironmentScrubber")
                    .Skip(1)
                    .Where(segment => segment.Contains(".Build(", StringComparison.Ordinal))
            )
            {
                int at = call.IndexOf(".Build(", StringComparison.Ordinal) + ".Build(".Length;
                Assert.True(
                    call[at..].TrimStart().StartsWith(PolicyMarker, StringComparison.Ordinal),
                    $"{Path.GetFileName(path)}: EnvironmentScrubber.Build without a named policy"
                );
            }
        }
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
