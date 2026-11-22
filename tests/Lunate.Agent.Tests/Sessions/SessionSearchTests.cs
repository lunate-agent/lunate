using Microsoft.Extensions.AI;

namespace Lunate.Agent.Tests;

/// <summary>
/// Session search (spec: "Session search"): one read-only, bounded, linear scan over a fixture state
/// directory. The fixture — <c>tests/fixtures/session-search/</c> — holds a matching session, a nested
/// session with a truncated JSONL line, and a session whose only hits are a tool result and a
/// compaction summary. The tests pin a match, no match, the malformed line, the bounds and that a
/// search writes nothing.
/// </summary>
public sealed class SessionSearchTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 20, 9, 0, 1, TimeSpan.Zero);

    private static string Fixtures =>
        Path.Combine(TestPaths.RepositoryRoot, "tests", "fixtures", "session-search");

    [Fact]
    public void A_query_returns_each_matching_turn_with_its_session_facts()
    {
        SessionSearchResult result = Session.Search(Fixtures, "Retry Budget");
        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(3, result.SessionsScanned);
        Assert.Equal(0, result.SessionsSkipped);
        Assert.False(result.StoppedAtBound);

        SessionSearchMatch match = Assert.Single(result.Matches, candidate =>
            candidate.SessionId == "s_20261020-090000-a1b2"
        );
        Assert.Equal("e_01", match.EntryId);
        Assert.Equal("/repo/app", match.Cwd);
        Assert.Equal(Start, match.Timestamp);
        Assert.Equal("user", match.Role);
        Assert.EndsWith("a1b2.jsonl", match.Path, StringComparison.Ordinal);
        Assert.Contains("retry budget", match.Snippet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\n', match.Snippet);

        SessionSearchMatch nested = Assert.Single(result.Matches, candidate =>
            candidate.SessionId == "s_20261021-100000-c3d4"
        );
        Assert.Equal("e_03", nested.EntryId);
        Assert.Equal("/repo/lib", nested.Cwd);
        Assert.Contains("nested", nested.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void No_match_returns_nothing_and_still_states_the_scan()
    {
        SessionSearchResult result = Session.Search(Fixtures, "kubernetes ingress");

        Assert.Empty(result.Matches);
        Assert.Equal(3, result.SessionsScanned);
        Assert.False(result.StoppedAtBound);
    }

    [Fact]
    public void A_malformed_line_is_skipped_and_counted()
    {
        SessionSearchResult result = Session.Search(Fixtures, "retry budget");

        Assert.Equal(1, result.MalformedLines);
        Assert.Equal(2, result.Matches.Count);
    }

    [Fact]
    public void A_tool_result_and_a_compaction_summary_are_not_turns()
    {
        SessionSearchResult toolResult = Session.Search(Fixtures, "a tool result mentioning");
        SessionSearchResult summary = Session.Search(Fixtures, "Compaction noting");

        Assert.Empty(toolResult.Matches);
        Assert.Empty(summary.Matches);
    }

    [Fact]
    public void The_file_bound_stops_the_scan_and_says_so()
    {
        using var temp = new TempDirectory();
        for (var index = 0; index <= Session.SearchSessionLimit; index++)
        {
            Session.Create(
                temp.File($"s_{index}.jsonl"),
                "/work",
                new FixedTimeProvider(Start.AddSeconds(index))
            );
        }

        SessionSearchResult result = Session.Search(temp.Root, "nothing matches this");

        Assert.Equal(Session.SearchSessionLimit, result.SessionsScanned);
        Assert.True(result.StoppedAtBound);
    }

    [Fact]
    public void The_match_bound_stops_the_scan_and_says_so()
    {
        using var temp = new TempDirectory();
        Session session = Session.Create(
            temp.File("s.jsonl"),
            "/work",
            new FixedTimeProvider(Start)
        );
        for (var index = 0; index < Session.SearchMatchLimit + 5; index++)
        {
            session.AppendMessage(new ChatMessage(ChatRole.User, $"retry budget {index}"));
        }

        SessionSearchResult result = Session.Search(temp.Root, "retry budget");

        Assert.Equal(Session.SearchMatchLimit, result.Matches.Count);
        Assert.True(result.StoppedAtBound);
    }

    [Fact]
    public void The_text_bound_stops_the_scan_and_says_so()
    {
        using var temp = new TempDirectory();
        Session session = Session.Create(
            temp.File("s.jsonl"),
            "/work",
            new FixedTimeProvider(Start)
        );
        session.AppendMessage(
            new ChatMessage(ChatRole.User, new string('x', (int)Session.SearchTextBytesLimit))
        );
        session.AppendMessage(new ChatMessage(ChatRole.User, "retry budget past the budget"));

        SessionSearchResult result = Session.Search(temp.Root, "retry budget");

        Assert.Empty(result.Matches);
        Assert.True(result.TextBytes >= Session.SearchTextBytesLimit);
        Assert.True(result.StoppedAtBound);
    }

    [Fact]
    public void A_search_writes_nothing()
    {
        using var temp = new TempDirectory();
        Session session = Session.Create(
            temp.File("s.jsonl"),
            "/work",
            new FixedTimeProvider(Start)
        );
        session.AppendMessage(new ChatMessage(ChatRole.User, "retry budget"));
        string path = session.Path;
        byte[] before = File.ReadAllBytes(path);
        DateTime beforeWrite = File.GetLastWriteTimeUtc(path);
        string[] filesBefore = Directory.GetFiles(temp.Root, "*", SearchOption.AllDirectories);

        Session.Search(temp.Root, "retry budget");

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(path));
        Assert.Equal(
            filesBefore.Order(StringComparer.Ordinal),
            Directory
                .GetFiles(temp.Root, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void A_missing_root_searches_nothing()
    {
        using var temp = new TempDirectory();

        SessionSearchResult result = Session.Search(temp.File("missing"), "retry budget");

        Assert.Empty(result.Matches);
        Assert.Equal(0, result.SessionsScanned);
        Assert.False(result.StoppedAtBound);
    }
}
