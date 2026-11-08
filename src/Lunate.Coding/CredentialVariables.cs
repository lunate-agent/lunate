using Lunate.Ai;

namespace Lunate.Coding;

/// <summary>
/// The environment variable names Lunate resolves as provider credentials: every built-in preset's
/// key variable and key alias (from <c>providers.json</c>), plus every configured custom provider's
/// <c>env:</c> credential reference, registered when the provider store loads. The list is a
/// superset by design — the reason a model-chosen command must not see a name is that Lunate
/// resolves it as a key, whichever provider the session happens to use.
/// </summary>
/// <remarks>
/// It is a strip list, not a pattern: <c>OPENAI_API_KEY_BACKUP</c> is the user's own variable and
/// survives, because Lunate never resolved it. Registration only ever adds names, and a stale name
/// strips nothing that is not there.
/// </remarks>
internal static class CredentialVariables
{
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
