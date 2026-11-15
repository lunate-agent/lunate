using System.Text.Json;
using Lunate.Agent;

namespace Lunate.Coding.Tests;

/// <summary>
/// The environment holds process-wide names, one of them (the empty <c>GIT_CONFIG_VALUE_0</c>) with
/// a meaning only the process target keeps, so a class that applies the lane's git configuration must
/// not run beside any other test class.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LaneGitConfigurationCollection
{
    public const string Name = "lane-git-configuration";
}

/// <summary>
/// The supervisor's git environment configuration — <c>GIT_CONFIG_COUNT</c>, <c>GIT_CONFIG_KEY_n</c>
/// and <c>GIT_CONFIG_VALUE_n</c> — is one unit: git reads the first COUNT key/value pairs and refuses
/// the whole run when a key is missing. The tool child's credential-shape strip used to withhold
/// <c>GIT_CONFIG_KEY_n</c> alone (its name has a <c>KEY</c> part), so every git command in a bash call
/// and the post-bash change scan failed with "missing config key GIT_CONFIG_KEY_0" (#871). These
/// tests run real git through both, with the process environment set the way the lane sets it: the
/// tool child and the scan inherit the process environment, so it is the only seam that reaches them.
/// </summary>
[Collection(LaneGitConfigurationCollection.Name)]
public sealed class BashGitConfigurationTests
{
    private static readonly ToolContext Context = new("unused", new NullAgentEvents());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Bash_runs_git_status_and_commit_under_the_lane_configuration()
    {
        Assert.SkipWhen(
            OperatingSystem.IsWindows(),
            "the lane runs POSIX shells; the builder's family rule is pinned on every platform"
        );
        using var repo = new GitTestRepo();
        repo.Write("a.cs", "class A { }\n");
        repo.CommitAll();
        repo.Write("a.cs", "class A { int changed; }\n");

        ToolResult result;
        using (LaneGitConfiguration.Apply())
        {
            var tool = new BashTool(new Workspace(repo.Root), null, WaitBudgets.CommandRun);
            result = await tool.ExecuteAsync(
                CommandArgs(
                    "printf '%s|%s|%s\\n' \"$GIT_CONFIG_COUNT\" \"$GIT_CONFIG_KEY_0\" "
                        + "\"$GIT_CONFIG_VALUE_1\"; git status --porcelain; git commit -qam second; "
                        + "git config --get credential.helper"
                ),
                Context,
                CancellationToken.None
            );
        }

        // The three names arrived, git status and git commit both ran, and the configured
        // credential helper is the empty value the supervisor set (the empty line before the exit
        // code), not the machine's helper.
        Assert.False(result.IsError, result.Output);
        Assert.Equal("3|credential.helper|0\n M a.cs\n\nexit code: 0", result.Output);
        Assert.Equal("second\n", GitTestRepo.Output(repo.Root, "log", "-1", "--pretty=%s"));
    }

    [Fact]
    public async Task The_change_scan_reads_git_under_the_lane_configuration()
    {
        using var repo = new GitTestRepo();
        repo.Write("a.cs", "class A { }\n");
        repo.CommitAll();
        var tracker = new BashChangeTracker(scanTimeout: WaitBudgets.BashScan);
        await tracker.ObserveAsync("run_1", repo.Root, Ct);

        repo.Write("b.cs", "class B { }\n");
        ScanReport scan;
        using (LaneGitConfiguration.Apply())
        {
            scan = await tracker.ObserveAsync("run_1", repo.Root, Ct);
        }

        // The scan's git is the same tool-child policy: it answered from git (not from the
        // content-stamp backstop, which is what a partial configuration forces) and published the
        // real change.
        Assert.Equal(ScanSource.Git, scan.Source);
        Assert.Null(scan.FailureReason);
        Assert.Equal([new BashChangeTracker.Change("b.cs", FileChangeKind.Created)], scan.Changes);
    }

    private static JsonElement CommandArgs(string command) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { command })).RootElement.Clone();

    /// <summary>
    /// The lane's three-entry git configuration, applied to the process environment for the duration
    /// of one test and restored afterwards. A platform whose process environment cannot hold an empty
    /// value skips the test rather than asserting a configuration it did not apply.
    /// </summary>
    private sealed class LaneGitConfiguration : IDisposable
    {
        private static readonly (string Name, string Value)[] Names =
        [
            ("GIT_CONFIG_COUNT", "3"),
            ("GIT_CONFIG_KEY_0", "credential.helper"),
            ("GIT_CONFIG_VALUE_0", ""),
            ("GIT_CONFIG_KEY_1", "gc.auto"),
            ("GIT_CONFIG_VALUE_1", "0"),
            ("GIT_CONFIG_KEY_2", "maintenance.auto"),
            ("GIT_CONFIG_VALUE_2", "false"),
        ];

        private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

        private LaneGitConfiguration() { }

        internal static LaneGitConfiguration Apply()
        {
            var applied = new LaneGitConfiguration();
            foreach ((string name, string value) in Names)
            {
                applied._previous[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }

            if (Environment.GetEnvironmentVariable("GIT_CONFIG_VALUE_0") is null)
            {
                // A skip must not leave the names behind for the rest of the run.
                applied.Dispose();
                Assert.Skip("this platform's process environment cannot hold an empty value");
            }

            return applied;
        }

        public void Dispose()
        {
            foreach ((string name, string? value) in _previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
