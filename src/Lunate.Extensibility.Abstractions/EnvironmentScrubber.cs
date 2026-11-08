namespace Lunate.Extensibility.Abstractions;

/// <summary>
/// Which of the two environment policies a child spawn asks for. There is no default: every spawn
/// site names one at its call, so no child's environment is decided by an omission.
/// </summary>
public enum ChildEnvironmentPolicy
{
    /// <summary>
    /// The minimal base set plus the declared names — the extension hosts' policy, where nothing
    /// the parent holds is inherited implicitly.
    /// </summary>
    Scrubbed,

    /// <summary>
    /// The caller's whole environment minus the names the caller declares as Lunate's own
    /// credentials — the tool child's policy, because the bash tool is the user's shell and must
    /// still see the user's own ambient environment.
    /// </summary>
    Inherited,
}

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
    /// <item><c>PATHEXT</c> — Windows resolves a bare command through the extensions listed
    /// here, so a server starting <c>node</c>, <c>npm</c> or a <c>.cmd</c> helper needs it.</item>
    /// <item><c>HOME</c> (POSIX home), <c>USERPROFILE</c> (the Windows name of the same) — config
    /// and caches under the user's home directory.</item>
    /// <item><c>SystemRoot</c> — Windows requires it for process startup itself; without it a
    /// child on Windows fails to start.</item>
    /// <item><c>ComSpec</c> — the Windows command processor's path; a server's shell helper
    /// starts <c>cmd.exe</c> through it.</item>
    /// <item><c>TEMP</c> and <c>TMP</c> (the Windows/temp-variable spellings), <c>TMPDIR</c> (the
    /// POSIX spelling) — a child writing scratch files needs a directory it is allowed to write.</item>
    /// <item><c>APPDATA</c> and <c>LOCALAPPDATA</c> — where Windows applications keep their
    /// per-user roaming and local data; package managers and runtimes read them to find or
    /// create their caches.</item>
    /// <item><c>LANG</c> — the locale decides the child's output encoding, so its text parses.</item>
    /// </list>
    /// Everything the parent holds beyond these list is deliberately absent from the child —
    /// declare a variable in the caller's own environment map to pass one.
    /// </summary>
    public static IReadOnlyList<string> BaseNames { get; } =
    [
        "PATH",
        "PATHEXT",
        "HOME",
        "USERPROFILE",
        "SystemRoot",
        "ComSpec",
        "TEMP",
        "TMP",
        "TMPDIR",
        "APPDATA",
        "LOCALAPPDATA",
        "LANG",
    ];

    /// <summary>
    /// Builds a child environment under <paramref name="policy"/>. <see cref="ChildEnvironmentPolicy.Scrubbed"/>
    /// is the minimal base set, then the declared pass-through names, then the declared values; a
    /// name the parent holds and does not declare never arrives. <see cref="ChildEnvironmentPolicy.Inherited"/>
    /// is the parent's whole environment, then the declared values, minus every declared strip name
    /// (the credential variables Lunate resolved) — and then the declared pass-through names are
    /// re-admitted from the parent, so an exact name the user opted back in wins over the strip for
    /// that name alone. The strip still wins over a declared value, so a spawn site cannot
    /// re-introduce a stripped name by accident.
    /// </summary>
    /// <param name="policy">The policy; the call site always names one.</param>
    /// <param name="read">Reads one parent variable; null reads the process environment.</param>
    /// <param name="set">Values to set; null sets none.</param>
    /// <param name="passthrough">Names read from the parent: the whole allow-list under <see cref="ChildEnvironmentPolicy.Scrubbed"/>, and the names re-admitted after the strip under <see cref="ChildEnvironmentPolicy.Inherited"/>; null passes none through.</param>
    /// <param name="strip">Names removed under <see cref="ChildEnvironmentPolicy.Inherited"/>; null strips none.</param>
    /// <param name="inherited">
    /// The set <see cref="ChildEnvironmentPolicy.Inherited"/> starts from; null reads the process
    /// environment. The test seam for the inherited policy.
    /// </param>
    public static IReadOnlyDictionary<string, string> Build(
        ChildEnvironmentPolicy policy,
        Func<string, string?>? read = null,
        IReadOnlyDictionary<string, string>? set = null,
        IReadOnlyList<string>? passthrough = null,
        IReadOnlyList<string>? strip = null,
        IEnumerable<KeyValuePair<string, string>>? inherited = null
    )
    {
        read ??= Environment.GetEnvironmentVariable;
        // Windows environment names are case-insensitive: a declared name differing only in case
        // from a base or pass-through name must replace it, never duplicate it.
        var environment = new Dictionary<string, string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal
        );

        if (policy == ChildEnvironmentPolicy.Inherited)
        {
            foreach ((string name, string value) in inherited ?? ProcessEnvironment())
            {
                environment[name] = value;
            }

            // The pass-through is the user's explicit act against the credential strip: remember the
            // exact names' inherited values before the strip, so they can be re-admitted after it.
            List<KeyValuePair<string, string>> passedThrough = [];
            foreach (string name in passthrough ?? [])
            {
                if (environment.TryGetValue(name, out string? value))
                {
                    passedThrough.Add(new KeyValuePair<string, string>(name, value));
                }
            }

            foreach (string name in strip ?? [])
            {
                environment.Remove(name);
            }

            foreach ((string name, string value) in passedThrough)
            {
                environment[name] = value;
            }
        }
        else
        {
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
        }

        foreach ((string name, string value) in set ?? new Dictionary<string, string>())
        {
            // The inherited policy's strip is a deny for the whole child, so a declared value
            // cannot put a stripped name back.
            if (policy == ChildEnvironmentPolicy.Inherited && strip is { } stripped)
            {
                if (stripped.Contains(name, StringComparer.Ordinal))
                {
                    continue;
                }
            }

            environment[name] = value;
        }

        return environment;
    }

    /// <summary>The process environment as name/value pairs — the inherited policy's default source.</summary>
    private static IEnumerable<KeyValuePair<string, string>> ProcessEnvironment()
    {
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name)
            {
                yield return new KeyValuePair<string, string>(
                    name,
                    entry.Value as string ?? string.Empty
                );
            }
        }
    }
}
