using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The wizard-shaped chat answer the model-resolution tests share: one provider-store entry with
/// an explicit endpoint, header and credential placement, and a <c>usages.chat</c> profile that
/// names it. The entry's values are what a frontend that resolves the profile must preserve.
/// </summary>
internal static class ChatUsageProfileTestSupport
{
    internal const string ModelId = "gpt-5";
    internal const string Endpoint = "https://gateway.example.test/v1";
    internal const string HeaderName = "x-gateway-project";
    internal const string HeaderValue = "lunate";
    internal const string CredentialPlacement = "header:x-gateway-key";
    internal const string LegacyModelId = "gpt-4o-mini";

    /// <summary>The settings file a wizard-configured install writes: a profile and a chat usage.</summary>
    internal static string SettingsJson(string? model = null) =>
        model is null
            ? $$"""
                {
                  "schemaVersion": 2,
                  "profiles": {
                    "main": {
                      "provider": "gateway",
                      "model": "{{ModelId}}",
                      "reasoningLevel": "high"
                    }
                  },
                  "usages": { "chat": "main" }
                }
                """
            : $$"""
                {
                  "schemaVersion": 2,
                  "model": "{{model}}",
                  "profiles": {
                    "main": {
                      "provider": "gateway",
                      "model": "{{ModelId}}",
                      "reasoningLevel": "high"
                    }
                  },
                  "usages": { "chat": "main" }
                }
                """;

    /// <summary>The same configuration as <see cref="SettingsJson"/>, for direct harness calls.</summary>
    internal static AgentSettings Settings(string? model = null) =>
        new()
        {
            Model = model,
            Profiles = new Dictionary<string, ModelProfile>(StringComparer.Ordinal)
            {
                ["main"] = new("main", "gateway", ModelId, "high"),
            },
            Usages = new Dictionary<string, string>(StringComparer.Ordinal) { ["chat"] = "main" },
        };

    internal static AgentSettings LegacySettings() => new() { Model = LegacyModelId };

    /// <summary>Writes the provider-store entry beside the auth file the run reads.</summary>
    internal static void SaveProvider(TempDirectory temp) =>
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(temp.File("auth.json")),
            [GatewayProvider()]
        );

    internal static ProviderEntry GatewayProvider()
    {
        bool valid = ProviderEntry.TryCreate(
            "gateway",
            "openai",
            new Uri(Endpoint),
            "auth:gateway",
            new Dictionary<string, string>(StringComparer.Ordinal) { [HeaderName] = HeaderValue },
            region: null,
            baseUrls: null,
            modelsUrl: null,
            credentialPlacements: new Dictionary<ProviderApiFamily, CredentialPlacement>
            {
                [ProviderApiFamily.OpenAiChat] = new("x-gateway-key"),
            },
            out ProviderEntry? provider,
            out string? error
        );
        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(provider);
    }
}
