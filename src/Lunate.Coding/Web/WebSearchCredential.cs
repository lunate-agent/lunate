namespace Lunate.Coding;

/// <summary>
/// The send-time resolution of a <c>tools.webSearch.credential</c> reference. The setting holds a
/// provider-form reference (<c>auth:</c>, <c>env:</c>, <c>helper:</c>), never a value; this resolves
/// it from the same sources provider credentials come from — the auth store's named keys and the
/// environment — and returns an actionable error, not an exception, when it cannot. A
/// <c>helper:</c> reference is accepted by the setting and refused here, exactly as a provider
/// header's helper reference is.
/// </summary>
internal static class WebSearchCredential
{
    internal const string SettingKey = "tools.webSearch.credential";

    /// <summary>
    /// Resolves one reference. <paramref name="value"/> is null exactly when
    /// <paramref name="error"/> is set, and a resolved blank value is an error, never an empty
    /// credential sent to the backend.
    /// </summary>
    internal static bool TryResolve(
        string reference,
        Func<string, string?>? namedKeySource,
        Func<string, string?> environment,
        out string? value,
        out string? error
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentNullException.ThrowIfNull(environment);
        value = null;
        error = null;

        int separator = reference.IndexOf(':', StringComparison.Ordinal);
        string source = separator <= 0 ? string.Empty : reference[..separator];
        string target = separator <= 0 ? string.Empty : reference[(separator + 1)..];
        if (source is not ("auth" or "env" or "helper"))
        {
            // The setting is validated when it loads; this arm covers a directly constructed
            // configuration, where the value is not a reference at all.
            error =
                $"'{SettingKey}' must be an auth:<name>, env:<VARIABLE> or helper:<name> reference, "
                + "never a literal key; fix the setting and try again.";
            return false;
        }

        if (source == "helper")
        {
            error =
                $"the web search credential reference 'helper:{target}' in '{SettingKey}' cannot be "
                + "resolved by this host; use an 'auth:<name>' or 'env:<VARIABLE>' reference, then try again.";
            return false;
        }

        string? resolved = source == "auth" ? namedKeySource?.Invoke(target) : environment(target);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            string owner =
                source == "auth"
                    ? $"the auth-store key '{target}' is not set; add it to auth.json"
                    : $"the environment variable {target} is not set";
            error =
                $"the web search credential reference '{reference}' in '{SettingKey}' cannot be "
                + $"resolved: {owner}. Set it, or point '{SettingKey}' at another reference, then try again.";
            return false;
        }

        value = resolved;
        return true;
    }
}
