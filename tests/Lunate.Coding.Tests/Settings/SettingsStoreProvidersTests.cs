using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

public sealed class SettingsStoreProvidersTests
{
    [Fact]
    public void Resolve_reads_the_providers_section()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "local": {
                  "baseUrl": "http://127.0.0.1:11434/v1",
                  "model": "qwen2.5-coder",
                  "catalogName": "local/qwen2.5-coder"
                }
              }
            }
            """
        );

        AgentSettings settings = SettingsStore.Resolve(path, _ => null);

        CustomProvider provider = Assert.Single(settings.Providers);
        Assert.Equal("local", provider.Id);
        Assert.Equal(new Uri("http://127.0.0.1:11434/v1"), provider.BaseUrl);
        Assert.Equal("qwen2.5-coder", provider.Model);
        Assert.Equal("local/qwen2.5-coder", provider.CatalogName);
    }

    [Fact]
    public void A_missing_catalog_name_defaults_to_id_slash_model()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "local": {
                  "baseUrl": "https://example.com/v1",
                  "model": "qwen2.5-coder"
                }
              }
            }
            """
        );

        AgentSettings settings = SettingsStore.Resolve(path, _ => null);

        Assert.Equal("local/qwen2.5-coder", Assert.Single(settings.Providers).CatalogName);
    }

    [Fact]
    public void An_invalid_scheme_is_reported_without_echoing_the_url()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "local": {
                  "baseUrl": "ftp://secret.example/v1",
                  "model": "qwen2.5-coder"
                }
              }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(path, _ => null)
        );

        Assert.Contains("providers", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ftp", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret.example", exception.Message, StringComparison.Ordinal);
        Assert.Contains("No settings were applied", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_model_is_reported_without_echoing_the_url()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "local": {
                  "baseUrl": "https://example.com/v1"
                }
              }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(path, _ => null)
        );

        Assert.Contains("model id", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_provider_field_is_reported_by_name()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "local": {
                  "baseUrl": "https://example.com/v1",
                  "model": "qwen2.5-coder",
                  "token": "refused"
                }
              }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(path, _ => null)
        );

        Assert.Contains(
            "unknown key 'providers.local.token'",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void WriteProvider_preserves_unrelated_keys_and_schema_2()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 1,
              "model": "gpt-4o-mini",
              "approval": "auto-edit",
              "providers": {
                "old": {
                  "baseUrl": "https://old.example/v1",
                  "model": "old-model",
                  "catalogName": "old/old-model"
                }
              }
            }
            """
        );
        var provider = new CustomProvider(
            "local",
            new Uri("http://127.0.0.1:11434/v1"),
            "qwen2.5-coder",
            "local/qwen2.5-coder"
        );

        SettingsStore.WriteProvider(provider, path);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("gpt-4o-mini", root.GetProperty("model").GetString());
        Assert.Equal("auto-edit", root.GetProperty("approval").GetString());
        JsonElement providers = root.GetProperty("providers");
        // A legacy entry is preserved verbatim: reading never rewrites a user file.
        Assert.Equal(
            "https://old.example/v1",
            providers.GetProperty("old").GetProperty("baseUrl").GetString()
        );
        JsonElement entry = providers.GetProperty("local");
        Assert.Equal(
            "http://127.0.0.1:11434/v1",
            entry.GetProperty("baseUrls").GetProperty("openai-chat").GetString()
        );
        Assert.Equal("qwen2.5-coder", entry.GetProperty("model").GetString());
        Assert.Equal("local/qwen2.5-coder", entry.GetProperty("catalogName").GetString());
        Assert.False(entry.TryGetProperty("baseUrl", out _));
    }

    [Fact]
    public void WriteProvider_writes_the_chosen_family_placement_and_models_url()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        var provider = new CustomProvider(
            "relay",
            new Uri("https://relay.example/v1"),
            "claude-wire",
            "relay/claude-wire",
            ProviderApiFamily.AnthropicMessages,
            CredentialPlacement.XApiKey,
            new Uri("https://relay.example/v1/models")
        );

        SettingsStore.WriteProvider(provider, path);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement entry = document.RootElement.GetProperty("providers").GetProperty("relay");
        Assert.Equal(
            "https://relay.example/v1",
            entry.GetProperty("baseUrls").GetProperty("anthropic-messages").GetString()
        );
        Assert.Equal("x-api-key", entry.GetProperty("credentialPlacement").GetString());
        Assert.Equal("https://relay.example/v1/models", entry.GetProperty("modelsUrl").GetString());
        Assert.False(entry.TryGetProperty("baseUrl", out _));
    }

    [Fact]
    public void WriteProvider_round_trips_family_placement_and_models_url_through_resolve()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        var provider = new CustomProvider(
            "relay",
            new Uri("https://relay.example/v1"),
            "claude-wire",
            "relay/claude-wire",
            ProviderApiFamily.AnthropicMessages,
            CredentialPlacement.XApiKey,
            new Uri("https://relay.example/v1/models")
        );

        SettingsStore.WriteProvider(provider, path);

        AgentSettings settings = SettingsStore.Resolve(path, _ => null);
        Assert.Equal(provider, Assert.Single(settings.Providers));
    }

    [Fact]
    public void Resolve_reads_a_hand_written_v2_entry_with_family_placement_and_headers()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "relay": {
                  "baseUrls": { "anthropic-messages": "https://relay.example/v1" },
                  "credentialPlacement": { "in": "header", "name": "x-api-key" },
                  "modelsUrl": "https://relay.example/v1/models",
                  "headers": { "x-title": "Lunate" },
                  "model": "claude-wire",
                  "catalogName": "relay/claude-wire"
                }
              }
            }
            """
        );

        AgentSettings settings = SettingsStore.Resolve(path, _ => null);

        CustomProvider provider = Assert.Single(settings.Providers);
        Assert.Equal(ProviderApiFamily.AnthropicMessages, provider.ApiFamily);
        Assert.Equal(new Uri("https://relay.example/v1"), provider.BaseUrl);
        Assert.Equal(CredentialPlacement.XApiKey, provider.CredentialPlacement);
        Assert.Equal(new Uri("https://relay.example/v1/models"), provider.ModelsUrl);
        Assert.Equal("Lunate", provider.Headers!["x-title"]);
        ModelInfo model = provider.ToModelInfo();
        Assert.Equal("anthropic", model.Provider);
        Assert.Equal("anthropic-messages", model.ApiFamily);
        Assert.Equal("x-api-key", model.CredentialPlacement);
        Assert.Equal("Lunate", model.ProviderHeaders!["x-title"]);
    }

    [Fact]
    public void An_unknown_family_in_baseUrls_is_refused_by_name()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "relay": {
                  "baseUrls": { "openai-realtime": "https://relay.example/v1" },
                  "model": "m"
                }
              }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(path, _ => null)
        );

        Assert.Contains("openai-realtime", exception.Message, StringComparison.Ordinal);
        Assert.Contains("supported families", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_custom_entry_with_two_families_is_refused()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "relay": {
                  "baseUrls": {
                    "openai-chat": "https://relay.example/v1",
                    "anthropic-messages": "https://relay.example/anthropic"
                  },
                  "model": "m"
                }
              }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(path, _ => null)
        );

        Assert.Contains("one API family", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_legacy_baseUrl_entry_loads_as_the_openai_chat_family()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "local": {
                  "baseUrl": "http://127.0.0.1:11434/v1",
                  "model": "qwen2.5-coder"
                }
              }
            }
            """
        );

        AgentSettings settings = SettingsStore.Resolve(path, _ => null);

        CustomProvider provider = Assert.Single(settings.Providers);
        Assert.Equal(ProviderApiFamily.OpenAiChat, provider.ApiFamily);
        Assert.Equal("openai-chat", provider.ToModelInfo().ApiFamily);
    }

    [Fact]
    public void An_unusable_header_value_in_a_custom_entry_is_refused_by_name()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        File.WriteAllText(
            path,
            """
            {
              "schemaVersion": 2,
              "providers": {
                "relay": {
                  "baseUrl": "https://relay.example/v1",
                  "model": "m",
                  "headers": { "x-session": "{credential}" }
                }
              }
            }
            """
        );

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            SettingsStore.Resolve(path, _ => null)
        );

        Assert.Contains("x-session", exception.Message, StringComparison.Ordinal);
        Assert.Contains("credential", exception.Message, StringComparison.Ordinal);
        Assert.Contains("cannot carry it", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteProvider_leaves_no_temp_file_and_resolves_back()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        var provider = new CustomProvider(
            "local",
            new Uri("https://example.com/v1"),
            "qwen2.5-coder",
            "local/qwen2.5-coder"
        );

        SettingsStore.WriteProvider(provider, path);

        Assert.Equal(
            new[] { path, SettingsStore.WriteLockPath(path) }.Order(StringComparer.Ordinal),
            Directory.GetFiles(temp.Root).Order(StringComparer.Ordinal)
        );
        AgentSettings settings = SettingsStore.Resolve(path, _ => null);
        CustomProvider stored = Assert.Single(settings.Providers);
        Assert.Equal(provider, stored);
    }
}
