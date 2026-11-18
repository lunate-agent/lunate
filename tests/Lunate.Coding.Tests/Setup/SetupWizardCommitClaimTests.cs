using System.Text.Json;
using System.Text.Json.Nodes;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The setup wizard's commit replaces settings.json, providers.json and auth.json, and it is a
/// read-modify-write like every store write: it seeds the staged copies from the targets and then
/// replaces them. Direct store writes hold a cross-process claim (spec: Cross-process settings
/// writes, Cross-process provider-store writes); so must this commit, or two processes that each
/// seed first lose one side's change — the lost update the direct-write tests cannot see.
/// <para>
/// The proof is a real second process running the production commit path while this one holds the
/// claim: the child must wait, write nothing, and then merge the change that landed while it
/// waited.
/// </para>
/// </summary>
public sealed class SetupWizardCommitClaimTests
{
    private const string SettingsPathVariable = "LUNATE_WIZARD_SETTINGS_PATH";
    private const string ProviderPathVariable = "LUNATE_WIZARD_PROVIDER_PATH";
    private const string AuthPathVariable = "LUNATE_WIZARD_AUTH_PATH";
    private const string ReadyPathVariable = "LUNATE_WIZARD_READY_PATH";
    private const string RefusalPathVariable = "LUNATE_WIZARD_REFUSAL_PATH";

    private const string SettingsChild = nameof(A_wizard_commit_waits_for_the_settings_claim);
    private const string ProviderChild = nameof(A_wizard_commit_waits_for_the_provider_claim);
    private const string NormalWriterChild = nameof(
        A_provider_entry_saved_by_a_normal_writer_during_the_walk_survives_the_commit
    );
    private const string UnclaimableChild = nameof(
        An_unclaimable_wizard_commit_refuses_without_writing
    );

    [Fact]
    public async Task A_wizard_commit_waits_for_the_settings_claim()
    {
        string? settingsPath = Environment.GetEnvironmentVariable(SettingsPathVariable);
        if (TestChildProcess.IsChild(SettingsChild))
        {
            File.WriteAllText(Environment.GetEnvironmentVariable(ReadyPathVariable)!, "ready");
            Commit(
                settingsPath!,
                Environment.GetEnvironmentVariable(ProviderPathVariable)!,
                Environment.GetEnvironmentVariable(AuthPathVariable)!
            );
            return;
        }

        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        string providerPath = temp.File("providers.json");
        string authPath = temp.File("auth.json");
        string ready = temp.File("wizard.ready");
        SettingsStore.WriteFavorites(["seed"], path);

        using FileLock held = FileLock.Acquire(
            Path.ChangeExtension(path, ".lock"),
            WaitBudgets.StoreClaimHold
        );
        Task<(int ExitCode, string Output)> child = Spawn(
            SettingsChild,
            path,
            providerPath,
            authPath,
            ready
        );

        await WaitForReadyAsync(ready);

        // The child has reached the commit. While this process holds the claim the commit must not
        // write anything — a last-writer-wins commit would seed and replace here.
        await Task.Delay(WaitBudgets.StoreClaimContention, TestContext.Current.CancellationToken);
        Assert.False(
            child.IsCompleted,
            "the wizard committed while another process held the settings claim"
        );
        Assert.DoesNotContain("chat-main", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.False(
            File.Exists(providerPath),
            "the wizard wrote the provider store while waiting"
        );
        Assert.False(File.Exists(authPath), "the wizard wrote the credential while waiting");

        // A change that lands while the child waits must survive the child's commit: the commit
        // reads the settings file after it takes the claim, not before it.
        JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["favorites"] = new JsonArray("parent");
        File.WriteAllText(
            path,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" })
        );

        held.Dispose();
        (int exitCode, string output) = await child;
        Assert.True(exitCode == 0, $"the child process failed:\n{output}");

        AgentSettings settings = SettingsStore.Resolve(path, _ => null);
        Assert.True(
            settings.Profiles.ContainsKey("chat-main"),
            "the wizard's profile did not land"
        );
        Assert.Equal(["parent"], settings.Favorites);
    }

    [Fact]
    public async Task A_wizard_commit_waits_for_the_provider_claim()
    {
        string? settingsPath = Environment.GetEnvironmentVariable(SettingsPathVariable);
        if (TestChildProcess.IsChild(ProviderChild))
        {
            File.WriteAllText(Environment.GetEnvironmentVariable(ReadyPathVariable)!, "ready");
            Commit(
                settingsPath!,
                Environment.GetEnvironmentVariable(ProviderPathVariable)!,
                Environment.GetEnvironmentVariable(AuthPathVariable)!
            );
            return;
        }

        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        string providerPath = temp.File("providers.json");
        string authPath = temp.File("auth.json");
        string ready = temp.File("wizard.ready");
        ProviderStore.Save(providerPath, [SeedProvider()]);

        using FileLock held = FileLock.Acquire(
            Path.ChangeExtension(providerPath, ".lock"),
            WaitBudgets.StoreClaimHold
        );
        Task<(int ExitCode, string Output)> child = Spawn(
            ProviderChild,
            path,
            providerPath,
            authPath,
            ready
        );

        await WaitForReadyAsync(ready);
        await Task.Delay(WaitBudgets.StoreClaimContention, TestContext.Current.CancellationToken);
        Assert.False(
            child.IsCompleted,
            "the wizard committed while another process held the provider-store claim"
        );
        Assert.False(File.Exists(path), "the wizard wrote the settings while waiting");

        // A provider entry another process saved while the child waits must survive the commit:
        // the commit merges into the store it reads under the claim, not into an earlier snapshot.
        File.WriteAllText(
            providerPath,
            """
            {
              "schemaVersion": 1,
              "providers": [
                {
                  "name": "parent",
                  "kind": "openai",
                  "baseUrl": "https://parent.example/v1",
                  "credentialReference": "auth:parent"
                },
                {
                  "name": "seed",
                  "kind": "openai",
                  "baseUrl": "https://seed.example/v1",
                  "credentialReference": "auth:seed"
                }
              ]
            }
            """
        );

        held.Dispose();
        (int exitCode, string output) = await child;
        Assert.True(exitCode == 0, $"the child process failed:\n{output}");

        AuthStore auth = AuthStore.Load(authPath);
        string[] names =
        [
            .. ProviderStore.Load(providerPath, auth, []).Entries.Select(entry => entry.Name),
        ];
        Assert.Contains("openai", names);
        Assert.Contains("parent", names);
        Assert.Contains("seed", names);
        Assert.True(
            SettingsStore.Resolve(path, _ => null).Profiles.ContainsKey("chat-main"),
            "the wizard's profile did not land"
        );
    }

    /// <summary>
    /// The other process uses the ordinary provider-store writer, not a raw claim: its entry is in
    /// the target before the commit reads it, so the commit must merge rather than replace it.
    /// </summary>
    [Fact]
    public async Task A_provider_entry_saved_by_a_normal_writer_during_the_walk_survives_the_commit()
    {
        if (TestChildProcess.IsChild(NormalWriterChild))
        {
            string childProviderPath = Required(ProviderPathVariable);
            string childAuthPath = Required(AuthPathVariable);
            ProviderStore store = ProviderStore.Load(
                childProviderPath,
                AuthStore.Load(childAuthPath),
                []
            );
            ProviderStore.Save(
                childProviderPath,
                [
                    .. store.Entries,
                    new ProviderEntry(
                        "concurrent",
                        "openai",
                        new Uri("https://concurrent.example/v1"),
                        "auth:concurrent"
                    ),
                ]
            );
            return;
        }

        using var temp = new TempDirectory();
        string settingsPath = temp.File("settings.json");
        string providerPath = temp.File("providers.json");
        string authPath = temp.File("auth.json");
        ProviderStore.Save(providerPath, [SeedProvider()]);

        (int exitCode, string output) = await Spawn(
            NormalWriterChild,
            settingsPath,
            providerPath,
            authPath,
            temp.File("unused.ready")
        );
        Assert.True(exitCode == 0, $"the child process failed:\n{output}");

        SetupWizardCommit.Commit(
            PresetWalk("walk-secret"),
            new SetupWizardCommit.CommitPaths(settingsPath, providerPath),
            () => AuthStore.Load(authPath)
        );

        AuthStore auth = AuthStore.Load(authPath);
        string[] names =
        [
            .. ProviderStore.Load(providerPath, auth, []).Entries.Select(entry => entry.Name),
        ];
        Assert.Contains("concurrent", names);
        Assert.Contains("openai", names);
        Assert.Contains("seed", names);
    }

    [Fact]
    public async Task An_unclaimable_wizard_commit_refuses_without_writing()
    {
        if (TestChildProcess.IsChild(UnclaimableChild))
        {
            string childRefusalPath = Required(RefusalPathVariable);
            try
            {
                Commit(
                    Required(SettingsPathVariable),
                    Required(ProviderPathVariable),
                    Required(AuthPathVariable),
                    WaitBudgets.StoreClaimRefusal
                );
                File.WriteAllText(childRefusalPath, "the commit did not refuse");
            }
            catch (InvalidOperationException childRefusal)
            {
                File.WriteAllText(childRefusalPath, childRefusal.Message);
            }

            return;
        }

        using var temp = new TempDirectory();
        string settingsPath = temp.File("settings.json");
        string providerPath = temp.File("providers.json");
        string authPath = temp.File("auth.json");
        string refusalPath = temp.File("refusal");
        File.WriteAllText(settingsPath, """{"schemaVersion": 2, "favorites": ["child"]}""" + "\n");
        ProviderStore.Save(providerPath, [SeedProvider()]);
        byte[] settingsBefore = File.ReadAllBytes(settingsPath);
        byte[] providersBefore = File.ReadAllBytes(providerPath);

        using FileLock held = FileLock.Acquire(
            Path.ChangeExtension(settingsPath, ".lock"),
            WaitBudgets.StoreClaimHold
        );
        (int exitCode, string output) = await TestChildProcess.RunAsync(
            typeof(SetupWizardCommitClaimTests),
            UnclaimableChild,
            new Dictionary<string, string?>
            {
                [SettingsPathVariable] = settingsPath,
                [ProviderPathVariable] = providerPath,
                [AuthPathVariable] = authPath,
                [RefusalPathVariable] = refusalPath,
            }
        );
        Assert.True(exitCode == 0, $"the child process failed:\n{output}");

        string refusal = File.ReadAllText(refusalPath);
        Assert.Contains(settingsPath, refusal, StringComparison.Ordinal);
        Assert.Contains("settings.lock", refusal, StringComparison.Ordinal);
        Assert.Contains("nothing was changed", refusal, StringComparison.Ordinal);
        Assert.Equal(settingsBefore, File.ReadAllBytes(settingsPath));
        Assert.Equal(providersBefore, File.ReadAllBytes(providerPath));
        Assert.False(File.Exists(authPath), "the commit wrote the credential");
        Assert.Empty(Directory.GetDirectories(temp.Root, ".setup-*"));
    }

    /// <summary>
    /// The commit merges the provider entry into the store as the commit finds it: the staged copy
    /// is seeded under the claim, so an entry saved while the walk was being answered survives.
    /// </summary>
    [Fact]
    public void The_wizard_commit_merges_a_provider_entry_saved_during_the_walk()
    {
        using var temp = new TempDirectory();
        string settingsPath = temp.File("settings.json");
        string providerPath = temp.File("providers.json");
        string authPath = temp.File("auth.json");

        // The walk is answered first; another process saves its entry before the commit runs.
        SetupWizardModel model = PresetWalk("walk-secret");
        ProviderStore.Save(providerPath, [SeedProvider()]);

        SetupWizardCommit.Commit(
            model,
            new SetupWizardCommit.CommitPaths(settingsPath, providerPath),
            () => AuthStore.Load(authPath)
        );

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(providerPath));
        string[] names =
        [
            .. document
                .RootElement.GetProperty("providers")
                .EnumerateArray()
                .Select(provider => provider.GetProperty("name").GetString()!),
        ];
        Assert.Contains("seed", names);
        Assert.Contains("openai", names);
    }

    private static void Commit(
        string settingsPath,
        string providerPath,
        string authPath,
        TimeSpan? claimTimeout = null
    ) =>
        SetupWizardCommit.Commit(
            PresetWalk("child-secret"),
            new SetupWizardCommit.CommitPaths(settingsPath, providerPath),
            () => AuthStore.Load(authPath),
            claimTimeout
        );

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

    private static ProviderEntry SeedProvider() =>
        new("seed", "openai", new Uri("https://seed.example/v1"), "auth:seed");

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"the child needs {name}");

    private static Task<(int ExitCode, string Output)> Spawn(
        string child,
        string settingsPath,
        string providerPath,
        string authPath,
        string ready
    ) =>
        TestChildProcess.RunAsync(
            typeof(SetupWizardCommitClaimTests),
            child,
            new Dictionary<string, string?>
            {
                [SettingsPathVariable] = settingsPath,
                [ProviderPathVariable] = providerPath,
                [AuthPathVariable] = authPath,
                [ReadyPathVariable] = ready,
            }
        );

    private static async Task WaitForReadyAsync(string path)
    {
        long deadline = Environment.TickCount64 + (long)WaitBudgets.SpawnWindow.TotalMilliseconds;
        while (!File.Exists(path))
        {
            if (Environment.TickCount64 >= deadline)
            {
                throw new TimeoutException(
                    $"the child never reached the commit: '{path}' did not appear within "
                        + $"{WaitBudgets.SpawnWindow.TotalSeconds:0} s."
                );
            }

            await Task.Delay(WaitBudgets.ChildMarkerPoll, TestContext.Current.CancellationToken);
        }
    }
}
