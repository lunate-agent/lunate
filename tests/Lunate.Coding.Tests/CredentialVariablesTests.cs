using System.Text.Json;
using System.Text.Json.Nodes;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The strip list the inherited child-environment policy removes: every built-in preset's key
/// variable and alias, plus each configured provider's <c>env:</c> reference, registered where the
/// provider store loads. Tests clear the registry through <see cref="CredentialVariables.ResetForTests"/>,
/// because it is process state by design.
/// </summary>
public sealed class CredentialVariablesTests
{
    [Fact]
    public void Every_builtin_preset_key_variable_and_alias_is_in_the_list()
    {
        CredentialVariables.ResetForTests();

        IReadOnlyList<string> names = CredentialVariables.All;

        foreach (ProviderPreset preset in ProviderPresets.All)
        {
            Assert.Contains(preset.KeyVariable, names);
            if (preset.KeyVariableAlias is { Length: > 0 } alias)
            {
                Assert.Contains(alias, names);
            }
        }

        // The point of the list is coverage, not the active provider: a name Lunate resolves is a
        // name a model-chosen command must not see, whichever provider the session uses.
        Assert.Contains("OPENAI_API_KEY", names);
    }

    [Theory]
    [InlineData("GITHUB_TOKEN", true)]
    [InlineData("AWS_SECRET_ACCESS_KEY", true)]
    [InlineData("DATABASE_PASSWORD", true)]
    [InlineData("OPENSSH_KEY", true)]
    [InlineData("github_token", true)]
    [InlineData("TOKEN", true)]
    [InlineData("MONKEY", false)]
    [InlineData("GITLAB_TOKENIZE_TIMEOUT", false)]
    [InlineData("KEYBOARD_LAYOUT", false)]
    [InlineData("PATH", false)]
    public void The_segment_rule_matches_whole_underscore_segments_only(string name, bool stripped)
    {
        // MONKEY and GITLAB_TOKENIZE_TIMEOUT are the cases a substring test gets wrong; the rule is
        // about a word in the name, not a run of characters inside one.
        Assert.Equal(stripped, CredentialVariables.IsSecretName(name));
    }

    [Fact]
    public void A_custom_providers_env_reference_is_registered_when_the_store_loads()
    {
        CredentialVariables.ResetForTests();
        try
        {
            using var temp = new TempDirectory();
            string providersPath = temp.File("providers.json");
            File.WriteAllText(
                providersPath,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["providers"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "gateway",
                            ["kind"] = "openai",
                            ["baseUrl"] = "https://gateway.example.test/v1",
                            ["credentialReference"] = "env:MY_GATEWAY_KEY",
                            ["headers"] = new JsonObject { ["x-tenant"] = "env:MY_TENANT_KEY" },
                        },
                    },
                }.ToJsonString()
            );
            AuthStore auth = AuthStore.Load(temp.File("auth.json"), _ => null);
            AgentSettings settings = SettingsStore.Resolve(temp.File("settings.json"), _ => null);

            _ = ProviderStore.Load(providersPath, auth, settings.Providers);

            Assert.Contains("MY_GATEWAY_KEY", CredentialVariables.All);
            Assert.Contains("MY_TENANT_KEY", CredentialVariables.All);
        }
        finally
        {
            CredentialVariables.ResetForTests();
        }
    }

    [Fact]
    public void Only_env_references_register_and_blank_names_never_do()
    {
        CredentialVariables.ResetForTests();
        try
        {
            CredentialVariables.RegisterReference("auth:openai");
            CredentialVariables.RegisterReference("helper:lunate-key openai");
            CredentialVariables.RegisterReference(null);
            CredentialVariables.RegisterReference("env:   ");

            Assert.DoesNotContain("openai", CredentialVariables.All);
            // Only the presets' own names: the base list, with no registration added.
            Assert.Equal(
                ProviderPresets
                    .All.SelectMany(preset =>
                        preset.KeyVariableAlias is { Length: > 0 } alias
                            ? new[] { preset.KeyVariable, alias }
                            : [preset.KeyVariable]
                    )
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                CredentialVariables.All.Count
            );
        }
        finally
        {
            CredentialVariables.ResetForTests();
        }
    }
}
