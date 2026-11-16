using Lunate.Agent;
using Lunate.Ai;
using Lunate.Protocols.Acp;

namespace Lunate.Coding.Tests;

/// <summary>
/// ACP keeps tool commands on the host in this change, so a container selection cannot be honored
/// there. The launch flag is already a usage error; the user-scope <c>environment</c> setting
/// resolves the same selection without a flag, so the serve start refuses it (exit 2, nothing on the
/// protocol stream) and the per-session harness refuses it again instead of running the commands on
/// the host, which is the silent fallback the selection forbids.
/// </summary>
public sealed class AcpEnvironmentRefusalTests
{
    private const string WorktreeVariable = "LUNATE_TEST_ACP_ENVIRONMENT_WORKTREE";

    [Fact]
    public async Task A_stored_environment_selection_refuses_acp_startup()
    {
        const string Method = nameof(A_stored_environment_selection_refuses_acp_startup);
        if (TestChildProcess.IsChild(Method))
        {
            Environment.CurrentDirectory = Environment.GetEnvironmentVariable(WorktreeVariable)!;
            using var input = new MemoryStream();
            using var frames = new MemoryStream();
            using var errors = new StringWriter();

            int exit = AcpMode.Run(errors, input, frames, TestContext.Current.CancellationToken);

            // No session is served: the refusal is the whole answer, on stderr only.
            Assert.Empty(frames.ToArray());
            AssertRefusal(errors.ToString());
            Assert.Equal(AcpExecutionEnvironmentRefusal.ExitCode, exit);
            return;
        }

        using var temp = new TempDirectory();
        string worktree = Directory.CreateDirectory(temp.File("worktree")).FullName;
        string home = Directory.CreateDirectory(temp.File("home")).FullName;
        File.WriteAllText(
            Path.Combine(home, "settings.json"),
            """{"schemaVersion":2,"environment":"docker:alpine"}"""
        );

        (int exitCode, string output) = await TestChildProcess.RunAsync(
            typeof(AcpEnvironmentRefusalTests),
            Method,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["LUNATE_HOME"] = home,
                [WorktreeVariable] = worktree,
            }
        );

        Assert.True(
            exitCode == 0,
            $"the ACP environment refusal check failed (exit {exitCode}):{output}"
        );
    }

    [Theory]
    [InlineData("docker", "alpine")]
    [InlineData("podman", "ghcr.io/org/image:tag")]
    public void A_session_harness_refuses_settings_with_a_selection(string engine, string image)
    {
        using var temp = new TempDirectory();
        AgentSettings settings = ChatUsageProfileTestSupport.Settings() with
        {
            Environment = new ExecutionEnvironmentSelection(engine, image),
        };

        AcpEnvironmentRefusedException exception = Assert.Throws<AcpEnvironmentRefusedException>(
            () =>
                CreateHarness(temp, settings, launch: null)
        );

        AssertRefusal(exception.Message);
    }

    [Fact]
    public void A_launch_selection_is_refused_by_the_session_harness_too()
    {
        using var temp = new TempDirectory();
        Assert.True(
            LaunchOptions.TryParse(["--env", "podman:alpine"], out LaunchOptions launch, out _)
        );

        AcpEnvironmentRefusedException exception = Assert.Throws<AcpEnvironmentRefusedException>(
            () =>
                CreateHarness(temp, ChatUsageProfileTestSupport.Settings(), launch)
        );

        AssertRefusal(exception.Message);
    }

    private static AgentHarness CreateHarness(
        TempDirectory temp,
        AgentSettings settings,
        LaunchOptions? launch
    ) =>
        AcpMode.CreateHarness(
            new AcpSessionContext(temp.Root, null, null),
            null,
            [],
            [],
            launch,
            sessionDirectory: temp.File("sessions"),
            settings: settings,
            catalog: ModelCatalog.Load(temp.File("models.json")),
            authPath: temp.File("auth.json")
        );

    /// <summary>
    /// The refusal names what is set, why ACP cannot use it, and what the user can do instead, so
    /// neither the setting nor the mode is left implicit.
    /// </summary>
    private static void AssertRefusal(string text)
    {
        Assert.Contains("'environment' setting", text, StringComparison.Ordinal);
        Assert.Contains("--acp cannot run", text, StringComparison.Ordinal);
        Assert.Contains("interactive or print mode", text, StringComparison.Ordinal);
    }
}
