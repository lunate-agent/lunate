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
    /// The caller's whole environment minus the credentials the policy denies — the names the
    /// caller declares as Lunate's own credentials and every name shaped like one — the tool
    /// child's policy, because the bash tool is the user's shell and must still see the user's own
    /// ambient environment.
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
    /// The words that mark an inherited name as credential-shaped. A name is split on its
    /// <c>_</c> and <c>-</c> separators and every part is compared against these, case-insensitively:
    /// <c>GITHUB_TOKEN</c>, <c>NPM_TOKEN</c>, <c>AWS_SECRET_ACCESS_KEY</c> and
    /// <c>OPENAI_API_KEY_BACKUP</c> are withheld, while <c>MONKEY</c>, <c>KEYBOARD_LAYOUT</c>,
    /// <c>HOCKEY_POOL</c>, <c>GITLAB_TOKENIZE_TIMEOUT</c> and <c>OPENAI_BASE_URL</c> are not.
    /// <c>AUTH</c>, <c>PWD</c>, <c>URL</c>, <c>URI</c> and <c>DSN</c> are deliberately absent:
    /// <c>SSH_AUTH_SOCK</c>, <c>PWD</c>, <c>OPENAI_BASE_URL</c> and <c>NEXT_PUBLIC_*_URL</c> are
    /// ambient names toolchains read, and a URL's name says nothing about whether it carries a
    /// password. The shape rule is a heuristic, not a guarantee — <c>DATABASE_URL</c> and
    /// <c>PGPASSWORD</c> still reach the child (ADR-0036).
    /// </summary>
    private static readonly HashSet<string> CredentialWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "KEY",
        "KEYS",
        "TOKEN",
        "TOKENS",
        "SECRET",
        "SECRETS",
        "PASSWORD",
        "PASSWORDS",
        "PASSWD",
        "PASS",
        "CREDENTIAL",
        "CREDENTIALS",
        "CREDS",
    };

    /// <summary>The pieces of a variable name: <c>_</c> and <c>-</c> separate words.</summary>
    private static readonly char[] NameSeparators = ['_', '-'];

    /// <summary>
    /// Builds a child environment under <paramref name="policy"/>. <see cref="ChildEnvironmentPolicy.Scrubbed"/>
    /// is the minimal base set, then the declared pass-through names, then the declared values; a
    /// name the parent holds and does not declare never arrives. <see cref="ChildEnvironmentPolicy.Inherited"/>
    /// is the parent's whole environment, then the declared values, minus two denies: every declared
    /// strip name (the credential variables Lunate resolved) and every name shaped like a credential
    /// (<see cref="CredentialWords"/>) — and then the declared pass-through names are re-admitted from
    /// the parent, so an exact name the user opted back in wins over both denies for that name alone.
    /// The denies still win over a declared value, so a spawn site cannot re-introduce a withheld
    /// name by accident.
    /// </summary>
    /// <param name="policy">The policy; the call site always names one.</param>
    /// <param name="read">Reads one parent variable; null reads the process environment.</param>
    /// <param name="set">Values to set; null sets none.</param>
    /// <param name="passthrough">Names read from the parent: the whole allow-list under <see cref="ChildEnvironmentPolicy.Scrubbed"/>, and the names re-admitted after both denies under <see cref="ChildEnvironmentPolicy.Inherited"/>; null passes none through.</param>
    /// <param name="strip">Names removed under <see cref="ChildEnvironmentPolicy.Inherited"/> beside the credential-shape rule; null strips none.</param>
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

            // The pass-through is the user's explicit act against the credential denies: remember
            // the exact names' inherited values before them, so they can be re-admitted after.
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

            // The shape deny is the default for a name Lunate never resolved: a developer's
            // GITHUB_TOKEN or AWS_SECRET_ACCESS_KEY is the same leak whether Lunate holds the name
            // or not. It is a word match, so a name that merely contains a credential word survives
            // (ADR-0036).
            foreach (string name in environment.Keys.ToArray())
            {
                if (IsCredentialShaped(name))
                {
                    environment.Remove(name);
                }
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
            // The inherited policy's denies are a deny for the whole child, so a declared value
            // cannot put a stripped or credential-shaped name back.
            if (policy == ChildEnvironmentPolicy.Inherited)
            {
                if (strip is { } stripped && stripped.Contains(name, StringComparer.Ordinal))
                {
                    continue;
                }

                if (IsCredentialShaped(name))
                {
                    continue;
                }
            }

            environment[name] = value;
        }

        return environment;
    }

    /// <summary>
    /// Whether a variable name is shaped like a credential: one of its <c>_</c>- or
    /// <c>-</c>-separated parts is a <see cref="CredentialWords">credential word</see>, compared
    /// case-insensitively. A substring match is deliberately not used: <c>MONKEY</c> and
    /// <c>KEYBOARD_LAYOUT</c> are the user's own variables.
    /// </summary>
    private static bool IsCredentialShaped(string name)
    {
        foreach (string part in name.Split(NameSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (CredentialWords.Contains(part))
            {
                return true;
            }
        }

        return false;
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
