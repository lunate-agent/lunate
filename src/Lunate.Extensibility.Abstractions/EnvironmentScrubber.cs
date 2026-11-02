namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// Builds a child process environment that can never inherit Lunate's own variables by omission:
/// the minimal base set (see <see cref="BaseNames"/>, each entry's reason documented there), plus
/// the declared pass-through names, plus the declared values — nothing else. The one
/// implementation shared by every child spawn: the MCP stdio host (#472), the code-mode sample and
/// the remote extension host.
/// </summary>
public static class EnvironmentScrubber
{
    /// <summary>
    /// The minimal base names every scrubbed child receives when the parent has them, each with
    /// its reason (<c>read</c>'s names; only the parent's values are used, never defaults):
    /// <list type="bullet">
    /// <item><c>PATH</c> — the child must find its own commands and libraries.</item>
    /// <item><c>HOME</c> (POSIX home), <c>USERPROFILE</c> (the Windows name of the same) — config
    /// and caches under the user's home directory.</item>
    /// <item><c>SystemRoot</c> — Windows requires it for process startup itself; without it a
    /// child on Windows fails to start.</item>
    /// <item><c>TEMP</c> and <c>TMP</c> (the Windows/temp-variable spellings), <c>TMPDIR</c> (the
    /// POSIX spelling) — a child writing scratch files needs a directory it is allowed to write.</item>
    /// <item><c>LANG</c> — the locale decides the child's output encoding, so its text parses.</item>
    /// </list>
    /// Everything the parent holds beyond these list is deliberately absent from the child —
    /// declare a variable in the caller's own environment map to pass one.
    /// </summary>
    public static IReadOnlyList<string> BaseNames { get; } =
    ["PATH", "HOME", "USERPROFILE", "SystemRoot", "TEMP", "TMP", "TMPDIR", "LANG"];

    /// <summary>
    /// Builds the exact environment: each base name present in the parent (read through
    /// <paramref name="read"/>, case-insensitively on Windows), each pass-through name, then the
    /// explicit values (which win).
    /// </summary>
    /// <param name="read">Reads the parent's environment; null reads the process environment.</param>
    /// <param name="set">Values to set; null sets none.</param>
    /// <param name="passthrough">Names inherited from the parent; null passes none through.</param>
    public static IReadOnlyDictionary<string, string> Build(
        Func<string, string?>? read = null,
        IReadOnlyDictionary<string, string>? set = null,
        IReadOnlyList<string>? passthrough = null
    )
    {
        read ??= Environment.GetEnvironmentVariable;
        // Windows environment names are case-insensitive: a declared name differing only in case
        // from a base or pass-through name must replace it, never duplicate it.
        var environment = new Dictionary<string, string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
        );
        foreach (string name in BaseNames)
        {
            if (read(name) is { Length: > 0 } value)
            {
                environment[name] = value;
            }
        }

        foreach (string name in passthrough ?? [])
        {
            if (read(name) is { } value)
            {
                environment[name] = value;
            }
        }

        foreach ((string name, string value) in set ?? new Dictionary<string, string>())
        {
            environment[name] = value;
        }

        return environment;
    }
}
