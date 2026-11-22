using System.Text;
using Microsoft.Extensions.AI;

namespace Lunate.Agent;

/// <summary>One matching turn: the session's facts, the entry to open at and the text around the hit.</summary>
internal sealed record SessionSearchMatch(
    string SessionId,
    string EntryId,
    string Path,
    DateTimeOffset Timestamp,
    string Cwd,
    string Role,
    string Snippet
);

/// <summary>
/// What one bounded scan covered: its rows, the work it did, what it could not parse and whether a
/// bound stopped it. The counters exist so both surfaces can state the bound honestly.
/// </summary>
internal sealed record SessionSearchResult(
    IReadOnlyList<SessionSearchMatch> Matches,
    int SessionsScanned,
    int SessionsSkipped,
    int MalformedLines,
    long TextBytes,
    bool StoppedAtBound
);

public sealed partial class Session
{
    /// <summary>The most session files one search opens, chosen by last write before any line.</summary>
    internal const int SearchSessionLimit = 200;

    /// <summary>The most message text one search reads, in UTF-8 bytes.</summary>
    internal const long SearchTextBytesLimit = 8L * 1024 * 1024;

    /// <summary>The most matching turns one search returns.</summary>
    internal const int SearchMatchLimit = 20;

    /// <summary>How much text before the hit a snippet keeps.</summary>
    internal const int SearchSnippetBefore = 40;

    /// <summary>How much text after the hit a snippet keeps.</summary>
    internal const int SearchSnippetAfter = 80;

    /// <summary>
    /// Searches past turns for <paramref name="query"/>: a read-only, linear scan of the session
    /// store, newest last write first, bounded by <see cref="SearchSessionLimit"/> files,
    /// <see cref="SearchTextBytesLimit"/> bytes of message text and <see cref="SearchMatchLimit"/>
    /// matches. Only <c>user</c> and <c>assistant</c> message text is searched — tool results,
    /// compaction summaries, extension entries and image blobs are not turns — and the query is a
    /// verbatim, case-insensitive substring, never a regex or a set of tokens. A malformed line is
    /// counted and skipped; an unreadable header skips its file. Nothing is written, and no index or
    /// cache is kept: the JSONL files are the corpus.
    /// </summary>
    internal static SessionSearchResult Search(string sessionsRoot, string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        List<SessionSearchMatch> matches = [];
        var scanned = 0;
        var skipped = 0;
        var malformed = 0;
        long textBytes = 0;
        (List<string> paths, int total) = NewestFiles(sessionsRoot);
        var stopped = false;

        foreach (string path in paths)
        {
            if (matches.Count >= SearchMatchLimit || textBytes >= SearchTextBytesLimit)
            {
                stopped = true;
                break;
            }

            scanned++;
            SessionFileScan scan = ScanFile(
                path,
                query,
                textBytes,
                SearchMatchLimit - matches.Count
            );
            malformed += scan.MalformedLines;
            textBytes += scan.TextBytes;
            if (scan.Header is null)
            {
                // A file whose header line does not read is skipped, as the listing skips it.
                skipped++;
            }
            else
            {
                // The rows the scan already found belong to the result even when its own bound
                // stopped it mid-file.
                matches.AddRange(scan.Matches);
            }

            if (scan.StoppedAtBound)
            {
                stopped = true;
                break;
            }
        }

        // A store with more files than the cap was cut by the file bound, even when every file
        // that was opened scanned cleanly.
        stopped |= total > paths.Count;

        return new(matches, scanned, skipped, malformed, textBytes, stopped);
    }

    /// <summary>
    /// The session files under the root, newest last write first, capped before any file is opened;
    /// equal write times order by path so the choice is deterministic. The total count is returned
    /// too, so the caller can say a bound cut the scan. A missing root yields nothing.
    /// </summary>
    private static (List<string> Paths, int Total) NewestFiles(string root)
    {
        if (!Directory.Exists(root))
        {
            return ([], 0);
        }

        List<(string Path, DateTime Modified)> files = [];
        foreach (string path in EnumerateSessionFiles(root))
        {
            try
            {
                files.Add((path, File.GetLastWriteTimeUtc(path)));
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                // A file that vanished between the enumeration and the stat is not searchable.
            }
        }

        files.Sort(
            (left, right) =>
            {
                int byModified = right.Modified.CompareTo(left.Modified);
                return byModified != 0 ? byModified : string.CompareOrdinal(right.Path, left.Path);
            }
        );

        return (files.Take(SearchSessionLimit).Select(file => file.Path).ToList(), files.Count);
    }

    /// <summary>One file's scan: its header, its matching turns and what it cost or could not parse.</summary>
    private sealed record SessionFileScan(
        SessionHeaderEntry? Header,
        IReadOnlyList<SessionSearchMatch> Matches,
        int MalformedLines,
        long TextBytes,
        bool StoppedAtBound
    );

    /// <summary>
    /// Reads one session file line by line, stopping when the text budget (shared by the whole
    /// search) or the remaining match budget is spent. A read failure keeps the header and the
    /// matches read so far; an unreadable header skips the file.
    /// </summary>
    private static SessionFileScan ScanFile(
        string path,
        string query,
        long textAlreadyRead,
        int matchesRemaining
    )
    {
        List<SessionSearchMatch> matches = [];
        var malformed = 0;
        long textBytes = 0;
        var stopped = false;
        SessionHeaderEntry? header = null;

        try
        {
            using var reader = SessionFiles.OpenTextReader(path);
            string? line = reader.ReadLine();
            if (line is not null)
            {
                header = SessionFormat.Parse(line) as SessionHeaderEntry;
            }

            while (header is not null && (line = reader.ReadLine()) is not null)
            {
                if (
                    textAlreadyRead + textBytes >= SearchTextBytesLimit
                    || matches.Count >= matchesRemaining
                )
                {
                    stopped = true;
                    break;
                }

                SessionEntry entry;
                try
                {
                    entry = SessionFormat.Parse(line);
                }
                catch (InvalidDataException)
                {
                    malformed++;
                    continue;
                }

                if (
                    entry is not SessionMessageEntry message
                    || MessageText(message) is not { } text
                )
                {
                    continue;
                }

                long remaining = SearchTextBytesLimit - textAlreadyRead - textBytes;
                long bytes = Encoding.UTF8.GetByteCount(text);
                if (bytes > remaining)
                {
                    // The message alone is larger than the text budget that is left: search and
                    // charge only the part that fits, then stop at the bound, so the scan never
                    // reads past the 8 MiB it states.
                    text = TextWithinBudget(text, remaining);
                    bytes = Encoding.UTF8.GetByteCount(text);
                    stopped = true;
                }

                textBytes += bytes;
                if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(
                        new(
                            header.Id,
                            message.Id,
                            path,
                            message.Timestamp,
                            header.Cwd,
                            message.Message.Role.Value,
                            Snippet(text, query)
                        )
                    );
                }

                if (stopped)
                {
                    break;
                }
            }
        }
        catch (Exception exception)
            when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            if (header is null)
            {
                matches.Clear();
            }
        }

        return new(header, matches, malformed, textBytes, stopped);
    }

    /// <summary>
    /// The longest prefix of <paramref name="text"/> whose UTF-8 encoding fits in
    /// <paramref name="byteBudget"/> bytes, cut on a rune boundary so a multi-byte character is
    /// never split. A message larger than the remaining budget is searched only up to the bound.
    /// </summary>
    private static string TextWithinBudget(string text, long byteBudget)
    {
        if (byteBudget <= 0)
        {
            return string.Empty;
        }

        long used = 0;
        int characters = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            int length = rune.Utf8SequenceLength;
            if (used + length > byteBudget)
            {
                break;
            }

            used += length;
            characters += rune.Utf16SequenceLength;
        }

        return text[..characters];
    }

    /// <summary>The turn's text, for a user or assistant message only; null for every other entry.</summary>
    private static string? MessageText(SessionMessageEntry entry)
    {
        if (entry.Message.Role != ChatRole.User && entry.Message.Role != ChatRole.Assistant)
        {
            return null;
        }

        string text = entry.Message.Text ?? string.Empty;
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    /// A single-line excerpt around the first hit: <see cref="SearchSnippetBefore"/> characters
    /// before it, <see cref="SearchSnippetAfter"/> after it, whitespace collapsed and cut edges
    /// marked with an ellipsis. Invariant by construction: it slices the message, it never formats.
    /// </summary>
    private static string Snippet(string text, string query)
    {
        int index = Math.Max(0, text.IndexOf(query, StringComparison.OrdinalIgnoreCase));
        int start = Math.Max(0, index - SearchSnippetBefore);
        int end = Math.Min(text.Length, index + query.Length + SearchSnippetAfter);
        string excerpt = CollapseWhitespace(text[start..end]);
        string prefix = start > 0 ? "…" : string.Empty;
        string suffix = end < text.Length ? "…" : string.Empty;
        return prefix + excerpt + suffix;
    }

    /// <summary>One line for a message that may carry newlines and runs of blanks.</summary>
    private static string CollapseWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
