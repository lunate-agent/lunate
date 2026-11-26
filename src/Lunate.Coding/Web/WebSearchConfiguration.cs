namespace Lunate.Coding;

/// <summary>
/// The web search configuration one frontend hands the tool registry: the resolved
/// <c>tools.webSearch</c> settings, the auth store's named-key lookup and the environment lookup a
/// backend's <c>env:</c> reference resolves from. Building the registry registers the reference in
/// <see cref="CredentialVariables"/>, the strip list the inherited child-environment policy removes,
/// exactly as a configured provider's reference is registered when the provider store loads.
/// </summary>
internal sealed record WebSearchConfiguration(
    WebSearchSettings? Settings = null,
    Func<string, string?>? NamedKeySource = null,
    Func<string, string?>? EnvironmentLookup = null
)
{
    /// <summary>The settings with the documented defaults applied.</summary>
    internal WebSearchSettings Resolved => Settings ?? new WebSearchSettings();

    /// <summary>The environment the credential and the settings resolve from.</summary>
    internal Func<string, string?> Environment =>
        EnvironmentLookup ?? System.Environment.GetEnvironmentVariable;

    /// <summary>Registers an <c>env:</c> credential reference with the inherited-policy strip list.</summary>
    internal void RegisterCredential()
    {
        CredentialVariables.RegisterReference(Resolved.Credential);
    }
}
