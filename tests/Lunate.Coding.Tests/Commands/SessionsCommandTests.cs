using System.Text.Json.Nodes;
using Lunate.Agent;
using Lunate.TestSupport;

namespace Lunate.Coding.Tests;

/// <summary>
/// <c>lunate sessions search</c> (spec: "Session search"): the fixture state directory
/// <c>tests/fixtures/session-search/</c> answers a match, no match and malformed lines. The command
/// prints each row's session id, timestamp, cwd, snippet and path, states the scan's bound, offers
/// the same rows as JSON and treats a missing or unknown query as usage.
/// </summary>
public sealed class SessionsCommandTests
{
    private static string Fixtures =>
        Path.Combine(RepositoryRoot.Find(), "tests", "fixtures", "session-search");

    [Fact]
    public void A_match_prints_its_session_facts_snippet_and_path()
    {
        (int exit, string output, string errors) = Run("search", "retry", "budget");

        Assert.Equal(SessionsCommand.ExitOk, exit);
        Assert.Empty(errors);
        Assert.Contains("s_20261020-090000-a1b2", output, StringComparison.Ordinal);
        Assert.Contains("2026-10-20T09:00:01Z", output, StringComparison.Ordinal);
        Assert.Contains("/repo/app", output, StringComparison.Ordinal);
        Assert.Contains("retry budget", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(entry e_01)", output, StringComparison.Ordinal);
        Assert.Contains("a1b2.jsonl", output, StringComparison.Ordinal);
        Assert.Contains("2 matches", output, StringComparison.Ordinal);
        Assert.Contains("bound 200 sessions", output, StringComparison.Ordinal);
        Assert.Contains("skipped 2 malformed lines", output, StringComparison.Ordinal);
    }

    [Fact]
    public void No_match_reports_nothing_matched_and_still_states_the_bound()
    {
        (int exit, string output, string errors) = Run("search", "kubernetes ingress");

        Assert.Equal(SessionsCommand.ExitOk, exit);
        Assert.Empty(errors);
        Assert.Contains("no match for 'kubernetes ingress'", output, StringComparison.Ordinal);
        Assert.Contains("bound 200 sessions", output, StringComparison.Ordinal);
        Assert.DoesNotContain(".jsonl (entry", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_is_the_rows_as_an_array()
    {
        (int exit, string output, string errors) = Run("search", "retry budget", "--json");

        Assert.Equal(SessionsCommand.ExitOk, exit);
        Assert.Contains("2 matches", errors, StringComparison.Ordinal);
        Assert.Contains("bound 200 sessions", errors, StringComparison.Ordinal);
        JsonArray matches = JsonNode.Parse(output.Trim())!.AsArray();
        Assert.Equal(2, matches.Count);
        JsonNode row = Assert.Single(matches, match =>
            match!["sessionId"]!.GetValue<string>() == "s_20261020-090000-a1b2"
        )!;
        Assert.Equal("e_01", row["entryId"]!.GetValue<string>());
        Assert.Equal("2026-10-20T09:00:01Z", row["timestamp"]!.GetValue<string>());
        Assert.Equal("/repo/app", row["cwd"]!.GetValue<string>());
        Assert.Equal("user", row["role"]!.GetValue<string>());
        Assert.Contains(
            "retry budget",
            row["snippet"]!.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase
        );
        Assert.EndsWith("a1b2.jsonl", row["path"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void Json_states_the_bound_and_a_stopped_scan_on_stderr()
    {
        using var temp = new TempDirectory();
        for (var index = 0; index <= Session.SearchSessionLimit; index++)
        {
            Session.Create(temp.File($"s_{index}.jsonl"), "/work");
        }

        using var output = new StringWriter();
        using var errors = new StringWriter();
        int exit = SessionsCommand.Run(
            ["search", "nothing matches this", "--json"],
            temp.Root,
            output,
            errors
        );

        Assert.Equal(SessionsCommand.ExitOk, exit);
        Assert.Empty(JsonNode.Parse(output.ToString().Trim())!.AsArray());
        Assert.Contains(
            "no match for 'nothing matches this'",
            errors.ToString(),
            StringComparison.Ordinal
        );
        Assert.Contains("bound 200 sessions", errors.ToString(), StringComparison.Ordinal);
        Assert.Contains("stopped at the bound", errors.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_query_is_a_usage_error()
    {
        (int exit, string output, string errors) = Run("search");

        Assert.Equal(SessionsCommand.ExitUsage, exit);
        Assert.Empty(output);
        Assert.Contains(SessionsCommand.Usage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_flag_is_a_usage_error()
    {
        (int exit, string output, string errors) = Run("search", "retry budget", "--wat");

        Assert.Equal(SessionsCommand.ExitUsage, exit);
        Assert.Empty(output);
        Assert.Contains(SessionsCommand.Usage, errors, StringComparison.Ordinal);
    }

    [Fact]
    public void The_query_may_follow_the_end_of_options_marker()
    {
        (int exit, string output, string errors) = Run("search", "--", "--retry");

        Assert.Equal(SessionsCommand.ExitOk, exit);
        Assert.Empty(errors);
        Assert.Contains("no match for '--retry'", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_cli_dispatches_the_sessions_subcommand()
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();

        int exit = Cli.Run(
            ["sessions", "search", "retry budget"],
            output,
            errors,
            TestContext.Current.CancellationToken,
            sessionsRoot: Fixtures
        );

        Assert.Equal(0, exit);
        Assert.Empty(errors.ToString());
        Assert.Contains("s_20261020-090000-a1b2", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_help_lists_the_sessions_subcommand()
    {
        using var output = new StringWriter();

        int exit = Cli.Run(
            ["--help"],
            output,
            TextWriter.Null,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, exit);
        Assert.Contains(
            "lunate sessions search <query> [--json]",
            output.ToString(),
            StringComparison.Ordinal
        );
    }

    private static (int Exit, string Output, string Errors) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        int exit = SessionsCommand.Run(args, Fixtures, output, errors);
        return (exit, output.ToString(), errors.ToString());
    }
}
