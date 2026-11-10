using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The tool child's default strip by name segment (#741): a variable whose underscore-separated
/// segment is <c>KEY</c>, <c>TOKEN</c>, <c>SECRET</c> or <c>PASSWORD</c> is removed from the
/// inherited environment, the known provider names are still removed, and the exact-name
/// <c>toolEnvironment.passthrough</c> list wins over both. Every case goes through the real tool
/// child's composition — <see cref="Lunate.Coding.ChildProcess.BuildEnvironment"/> — so the rule is
/// proved where a tool call reads it, not on a helper beside it.
/// </summary>
[Collection(ToolEnvironmentCollection.Name)]
public sealed class ToolChildDefaultStripTests
{
    [Fact]
    public void A_name_with_a_token_segment_is_stripped()
    {
        IReadOnlyDictionary<string, string> child = Build(
            new() { ["GITHUB_TOKEN"] = "fixture-only" }
        );

        Assert.DoesNotContain("GITHUB_TOKEN", child.Keys);
    }

    [Fact]
    public void A_name_with_a_secret_segment_is_stripped()
    {
        IReadOnlyDictionary<string, string> child = Build(
            new() { ["AWS_SECRET_ACCESS_KEY"] = "fixture-only" }
        );

        // `SECRET` and `KEY` both match; one segment is enough.
        Assert.DoesNotContain("AWS_SECRET_ACCESS_KEY", child.Keys);
    }

    [Fact]
    public void A_name_that_is_only_monkey_is_not_stripped()
    {
        IReadOnlyDictionary<string, string> child = Build(new() { ["MONKEY"] = "user-owned" });

        // The segment is MONKEY, not KEY. This is the case a substring test gets wrong.
        Assert.Equal("user-owned", child["MONKEY"]);
    }

    [Fact]
    public void A_segment_that_only_starts_with_token_is_not_stripped()
    {
        IReadOnlyDictionary<string, string> child = Build(
            new() { ["GITLAB_TOKENIZE_TIMEOUT"] = "user-owned" }
        );

        // TOKENIZE is not TOKEN: a segment match, never a substring one.
        Assert.Equal("user-owned", child["GITLAB_TOKENIZE_TIMEOUT"]);
    }

    [Fact]
    public void A_known_provider_key_is_still_stripped()
    {
        IReadOnlyDictionary<string, string> child = Build(
            new() { ["OPENAI_API_KEY"] = "fixture-only" }
        );

        // The known-name list is unchanged: a name Lunate resolved is still removed even though the
        // segment rule would also match it.
        Assert.DoesNotContain("OPENAI_API_KEY", child.Keys);
    }

    [Fact]
    public void A_passed_through_name_survives_a_matching_segment()
    {
        try
        {
            ToolEnvironmentPassthrough.Configure(["GITHUB_TOKEN"]);

            IReadOnlyDictionary<string, string> child = Build(
                new() { ["GITHUB_TOKEN"] = "user-owned", ["NPM_TOKEN"] = "fixture-only" }
            );

            // The exact name is the opt-in against the segment rule, and it admits only that name.
            Assert.Equal("user-owned", child["GITHUB_TOKEN"]);
            Assert.DoesNotContain("NPM_TOKEN", child.Keys);
        }
        finally
        {
            ToolEnvironmentPassthrough.ResetForTests();
        }
    }

    [Fact]
    public void A_segment_match_is_matched_without_regard_to_case()
    {
        IReadOnlyDictionary<string, string> child = Build(
            new() { ["github_token"] = "fixture-only", ["Database_Password"] = "fixture-only" }
        );

        Assert.DoesNotContain("github_token", child.Keys);
        Assert.DoesNotContain("Database_Password", child.Keys);
    }

    [Fact]
    public void A_declared_value_cannot_put_a_segment_match_back()
    {
        var request = new ProcessRunRequest(
            "/bin/true",
            [],
            ".",
            Environment: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PATH"] = "/usr/bin:/bin",
            }
        )
        {
            ExtraEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NPM_TOKEN"] = "fixture-only",
            },
        };

        IReadOnlyDictionary<string, string> child = Lunate.Coding.ChildProcess.BuildEnvironment(
            request
        );

        // The segment rule is a deny for the whole child, like the known-name strip.
        Assert.DoesNotContain("NPM_TOKEN", child.Keys);
    }

    [Fact]
    public void The_rest_of_the_parent_environment_still_arrives()
    {
        IReadOnlyDictionary<string, string> child = Build(
            new() { ["MY_TOOLCHAIN_HOME"] = "/opt/toolchain" }
        );

        // The rule strips names, not the inherited policy: an unrelated variable is the user's.
        Assert.Equal("/opt/toolchain", child["MY_TOOLCHAIN_HOME"]);
        Assert.Equal("/usr/bin:/bin", child["PATH"]);
    }

    /// <summary>
    /// Builds the tool child's environment from a parent set plus the platform names a shell needs,
    /// so the assertion is about the strip and never about the test's own parent dictionary.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Build(Dictionary<string, string> parent)
    {
        parent.TryAdd("PATH", "/usr/bin:/bin");
        var request = new ProcessRunRequest(
            "/bin/true",
            [],
            ".",
            Environment: new Dictionary<string, string>(parent, StringComparer.Ordinal)
        );
        return Lunate.Coding.ChildProcess.BuildEnvironment(request);
    }
}
