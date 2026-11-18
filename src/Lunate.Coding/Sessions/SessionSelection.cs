using Lunate.Agent;

namespace Lunate.Coding;

/// <summary>
/// The launch-time session choice shared by the interactive and print frontends: path detection,
/// id lookup across the store and the newest-by-cwd query. The caller loads the chosen file and
/// owns how a failure is presented.
/// </summary>
internal static class SessionSelection
{
    /// <summary>The user profile's session store.</summary>
    internal static string DefaultRoot => Path.Combine(Lunate.Ai.LunateHome.Store, "sessions");

    /// <summary>The workspace path comparison: ordinal on Linux, ordinal-ignore-case elsewhere.</summary>
    internal static StringComparison PathComparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>A separator or the <c>.jsonl</c> suffix marks a path; any other value is an id.</summary>
    internal static bool IsPath(string value) =>
        value.Contains('/')
        || value.Contains('\\')
        || value.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);

    /// <summary>The grammar a caller-chosen session id must match, as the usage error spells it.</summary>
    internal const string IdRule =
        "1 to 128 characters from A-Z, a-z, 0-9, '.', '_' and '-', starting with a letter or digit and without '..'";

    /// <summary>
    /// Whether <paramref name="id"/> is a legal caller-chosen session id. It becomes a file name in
    /// the session directory, so it must be path-safe and must not read as a flag or a hidden file.
    /// </summary>
    internal static bool IsValidId(string id) =>
        id.Length is >= 1 and <= 128
        && char.IsAsciiLetterOrDigit(id[0])
        && id.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'
        )
        && !id.Contains("..", StringComparison.Ordinal);

    /// <summary>The error text for an id outside the grammar, or null when it is legal.</summary>
    internal static string? ValidateId(string id) =>
        IsValidId(id) ? null : $"option '--session-id' expects an id of {IdRule}, but was '{id}'";

    /// <summary>
    /// <c>--session-id &lt;id&gt;</c>: the file named by the id inside the working directory's own
    /// session directory. Unlike <c>--session &lt;id&gt;</c> the id is never searched across the
    /// store, so a same-named session in another worktree can neither be continued nor overwritten.
    /// </summary>
    internal static string IdPath(string sessionsDirectory, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionsDirectory);
        if (ValidateId(sessionId) is { } error)
        {
            throw new ArgumentException(error, nameof(sessionId));
        }

        // The store's own path-safety check stays the authority for the file name.
        return Path.Combine(sessionsDirectory, SessionPaths.SessionFileName(sessionId));
    }

    /// <summary><c>--session &lt;value&gt;</c>: the path, or an error naming the input.</summary>
    internal static SessionChoice FromValue(string sessionsRoot, string value)
    {
        if (IsPath(value))
        {
            return new SessionChoice(value, value, null);
        }

        IReadOnlyList<SessionSummary> matches = Session.FindById(sessionsRoot, value);
        if (matches.Count == 0)
        {
            return new SessionChoice(
                null,
                value,
                $"no session with id '{value}'; run a prompt to start one"
            );
        }

        if (matches.Count > 1)
        {
            return new SessionChoice(
                null,
                value,
                $"session id '{value}' is ambiguous: {string.Join(", ", matches.Select(match => match.Path))}"
            );
        }

        return new SessionChoice(matches[0].Path, value, null);
    }

    /// <summary>
    /// <c>--continue</c>: the newest session whose header recorded <paramref name="cwd"/>, or an
    /// error naming the directory.
    /// </summary>
    internal static SessionChoice Continue(string sessionsRoot, string cwd) =>
        Session.FindNewest(sessionsRoot, cwd, PathComparison) is { } newest
            ? new SessionChoice(newest.Path, newest.Id, null)
            : new SessionChoice(null, cwd, $"no session for '{cwd}'; run a prompt to start one");
}

/// <summary>A resolved session file and the input label, or the error to report instead.</summary>
internal sealed record SessionChoice(string? Path, string Label, string? Error);
