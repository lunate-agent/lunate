using System.Text.Json;
using Lunate.Agent;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The profile writers' half of the settings contract: a profile is merged into, never replaced
/// (S-29), and the two mutually exclusive reasoning keys never both reach the file (S-28) — so a
/// value a `/settings` command did not name survives, and the next load still resolves.
/// </summary>
public sealed class SettingsStoreProfileMergeTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string SettingsPath => _temp.File("settings.json");
    private string ModelsPath => _temp.File("models.json");

    [Fact]
    public void WriteProfile_replaces_only_the_keys_the_wizard_owns()
    {
        File.WriteAllText(
            SettingsPath,
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "main": {
                  "provider": "openai",
                  "model": "gpt-4o",
                  "reasoningLevel": "low",
                  "maxOutput": 4096,
                  "temperature": 0.4,
                  "contextWindow": 64000,
                  "capabilities": { "inputModalities": ["text", "image"] }
                }
              }
            }
            """
        );

        SettingsStore.WriteProfile(
            new ModelProfile("main", "openai", "gpt-5", Description: "everyday work"),
            SettingsPath
        );

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        JsonElement profile = settings.RootElement.GetProperty("profiles").GetProperty("main");
        Assert.Equal("gpt-5", profile.GetProperty("model").GetString());
        Assert.Equal("everyday work", profile.GetProperty("description").GetString());
        Assert.Equal(4096, profile.GetProperty("maxOutput").GetInt32());
        Assert.Equal(0.4m, profile.GetProperty("temperature").GetDecimal());
        Assert.Equal(64000, profile.GetProperty("contextWindow").GetInt32());
        Assert.Equal(
            "image",
            profile.GetProperty("capabilities").GetProperty("inputModalities")[1].GetString()
        );
        // The walk carries no level for this profile: the key it owns is removed, not left stale.
        Assert.False(profile.TryGetProperty("reasoningLevel", out _));
    }

    [Fact]
    public void WriteProfile_drops_an_inherited_budget_when_it_sets_a_level()
    {
        // The merge must not recreate the pair resolution refuses.
        File.WriteAllText(
            SettingsPath,
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "main": {
                  "provider": "openai",
                  "model": "budget-model",
                  "reasoningBudget": 2048,
                  "maxOutput": 4096
                }
              }
            }
            """
        );

        SettingsStore.WriteProfile(
            new ModelProfile("main", "openai", "budget-model", ReasoningLevel: "high"),
            SettingsPath
        );

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        JsonElement profile = settings.RootElement.GetProperty("profiles").GetProperty("main");
        Assert.Equal("high", profile.GetProperty("reasoningLevel").GetString());
        Assert.False(profile.TryGetProperty("reasoningBudget", out _));
        Assert.Equal(4096, profile.GetProperty("maxOutput").GetInt32());
    }

    [Fact]
    public void The_thinking_write_removes_a_budget_and_the_next_resolution_succeeds()
    {
        SeedBudgetModel();
        File.WriteAllText(
            SettingsPath,
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "main": {
                  "provider": "openai",
                  "model": "budget-model",
                  "reasoningBudget": 2048
                }
              },
              "usages": { "chat": "main" }
            }
            """
        );

        string? problem = SettingsStore.WriteProfileReasoningLevel(
            "main",
            ThinkingLevel.High,
            out bool removedBudget,
            SettingsPath
        );

        Assert.Null(problem);
        Assert.True(removedBudget, "the conflicting budget is removed, not left beside the level");
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        JsonElement profile = settings.RootElement.GetProperty("profiles").GetProperty("main");
        Assert.Equal("high", profile.GetProperty("reasoningLevel").GetString());
        Assert.False(profile.TryGetProperty("reasoningBudget", out _));

        // The next start: the file loads and the profile resolves — the failure S-28 reported.
        AgentSettings loaded = SettingsStore.Resolve(SettingsPath, _ => null);
        ResolvedModelProfile resolved = ModelProfileResolver.Resolve(
            loaded.Profiles["main"],
            [BudgetProvider()],
            ModelCatalog.Load(ModelsPath)
        );
        Assert.Equal("high", resolved.Profile.ReasoningLevel);
        Assert.Null(resolved.Profile.ReasoningBudget);
    }

    [Fact]
    public void The_thinking_write_keeps_an_unrelated_profile_value()
    {
        SeedBudgetModel();
        File.WriteAllText(
            SettingsPath,
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "main": {
                  "provider": "openai",
                  "model": "budget-model",
                  "reasoningLevel": "low",
                  "maxOutput": 2048
                }
              }
            }
            """
        );

        string? problem = SettingsStore.WriteProfileReasoningLevel(
            "main",
            ThinkingLevel.Medium,
            out bool removedBudget,
            SettingsPath
        );

        Assert.Null(problem);
        Assert.False(removedBudget);
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        JsonElement profile = settings.RootElement.GetProperty("profiles").GetProperty("main");
        Assert.Equal("medium", profile.GetProperty("reasoningLevel").GetString());
        Assert.Equal(2048, profile.GetProperty("maxOutput").GetInt32());
    }

    private void SeedBudgetModel() =>
        File.WriteAllText(
            ModelsPath,
            """
            {
              "schemaVersion": 1,
              "models": [
                { "id": "budget-model", "provider": "openai", "endpoint": null,
                  "contextWindow": 128000, "supportsTools": true, "reasoning": "budget",
                  "maxOutput": 8192 }
              ]
            }
            """
        );

    private static ProviderEntry BudgetProvider()
    {
        bool valid = ProviderEntry.TryCreate(
            "openai",
            "openai",
            new Uri("https://api.openai.com/v1"),
            "auth:openai",
            headers: null,
            region: null,
            out ProviderEntry? provider,
            out string? error
        );
        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(provider);
    }
}
