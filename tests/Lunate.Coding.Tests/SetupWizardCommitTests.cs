using System.Text;
using System.Text.Json;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The wizard's commit: one pass through the store writers (spec: One settings writer), a
/// skip-everything confirm that writes nothing (Nothing written before the summary confirms), the
/// writer pinning (a second writer fails the test), and the custom-entry parity with
/// <c>auth login --custom</c> (task 3.8).
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardCommitTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    /// <summary>The entries auth.json already names, so loading it migrates nothing into the store.</summary>
    private static readonly ProviderEntry[] SeedEntries =
    [
        new("anthropic", "anthropic", new Uri("https://api.anthropic.com/v1"), "auth:anthropic"),
        new("openai", "openai", new Uri("https://api.openai.com/v1"), "auth:openai"),
    ];

    public void Dispose() => _temp.Dispose();

    private string SettingsPath => _temp.File("settings.json");
    private string ProviderStorePath => AuthStore.ProviderStoreFilePath(_temp.File("auth.json"));

    [PosixFact]
    public void Staged_preset_walk_writes_credential_entry_profile_and_usage()
    {
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "openai",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("openai", Key: "typed-secret"));
        model.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        model.RecordChatProfile(
            new WizardProfileChoice("chat-main", "gpt-5", ReasoningLevel: null)
        );

        Commit(model);

        string authJson = File.ReadAllText(_temp.File("auth.json"));
        Assert.Contains("typed-secret", authJson, StringComparison.Ordinal);
        string providersJson = File.ReadAllText(ProviderStorePath);
        Assert.DoesNotContain("typed-secret", providersJson, StringComparison.Ordinal);
        string settingsJson = File.ReadAllText(SettingsPath);
        Assert.DoesNotContain("typed-secret", settingsJson, StringComparison.Ordinal);

        using JsonDocument document = JsonDocument.Parse(providersJson);
        JsonElement entry = document.RootElement.GetProperty("providers")[0];
        Assert.Equal("openai", entry.GetProperty("name").GetString());
        Assert.Equal("auth:openai", entry.GetProperty("credentialReference").GetString());

        using JsonDocument settings = JsonDocument.Parse(settingsJson);
        JsonElement profile = settings.RootElement.GetProperty("profiles").GetProperty("chat-main");
        Assert.Equal("openai", profile.GetProperty("provider").GetString());
        Assert.Equal("gpt-5", profile.GetProperty("model").GetString());
        Assert.Equal(
            "chat-main",
            settings.RootElement.GetProperty("usages").GetProperty("chat").GetString()
        );
    }

    [PosixFact]
    public void An_environment_reference_stores_no_key()
    {
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "anthropic",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "anthropic-messages",
                Placement: CredentialPlacement.XApiKey,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("env:ANTHROPIC_API_KEY"));

        SetupWizardCommit.Commit(model, Paths(), LoadAuth);

        Assert.True(!File.Exists(_temp.File("auth.json")));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ProviderStorePath));
        Assert.Equal(
            "env:ANTHROPIC_API_KEY",
            document
                .RootElement.GetProperty("providers")[0]
                .GetProperty("credentialReference")
                .GetString()
        );
    }

    [PosixFact]
    public void Skip_everything_confirm_is_a_no_op()
    {
        var model = new SetupWizardModel();
        foreach (SetupStep step in SetupWizardModel.Order)
        {
            model.Enter(step);
            model.Skip(step);
        }

        string settingsBefore = Seed(SettingsPath, """{"schemaVersion": 2, "favorites": []}""");
        SetupWizardCommit.Commit(model, Paths(), LoadAuth);
        string settingsAfter = Seed(SettingsPath, """{"schemaVersion": 2, "favorites": []}""");
        Assert.Equal(settingsBefore, settingsAfter);
    }

    [PosixFact]
    public void The_wizard_writes_through_the_store_only()
    {
        // The writer pinning test (3.7): the wizard's written file must be byte-equal to the
        // output of the store's own writer calls for the same edits — the wizard formats nothing.
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "openai",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("env:OPENAI_API_KEY"));
        model.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        model.RecordChatProfile(
            new WizardProfileChoice("chat-main", "gpt-5", ReasoningLevel: "high")
        );
        model.RecordFastProfile(true);

        Commit(model);

        string wizardPath = _temp.File("settings-writer-pin.json");
        SettingsStore.WriteProfile(
            new ModelProfile("chat-main", "openai", "gpt-5", ReasoningLevel: "high"),
            wizardPath
        );
        SettingsStore.WriteUsage("chat", "chat-main", wizardPath);
        SettingsStore.WriteUsage("fast", "chat-main", wizardPath);

        Assert.Equal(File.ReadAllText(wizardPath), File.ReadAllText(SettingsPath));
    }

    [PosixFact]
    public void The_custom_entry_and_auth_login_custom_produce_the_same_entry()
    {
        // Task 3.8: same answers, same providers.json entry — auth login --custom writes its
        // settings section through SettingsStore.WriteProvider; the wizard stages the same entry
        // with a typed key and the same custom fields, and both load to one entry shape.
        CustomProvider? expected;
        {
            string customPath = _temp.File("auth-custom.json");
            Assert.True(
                CustomProvider.TryCreate(
                    "homelab",
                    "http://localhost:8080/v1",
                    "local-model",
                    catalogName: null,
                    out expected,
                    out string? error,
                    "openai-chat",
                    CredentialPlacement.Bearer,
                    null,
                    null
                ) && expected is not null,
                error
            );

            // credential login's own order: the key first, then the settings entry it pairs with.
            AuthStore login = AuthStore.Load(customPath);
            login.Set("homelab", "typed-secret");
            login.Save();
            SettingsStore.WriteProvider(expected, AuthStore.SettingsFilePath(customPath));
        }

        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "homelab",
                Custom: true,
                Region: null,
                Endpoint: new Uri("http://localhost:8080/v1"),
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("homelab", Key: "typed-secret"));
        model.RecordModel(new WizardModelChoice("local-model", "openai-chat"));

        Commit(model);

        ProviderEntry wizardEntry = ProviderStore
            .Load(ProviderStorePath, LoadAuth(), [])
            .Entries.Single(entry => entry.Name == "homelab");
        AuthStore loginAuth = AuthStore.Load(_temp.File("auth-custom.json"));
        ProviderEntry loginEntry = ProviderStore
            .Load(AuthStore.ProviderStoreFilePath(_temp.File("auth-custom.json")), loginAuth, [])
            .Entries.Single(entry => entry.Name == "homelab");

        Assert.Equal(expected!.BaseUrl, wizardEntry.BaseUrl);
        Assert.Equal(expected!.BaseUrl, loginEntry.BaseUrl);
        Assert.Equal(wizardEntry.Kind, loginEntry.Kind);
        Assert.Equal(wizardEntry.CredentialReference, loginEntry.CredentialReference);
        Assert.Equal(wizardEntry.BaseUrls?.Count ?? 0, loginEntry.BaseUrls?.Count ?? 0);
    }

    [PosixFact]
    public void Routing_switch_and_descriptions_are_written_through_the_writers()
    {
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "openai",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("env:OPENAI_API_KEY"));
        model.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        model.RecordChatProfile(
            new WizardProfileChoice("chat-main", "gpt-5", ReasoningLevel: null)
        );
        model.RecordDecisionProfile(
            new WizardDecisionChoice("decider", "jev-1.14", Native: false, PinnedVersion: null)
        );
        model.RecordDecisions(
            new WizardDecisionsChoice(
                Approvals: "shadow",
                Routing: null,
                Tool: "on",
                RoutingProfiles: [],
                RoutingDescriptions: new Dictionary<string, string>(StringComparer.Ordinal)
            )
        );

        Commit(model);

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(
            "decider",
            settings.RootElement.GetProperty("decisions").GetProperty("profile").GetString()
        );
        Assert.Equal(
            "shadow",
            settings.RootElement.GetProperty("decisions").GetProperty("approvals").GetString()
        );
        Assert.Equal(
            "on",
            settings.RootElement.GetProperty("decisions").GetProperty("tool").GetString()
        );
    }

    [PosixFact]
    public void Accepting_the_decisions_defaults_writes_nothing_for_the_layer()
    {
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "openai",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("env:OPENAI_API_KEY"));
        model.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        model.RecordDecisions(null);

        Commit(model);

        // No staged profile or decisions answer: the settings file is not written at all.
        Assert.True(!File.Exists(SettingsPath), "the defaults write nothing for the layer");
    }

    [PosixFact]
    public void A_failure_after_the_auth_stage_leaves_every_file_byte_identical()
    {
        // B-5: the credential used to be saved before the entry and "restored" in memory only, so
        // a later failure left the new key on disk and the previous key gone. The commit now
        // stages the key and touches no target until every body is ready.
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "openai",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "openai-realtime",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("openai", Key: "new-secret"));
        model.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        model.RecordChatProfile(
            new WizardProfileChoice("chat-main", "gpt-5", ReasoningLevel: null)
        );
        string authPath = _temp.File("auth.json");
        Seed(
            authPath,
            """{"schemaVersion": 1, "keys": {"anthropic": "other-secret", "openai": "previous-secret"}}"""
        );
        ProviderStore.Save(ProviderStorePath, SeedEntries);
        Seed(SettingsPath, """{"schemaVersion": 2, "model": "gpt-4o"}""");
        byte[] authBefore = File.ReadAllBytes(authPath);
        byte[] providersBefore = File.ReadAllBytes(ProviderStorePath);
        byte[] settingsBefore = File.ReadAllBytes(SettingsPath);

        // The unsupported family is refused when the provider entry is built: after the credential
        // has been written to the stage, and before any target file is replaced.
        InvalidDataException failure = Assert.Throws<InvalidDataException>(() =>
            SetupWizardCommit.Commit(model, Paths(), LoadAuth)
        );

        Assert.Contains("openai-realtime", failure.Message, StringComparison.Ordinal);
        Assert.Equal(authBefore, File.ReadAllBytes(authPath));
        Assert.Equal(providersBefore, File.ReadAllBytes(ProviderStorePath));
        Assert.Equal(settingsBefore, File.ReadAllBytes(SettingsPath));
        Assert.Contains("previous-secret", File.ReadAllText(authPath), StringComparison.Ordinal);
        Assert.DoesNotContain("new-secret", File.ReadAllText(authPath), StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(_temp.Root, ".setup-*"));
    }

    [PosixFact]
    public void A_failed_replace_restores_every_file_already_written()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        // The other half of the transaction: when a replace itself fails, the files replaced
        // before it go back to their exact previous bytes. The provider store lives in a read-only
        // directory, so it is the second replace — after auth.json — and the commit must undo it.
        string locked = Path.Combine(_temp.Root, "locked");
        Directory.CreateDirectory(locked);
        string providersPath = Path.Combine(locked, "providers.json");

        // The seeded openai entry differs from the one the walk writes (it carries a region), so
        // the provider store is a real pending replace and not an unchanged no-op.
        ProviderStore.Save(providersPath, [SeedEntries[0], SeedEntries[1] with { Region = "eu" }]);
        var model = PresetWalk("new-secret");
        string authPath = _temp.File("auth.json");
        Seed(
            authPath,
            """{"schemaVersion": 1, "keys": {"anthropic": "other-secret", "openai": "previous-secret"}}"""
        );
        Seed(SettingsPath, """{"schemaVersion": 2, "favorites": ["gpt-5"]}""");
        byte[] authBefore = File.ReadAllBytes(authPath);
        byte[] providersBefore = File.ReadAllBytes(providersPath);
        byte[] settingsBefore = File.ReadAllBytes(SettingsPath);

        UnixFileMode mode = File.GetUnixFileMode(locked);
        File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            Exception? failure = Record.Exception(() =>
                SetupWizardCommit.Commit(
                    model,
                    new SetupWizardCommit.CommitPaths(SettingsPath, providersPath),
                    LoadAuth
                )
            );
            Assert.True(
                failure is IOException or UnauthorizedAccessException,
                $"expected a failed replace, got {failure?.GetType().Name ?? "none"}"
            );
        }
        finally
        {
            File.SetUnixFileMode(locked, mode);
        }

        Assert.Equal(authBefore, File.ReadAllBytes(authPath));
        Assert.Equal(providersBefore, File.ReadAllBytes(providersPath));
        Assert.Equal(settingsBefore, File.ReadAllBytes(SettingsPath));
        Assert.Contains("previous-secret", File.ReadAllText(authPath), StringComparison.Ordinal);
        Assert.DoesNotContain("new-secret", File.ReadAllText(authPath), StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(_temp.Root, ".setup-*"));
    }

    [PosixFact]
    public void A_commit_replaces_the_three_files_and_keeps_the_sections_it_did_not_touch()
    {
        var model = PresetWalk("new-secret");
        string authPath = _temp.File("auth.json");
        Seed(
            authPath,
            """{"schemaVersion": 1, "keys": {"anthropic": "other-secret", "openai": "previous-secret"}}"""
        );
        Seed(SettingsPath, """{"schemaVersion": 2, "favorites": ["gpt-5"]}""");
        ProviderStore.Save(ProviderStorePath, SeedEntries);

        Commit(model);

        using JsonDocument auth = JsonDocument.Parse(File.ReadAllText(authPath));
        Assert.Equal(
            "new-secret",
            auth.RootElement.GetProperty("keys").GetProperty("openai").GetString()
        );
        Assert.Equal(
            "other-secret",
            auth.RootElement.GetProperty("keys").GetProperty("anthropic").GetString()
        );
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal("gpt-5", settings.RootElement.GetProperty("favorites")[0].GetString());
        Assert.Equal(
            "chat-main",
            settings.RootElement.GetProperty("usages").GetProperty("chat").GetString()
        );
        using JsonDocument providers = JsonDocument.Parse(File.ReadAllText(ProviderStorePath));
        Assert.Equal(
            ["anthropic", "openai"],
            providers
                .RootElement.GetProperty("providers")
                .EnumerateArray()
                .Select(entry => entry.GetProperty("name").GetString()!)
        );
        Assert.Empty(Directory.GetDirectories(_temp.Root, ".setup-*"));
        Assert.Empty(Directory.GetFiles(_temp.Root, "*.tmp"));
    }

    [PosixFact]
    public void The_walked_region_reaches_the_provider_entry_and_the_summary()
    {
        // S-11: the answer the summary prints is the metadata providers.json keeps.
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "openrouter",
                Custom: false,
                Region: "eu",
                Endpoint: null,
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("env:OPENROUTER_API_KEY"));

        Commit(model);

        using JsonDocument providers = JsonDocument.Parse(File.ReadAllText(ProviderStorePath));
        Assert.Equal(
            "eu",
            providers.RootElement.GetProperty("providers")[0].GetProperty("region").GetString()
        );
        Assert.Contains(
            "provider: openrouter (region eu)",
            string.Join("\n", SetupWizardSummary.Render(model)),
            StringComparison.Ordinal
        );
    }

    [PosixFact]
    public void A_custom_providers_region_is_written_to_the_provider_store_entry()
    {
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "homelab",
                Custom: true,
                Region: "local",
                Endpoint: new Uri("http://localhost:8080/v1"),
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("env:HOMELAB_API_KEY"));

        Commit(model);

        using JsonDocument providers = JsonDocument.Parse(File.ReadAllText(ProviderStorePath));
        Assert.Equal(
            "local",
            providers.RootElement.GetProperty("providers")[0].GetProperty("region").GetString()
        );
    }

    [PosixFact]
    public void A_typed_custom_providers_region_is_written_to_the_settings_entry()
    {
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "homelab",
                Custom: true,
                Region: "eu",
                Endpoint: new Uri("http://localhost:8080/v1"),
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("homelab", Key: "typed-secret"));
        model.RecordModel(new WizardModelChoice("local-model", "openai-chat"));

        Commit(model);

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(
            "eu",
            settings
                .RootElement.GetProperty("providers")
                .GetProperty("homelab")
                .GetProperty("region")
                .GetString()
        );
    }

    [PosixFact]
    public void A_family_the_walk_resolved_is_written_to_the_entry_and_the_profile()
    {
        // S-12: the family the check used is the family the entry and the profile record.
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "opencode-go",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "anthropic-messages",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("env:OPENCODE_API_KEY"));
        model.RecordModel(
            new WizardModelChoice("anthropic/claude-sonnet-5-5", "anthropic-messages")
        );
        model.RecordChatProfile(
            new WizardProfileChoice("main", "anthropic/claude-sonnet-5-5", ReasoningLevel: null)
        );

        Commit(model);

        using JsonDocument providers = JsonDocument.Parse(File.ReadAllText(ProviderStorePath));
        JsonElement entry = providers.RootElement.GetProperty("providers")[0];
        Assert.Equal(
            "https://opencode.ai/inference/go/anthropic/v1",
            entry.GetProperty("baseUrls").GetProperty("anthropic-messages").GetString()
        );
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(
            "anthropic-messages",
            settings
                .RootElement.GetProperty("profiles")
                .GetProperty("main")
                .GetProperty("apiFamily")
                .GetString()
        );
        ProviderEntry written = ProviderStore
            .Load(ProviderStorePath, LoadAuth(), [])
            .Entries.Single(entry => entry.Name == "opencode-go");
        Assert.Contains(ProviderApiFamily.AnthropicMessages, written.ChatFamilies);
    }

    [PosixFact]
    public void A_rerun_keeps_the_profile_keys_the_wizard_does_not_own()
    {
        // S-29: a re-run that changes the model leaves the hand-set profile values alone.
        Seed(
            SettingsPath,
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "chat-main": {
                  "provider": "openai",
                  "model": "gpt-4o",
                  "maxOutput": 4096,
                  "temperature": 0.4,
                  "contextWindow": 64000,
                  "capabilities": { "inputModalities": ["text", "image"] }
                }
              },
              "usages": { "chat": "chat-main" }
            }
            """
        );
        var model = PresetWalk("new-secret");

        Commit(model);

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        JsonElement profile = settings.RootElement.GetProperty("profiles").GetProperty("chat-main");
        Assert.Equal("gpt-5", profile.GetProperty("model").GetString());
        Assert.Equal(4096, profile.GetProperty("maxOutput").GetInt32());
        Assert.Equal(0.4m, profile.GetProperty("temperature").GetDecimal());
        Assert.Equal(64000, profile.GetProperty("contextWindow").GetInt32());
        Assert.Equal(
            "image",
            profile.GetProperty("capabilities").GetProperty("inputModalities")[1].GetString()
        );
    }

    private static SetupWizardModel PresetWalk(string secret)
    {
        var model = new SetupWizardModel();
        model.RecordProvider(
            new WizardProviderChoice(
                "openai",
                Custom: false,
                Region: null,
                Endpoint: null,
                ApiFamily: "openai-chat",
                Placement: CredentialPlacement.Bearer,
                ModelsUrl: null
            )
        );
        model.RecordCredential(new WizardCredentialChoice("openai", Key: secret));
        model.RecordModel(new WizardModelChoice("gpt-5", "openai-chat"));
        model.RecordChatProfile(
            new WizardProfileChoice("chat-main", "gpt-5", ReasoningLevel: null)
        );
        return model;
    }

    private void Commit(SetupWizardModel model) =>
        SetupWizardCommit.Commit(model, Paths(), LoadAuth);

    private SetupWizardCommit.CommitPaths Paths() => new(SettingsPath, ProviderStorePath);

    private AuthStore LoadAuth() => AuthStore.Load(_temp.File("auth.json"));

    private static string Seed(string path, string content)
    {
        File.WriteAllText(path, content + "\n");
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
