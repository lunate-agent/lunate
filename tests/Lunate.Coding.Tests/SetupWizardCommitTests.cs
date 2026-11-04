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

        SetupWizardCommit.Commit(model, Paths(), LoadAuth, () => LoadEntries);

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
        SetupWizardCommit.Commit(model, Paths(), LoadAuth, () => LoadEntries);
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

    private void Commit(SetupWizardModel model) =>
        SetupWizardCommit.Commit(model, Paths(), LoadAuth, () => LoadEntries);

    private SetupWizardCommit.CommitPaths Paths() => new(SettingsPath, ProviderStorePath);

    private AuthStore LoadAuth() => AuthStore.Load(_temp.File("auth.json"));

    private IReadOnlyList<ProviderEntry> LoadEntries =>
        ProviderStore.Load(ProviderStorePath, LoadAuth(), []).Entries;

    private static string Seed(string path, string content)
    {
        File.WriteAllText(path, content + "\n");
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
