using System.Globalization;
using System.Text.Json.Nodes;
using Lunate.Agent;

namespace Lunate.Coding;

/// <summary>
/// The <c>lunate sessions search &lt;query&gt; [--json]</c> subcommand: finds past turns by their
/// text. The scan is the store's own (<see cref="Session.Search"/>), bounded and read-only — no
/// index, no cache, no second store — and every run states the bound so a user can tell a full answer
/// from a cut one. A row prints the session id, the turn's timestamp, the session's <c>cwd</c>, the
/// text around the hit and the session file path; searching never opens, resumes or writes a session.
/// <para>
/// A match is printed with its path because this command is non-interactive: opening the turn is what
/// the interactive session's picker search does. Exit codes: 0 a completed search (a match or not),
/// 1 the search could not run, 2 usage.
/// </para>
/// </summary>
internal static class SessionsCommand
{
    internal const int ExitOk = 0;
    internal const int ExitFailure = 1;
    internal const int ExitUsage = 2;

    internal const string Usage = "Usage: lunate sessions search <query> [--json]";

    internal static int Run(
        string[] args,
        string? sessionsRoot,
        TextWriter output,
        TextWriter errors
    )
    {
        if (!TryParse(args, errors, out string query, out bool json))
        {
            return ExitUsage;
        }

        try
        {
            SessionSearchResult result = Session.Search(
                sessionsRoot ?? SessionSelection.DefaultRoot,
                query
            );
            if (json)
            {
                output.WriteLine(ToJson(result));
            }
            else
            {
                WriteMatches(result, query, output);
            }

            return ExitOk;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            errors.WriteLine($"lunate: {exception.Message}");
            return ExitFailure;
        }
    }

    /// <summary>The human form: one block per match, then the scan's own summary with its bound.</summary>
    private static void WriteMatches(SessionSearchResult result, string query, TextWriter output)
    {
        foreach (SessionSearchMatch match in result.Matches)
        {
            output.WriteLine(
                $"{match.SessionId}  {Timestamp(match.Timestamp)}  {match.Role}  {match.Cwd}"
            );
            output.WriteLine("    " + match.Snippet);
            output.WriteLine($"    {match.Path} (entry {match.EntryId})");
        }

        string head =
            result.Matches.Count == 0
                ? $"sessions: no match for '{query}'"
                : FormattableString.Invariant($"sessions: {result.Matches.Count} matches");
        output.WriteLine(head + " — " + ScanLine(result));
    }

    /// <summary>What the scan covered and what the bound was, in one invariant line.</summary>
    private static string ScanLine(SessionSearchResult result)
    {
        string bound = FormattableString.Invariant(
            $"bound {Session.SearchSessionLimit} sessions, {Bytes(Session.SearchTextBytesLimit)} of text and {Session.SearchMatchLimit} matches"
        );
        string line = FormattableString.Invariant(
            $"searched {Sessions(result.SessionsScanned)} ({Bytes(result.TextBytes)} of text); {bound}"
        );
        if (result.MalformedLines > 0)
        {
            line += "; " + Skipped(result.MalformedLines);
        }

        return result.StoppedAtBound ? line + "; stopped at the bound" : line;
    }

    /// <summary>"1 session" and "2 sessions": the scan line reads as English either way.</summary>
    private static string Sessions(int count) =>
        count == 1 ? "1 session" : FormattableString.Invariant($"{count} sessions");

    /// <summary>"1 malformed line" and "2 malformed lines", the same way.</summary>
    private static string Skipped(int count) =>
        count == 1
            ? "skipped 1 malformed line"
            : FormattableString.Invariant($"skipped {count} malformed lines");

    private static string Timestamp(DateTimeOffset timestamp) =>
        timestamp
            .ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>A byte count in the unit a reader can compare with the bound, culture-invariantly.</summary>
    internal static string Bytes(long bytes) =>
        bytes < 1024 ? FormattableString.Invariant($"{bytes} bytes")
        : bytes < 1024 * 1024 ? FormattableString.Invariant($"{bytes / 1024.0:0.0} KiB")
        : FormattableString.Invariant($"{bytes / (1024.0 * 1024.0):0.0} MiB");

    /// <summary>The machine-readable form: the same rows as the human output, as a JSON array.</summary>
    private static string ToJson(SessionSearchResult result)
    {
        var matches = new JsonArray();
        foreach (SessionSearchMatch match in result.Matches)
        {
            matches.Add(
                new JsonObject
                {
                    ["sessionId"] = match.SessionId,
                    ["entryId"] = match.EntryId,
                    ["timestamp"] = Timestamp(match.Timestamp),
                    ["cwd"] = match.Cwd,
                    ["role"] = match.Role,
                    ["snippet"] = match.Snippet,
                    ["path"] = match.Path,
                }
            );
        }

        return matches.ToJsonString();
    }

    /// <summary>
    /// <c>search</c>, the phrase — the remaining bare words joined, so an unquoted phrase works —
    /// and <c>--json</c>; a token after <c>--</c> is phrase text, never a flag. Anything else is a
    /// usage error, including a missing or blank phrase.
    /// </summary>
    private static bool TryParse(string[] args, TextWriter errors, out string query, out bool json)
    {
        query = string.Empty;
        json = false;
        if (args.Length == 0 || args[0] != "search")
        {
            errors.WriteLine(Usage);
            return false;
        }

        List<string> words = [];
        var literal = false;
        foreach (string arg in args[1..])
        {
            if (!literal && arg == "--")
            {
                literal = true;
            }
            else if (!literal && arg == "--json")
            {
                json = true;
            }
            else if (!literal && arg.StartsWith('-'))
            {
                errors.WriteLine(Usage);
                return false;
            }
            else
            {
                words.Add(arg);
            }
        }

        query = string.Join(' ', words).Trim();
        if (query.Length == 0)
        {
            errors.WriteLine(Usage);
            return false;
        }

        return true;
    }
}
