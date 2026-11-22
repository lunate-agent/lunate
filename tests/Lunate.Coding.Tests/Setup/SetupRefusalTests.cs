using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The one configure-it refusal (spec: The wizard trigger — "Non-interactive contexts (print mode,
/// ACP, piped stdin, CI, TERM=dumb) SHALL never open the wizard; the configure-it refusal SHALL name
/// `lunate setup` and its flags and exit 2"). On a machine with no chat answer, print mode, ACP
/// startup and a piped interactive start each exit 2 with the same sentence, instead of the pre-0.3
/// "set 'model'" text and exit 1. Every case runs the production entry: <c>Cli.Run</c> for print and
/// the piped start, <c>AcpMode.Run</c> for ACP (the method <c>lunate --acp</c> calls).
/// </summary>
public sealed class SetupRefusalTests
{
    private const string WorktreeVariable = "LUNATE_TEST_ACP_SETUP_WORKTREE";

    [Fact]
    public async Task Print_mode_on_an_empty_home_exits_2_with_the_setup_refusal()
    {
        using var temp = new TempDirectory();
        using var output = new StringWriter();
        using var errors = new StringWriter();

        int exit = Cli.Run(
            ["-p", "hi"],
            output,
            errors,
            TestContext.Current.CancellationToken,
            printOptions: new PrintModeOptions
            {
                Factory = new FakeChatClientFactory(new ScriptedChatClient()),
                SettingsPath = temp.File("settings.json"),
                ModelsPath = temp.File("models.json"),
                AuthPath = temp.File("auth.json"),
                SessionDirectory = temp.File("sessions"),
                SessionsRoot = temp.File("sessions"),
                WorkingDirectory = temp.Root,
                HomeDirectory = temp.File("home"),
                Environment = _ => null,
                Extensions = TestExtensionSeams.Empty(temp),
                McpConfigPath = temp.File("global-mcp.json"),
                McpAgentsConfigPath = temp.File("agents-mcp.json"),
            }
        );

        Assert.Equal(2, exit);
        Assert.Empty(output.ToString());
        AssertRefusal(errors.ToString());
    }

    [Fact]
    public void Print_mode_reports_a_systemone_chat_binding_once_and_names_the_repair()
    {
        using var temp = new TempDirectory();
        string settingsPath = temp.File("settings.json");
        string authPath = temp.File("auth.json");
        File.WriteAllText(
            settingsPath,
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "decisions": {
                  "provider": "opencode-go",
                  "model": "opencode-go/jev-1.13",
                  "apiFamily": "systemone"
                }
              },
              "usages": { "chat": "decisions" },
              "decisions": { "profile": "decisions" }
            }
            """
        );
        Assert.True(
            ProviderEntry.TryCreate(
                "opencode-go",
                "opencode-go",
                new Uri("https://opencode.ai/inference/go/v1"),
                "auth:opencode-go",
                headers: null,
                region: null,
                out ProviderEntry? provider,
                out string? error
            ),
            error
        );
        ProviderStore.Save(AuthStore.ProviderStoreFilePath(authPath), [provider!]);
        using var output = new StringWriter();
        using var errors = new StringWriter();

        int exit = Cli.Run(
            ["-p", "hi"],
            output,
            errors,
            TestContext.Current.CancellationToken,
            printOptions: new PrintModeOptions
            {
                Factory = new FakeChatClientFactory(new ScriptedChatClient()),
                SettingsPath = settingsPath,
                ModelsPath = temp.File("models.json"),
                AuthPath = authPath,
                SessionDirectory = temp.File("sessions"),
                SessionsRoot = temp.File("sessions"),
                WorkingDirectory = temp.Root,
                HomeDirectory = temp.File("home"),
                Environment = _ => null,
                Extensions = TestExtensionSeams.Empty(temp),
                McpConfigPath = temp.File("global-mcp.json"),
                McpAgentsConfigPath = temp.File("agents-mcp.json"),
            }
        );

        Assert.Equal(1, exit);
        Assert.Empty(output.ToString());
        string text = errors.ToString();
        Assert.Contains("decision-only", text, StringComparison.Ordinal);
        Assert.Contains("lunate setup", text, StringComparison.Ordinal);
        Assert.Equal(1, text.Split("decision-only", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void A_piped_interactive_start_on_an_empty_home_exits_2_with_the_setup_refusal()
    {
        using var temp = new TempDirectory();
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var console = new ScriptedConsoleIO { IsInteractive = false };

        int exit = Cli.Run(
            [],
            output,
            errors,
            TestContext.Current.CancellationToken,
            stdinIsTerminal: false,
            interactiveOptions: new InteractiveSessionOptions
            {
                Console = console,
                Scheduler = new ManualScheduler(),
                SettingsPath = temp.File("settings.json"),
                ModelsPath = temp.File("models.json"),
                AuthPath = temp.File("auth.json"),
                SessionDirectory = temp.File("sessions"),
                WorkingDirectory = temp.Root,
                HomeDirectory = temp.File("home"),
                Environment = _ => null,
            }
        );

        Assert.Equal(2, exit);
        Assert.Empty(output.ToString());
        AssertRefusal(errors.ToString());
        // The terminal hint still belongs to a configured machine; the refusal replaced it here.
        Assert.DoesNotContain("interactive mode needs a terminal", errors.ToString());
    }

    /// <summary>
    /// ACP reads its settings from the process-wide store, so the empty-home half runs in its own
    /// process: <c>LUNATE_HOME</c> points at a fresh directory beside the working directory, and the
    /// serve start refuses before a single frame is served.
    /// </summary>
    [Fact]
    public async Task Acp_startup_on_an_empty_home_exits_2_with_the_setup_refusal()
    {
        const string Method = nameof(Acp_startup_on_an_empty_home_exits_2_with_the_setup_refusal);
        if (TestChildProcess.IsChild(Method))
        {
            Environment.CurrentDirectory = Environment.GetEnvironmentVariable(WorktreeVariable)!;
            using var input = new MemoryStream();
            using var frames = new MemoryStream();
            using var errors = new StringWriter();

            int exit = AcpMode.Run(errors, input, frames, TestContext.Current.CancellationToken);

            Assert.Empty(frames.ToArray());
            AssertRefusal(errors.ToString());
            Assert.Equal(2, exit);
            return;
        }

        using var temp = new TempDirectory();
        string worktree = Directory.CreateDirectory(temp.File("worktree")).FullName;
        string home = Directory.CreateDirectory(temp.File("home")).FullName;

        (int exitCode, string output) = await TestChildProcess.RunAsync(
            typeof(SetupRefusalTests),
            Method,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["LUNATE_HOME"] = home,
                ["LUNATE_MODEL"] = null,
                [WorktreeVariable] = worktree,
            }
        );

        Assert.True(exitCode == 0, $"the ACP refusal check failed (exit {exitCode}):{output}");
    }

    /// <summary>One sentence, naming the command and the flags it offers.</summary>
    private static void AssertRefusal(string errors)
    {
        Assert.Contains("lunate setup", errors, StringComparison.Ordinal);
        Assert.Contains("--provider", errors, StringComparison.Ordinal);
        Assert.Contains("--model", errors, StringComparison.Ordinal);
        Assert.Contains("--credential", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("'model' in", errors, StringComparison.Ordinal);
    }
}
