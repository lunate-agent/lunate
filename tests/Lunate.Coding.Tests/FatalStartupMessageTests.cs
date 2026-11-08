using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The fatal startup message the live area must keep readable after teardown (spec: Fatal startup
/// message rendering). At 40 columns the workspace-guard refusal is committed to scrollback wrapped
/// across physical rows: every row is inside the terminal and the whole reason survives, tail
/// included. The second proof is the other half of the rule: an unset <c>LUNATE_HOME</c> resolves to
/// the default home even when the session starts from that home directory, so the session composes
/// and reports no workspace refusal.
/// <para>
/// Both proofs run in a child process because the variable is process-wide: the child is spawned for
/// one case, sets the environment once, and asserts — no other test can observe the mutation.
/// </para>
/// </summary>
public sealed class FatalStartupMessageTests
{
    private const string Refusal =
        "LUNATE_HOME points inside the workspace; trust records there could be written by the repository";

    private const string WorktreeVariable = "LUNATE_TEST_FATAL_WORKTREE";
    private const string HomeVariable = "LUNATE_TEST_DEFAULT_HOME";

    [Fact]
    public async Task The_workspace_guard_message_is_fully_readable_at_40_columns()
    {
        const string Method = nameof(The_workspace_guard_message_is_fully_readable_at_40_columns);
        if (TestChildProcess.IsChild(Method))
        {
            await ProbeGuardAsync();
            return;
        }

        using var temp = new TempDirectory();
        string worktree = temp.File("worktree");
        Directory.CreateDirectory(worktree);
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["LUNATE_HOME"] = Path.Combine(worktree, ".state"),
            [WorktreeVariable] = worktree,
        };

        (int exitCode, string output) = await TestChildProcess.RunAsync(
            typeof(FatalStartupMessageTests),
            Method,
            environment
        );

        Assert.True(
            exitCode == 0,
            $"the 40-column probe failed (exit {exitCode}):{Environment.NewLine}{output}"
        );
    }

    [Fact]
    public async Task A_session_starts_with_the_default_home_from_the_home_directory()
    {
        const string Method = nameof(
            A_session_starts_with_the_default_home_from_the_home_directory
        );
        if (TestChildProcess.IsChild(Method))
        {
            await ProbeDefaultHomeAsync();
            return;
        }

        using var temp = new TempDirectory();
        string home = temp.File("home");
        Directory.CreateDirectory(home);
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["LUNATE_HOME"] = null,
            ["HOME"] = home,
            ["USERPROFILE"] = home,
            [HomeVariable] = home,
        };

        (int exitCode, string output) = await TestChildProcess.RunAsync(
            typeof(FatalStartupMessageTests),
            Method,
            environment
        );

        Assert.True(
            exitCode == 0,
            $"the default-home probe failed (exit {exitCode}):{Environment.NewLine}{output}"
        );
    }

    private static async Task ProbeGuardAsync()
    {
        string worktree = Required(WorktreeVariable);
        Environment.CurrentDirectory = worktree;

        using var host = new InteractiveSessionHost(
            workingDirectory: worktree,
            interactiveConsole: true,
            unifiedScrollback: true
        );
        host.Console.Size = new ConsoleSize(40, 24);
        host.Console.Complete();
        await host.RunAsync();

        Assert.Equal(1, host.Session.ExitCode);
        Assert.False(host.Session.IsComposed);

        var screen = new TerminalScreen(40);
        foreach (string write in host.Console.Writes)
        {
            screen.Feed(write);
        }

        IReadOnlyList<string> lines = screen.Lines;
        Assert.All(
            lines,
            line => Assert.True(CellText.Width(line) <= 40, $"'{line}' overflows 40 columns")
        );

        // The reason wraps across physical rows (the first also carries the "lunate: " prefix), so
        // joining the rows back into one whitespace-normalised stream is what proves the tail is
        // still there: a clipped or elided message loses its final word and fails this assertion.
        string text = string.Join("\n", lines);
        string reason = string.Join(
            ' ',
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
        Assert.Contains(Refusal, reason, StringComparison.Ordinal);
    }

    private static async Task ProbeDefaultHomeAsync()
    {
        string home = Required(HomeVariable);
        Environment.CurrentDirectory = home;
        Assert.Null(Environment.GetEnvironmentVariable("LUNATE_HOME"));

        using var host = new InteractiveSessionHost(workingDirectory: home);
        host.Console.Complete();
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);
        await run;

        Assert.True(host.Session.IsComposed);
        Assert.Equal(0, host.Session.ExitCode);
        Assert.DoesNotContain(
            "LUNATE_HOME points inside the workspace",
            host.Diagnostics,
            StringComparison.Ordinal
        );
    }

    private static string Required(string variable) =>
        Environment.GetEnvironmentVariable(variable)
        ?? throw new InvalidOperationException($"{variable} is not set for the child probe.");
}
