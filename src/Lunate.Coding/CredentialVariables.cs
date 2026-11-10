using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The environment variable names Lunate resolves as provider credentials: every built-in preset's
/// key variable and key alias (from <c>providers.json</c>), plus every configured custom provider's
/// <c>env:</c> credential reference, registered when the provider store loads. The list is a
/// superset by design — the reason a model-chosen command must not see a name is that Lunate
/// resolves it as a key, whichever provider the session happens to use. Beside the resolved names,
/// <see cref="IsSecretName"/> describes the shape of a credential name, which covers the credential
/// Lunate never resolved (#741).
/// </summary>
/// <remarks>
/// Two rules, one purpose. <see cref="All"/> names what Lunate resolved; a name the user's machine
/// set for another application — <c>GITHUB_TOKEN</c>, <c>AWS_SECRET_ACCESS_KEY</c> — is not there,
/// so the shape rule is what removes it from a tool child by default.
/// <para>
/// The shape rule matches <b>whole underscore-separated segments</b>, never substrings. A substring
/// test is the wrong rule: it strips <c>MONKEY</c> (its one segment is <c>MONKEY</c>, not
/// <c>KEY</c>) and <c>GITLAB_TOKENIZE_TIMEOUT</c> (<c>TOKENIZE</c> is not <c>TOKEN</c>), and a build
/// that breaks because a variable contained a syllable is a worse failure than the leak it
/// prevents. Matching by segment makes the rule about a word in the name, not a run of characters
/// inside one.
/// </para>
/// The tool child's <c>toolEnvironment.passthrough</c> list outranks both rules for an exact name —
/// that list is the user's explicit act, which is why it wins. Registration only ever adds names,
/// and a stale name strips nothing that is not there.
/// </remarks>
internal static class CredentialVariables
{
    /// <summary>
    /// The name segments that mark a variable as a credential. Compared case-insensitively, because
    /// a name's spelling is the platform's and not ours: <c>github_token</c> is the same credential
    /// as <c>GITHUB_TOKEN</c>.
    /// </summary>
    private static readonly HashSet<string> SecretSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "KEY",
        "TOKEN",
        "SECRET",
        "PASSWORD",
    };

    private static readonly HashSet<string> Registered = new(StringComparer.Ordinal);

    // Two presets can share a key variable (OpenCode and OpenCode Go both read
    // OPENCODE_API_KEY), so the list is a set: a strip list names a name once.
    private static readonly IReadOnlyList<string> PresetNames =
    [
        .. ProviderPresets
            .All.SelectMany(preset =>
                preset.KeyVariableAlias is { Length: > 0 } alias
                    ? new[] { preset.KeyVariable, alias }
                    : [preset.KeyVariable]
            )
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>Every name the inherited child-environment policy removes.</summary>
    public static IReadOnlyList<string> All
    {
        get
        {
            lock (Registered)
            {
                return Registered.Count == 0 ? PresetNames : [.. PresetNames, .. Registered];
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="name"/> carries a credential-shaped name segment: one of its
    /// underscore-separated segments is <c>KEY</c>, <c>TOKEN</c>, <c>SECRET</c> or
    /// <c>PASSWORD</c>, compared case-insensitively. The tool child's inherited policy strips such a
    /// variable by default; its pass-through list re-admits an exact name.
    /// </summary>
    /// <remarks>
    /// A name with no underscore is its own single segment, so <c>MONKEY</c> is kept and a bare
    /// <c>TOKEN</c> is stripped. This is deliberately not a substring test — see the type's remarks
    /// for the false positives that makes it wrong.
    /// </remarks>
    public static bool IsSecretName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Split('_').Any(SecretSegments.Contains);
    }

    /// <summary>
    /// Registers a name a configured provider resolves as a credential (an <c>env:</c> reference).
    /// A blank or malformed reference registers nothing; the store validates references before this
    /// is called, so the check is a guard against a caller passing the raw reference.
    /// </summary>
    public static void Register(string? variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return;
        }

        lock (Registered)
        {
            Registered.Add(variableName);
        }
    }

    /// <summary>Registers every <c>env:</c> name in a credential reference or provider header value.</summary>
    public static void RegisterReference(string? reference)
    {
        const string prefix = "env:";
        if (reference is null || !reference.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        Register(reference[prefix.Length..].Trim());
    }

    /// <summary>Clears the registered names; the process-wide list is what tests must not inherit.</summary>
    internal static void ResetForTests()
    {
        lock (Registered)
        {
            Registered.Clear();
        }
    }
}
