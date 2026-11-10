using System.Text.Json;
using System.Text.Json.Nodes;
using Lunate.Ai;

namespace Lunate.Coding.Tests;

/// <summary>
/// The settings and provider stores serialize their read-modify-write across processes, not just
/// threads: the in-process gate cannot stop two Lunate processes from each reading the file and
/// each replacing it with a document that lacks the other's change (review finding N-4). The proof
/// is a real second process contending on the same lock file; an in-process second handle would not
/// show that the claim is the file, and asserting a share-mode constant is not evidence.
/// </summary>
public sealed class StoreCrossProcessLockTests
{
    private const string SettingsPathVariable = "LUNATE_TEST_SETTINGS_PATH";
    private const string ProviderPathVariable = "LUNATE_TEST_PROVIDER_PATH";
    private const string AuthPathVariable = "LUNATE_TEST_AUTH_PATH";
    private const string ReadyPathVariable = "LUNATE_TEST_READY_PATH";

    private const string SettingsChild = nameof(
        A_settings_write_waits_for_a_claim_another_process_holds
    );
    private const string ProviderChild = nameof(
        A_provider_save_waits_for_a_claim_another_process_holds
    );
    private const string MigrationChild = nameof(A_migration_reads_again_under_the_claim);

    [Fact]
    public async Task A_settings_write_waits_for_a_claim_another_process_holds()
    {
        string? settingsPath = Environment.GetEnvironmentVariable(SettingsPathVariable);
        if (TestChildProcess.IsChild(SettingsChild))
        {
            File.WriteAllText(Environment.GetEnvironmentVariable(ReadyPathVariable)!, "ready");
            SettingsStore.WriteProfile(new ModelProfile("child", "openai", "gpt-5"), settingsPath!);
            return;
        }

        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        string ready = temp.File("settings.ready");
        SettingsStore.WriteFavorites(["seed"], path);

        using FileLock held = FileLock.Acquire(
            Path.ChangeExtension(path, ".lock"),
            WaitBudgets.StoreClaimHold
        );
        Task<(int ExitCode, string Output)> child = TestChildProcess.RunAsync(
            typeof(StoreCrossProcessLockTests),
            SettingsChild,
            new Dictionary<string, string?>
            {
                [SettingsPathVariable] = path,
                [ReadyPathVariable] = ready,
            }
        );

        await WaitForReadyAsync(ready);

        // The child has reached the writer. While this process holds the claim, the write must not
        // land: a last-writer-wins store would let it through here.
        await Task.Delay(WaitBudgets.StoreClaimContention, TestContext.Current.CancellationToken);
        Assert.False(child.IsCompleted, "the child wrote while another process held the claim");
        Assert.DoesNotContain("child", File.ReadAllText(path), StringComparison.Ordinal);

        // A change that lands while the child waits must survive the child's read-modify-write, not
        // be replaced by the document the child read before it.
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
        Assert.True(settings.Profiles.ContainsKey("child"), "the child's profile did not land");
        Assert.Equal(["parent"], settings.Favorites);
    }

    [Fact]
    public async Task A_provider_save_waits_for_a_claim_another_process_holds()
    {
        string? providersPath = Environment.GetEnvironmentVariable(ProviderPathVariable);
        if (TestChildProcess.IsChild(ProviderChild))
        {
            File.WriteAllText(Environment.GetEnvironmentVariable(ReadyPathVariable)!, "ready");
            ProviderStore.Save(providersPath!, [ChildProvider()]);
            return;
        }

        using var temp = new TempDirectory();
        string path = temp.File("providers.json");
        string ready = temp.File("providers.ready");
        ProviderStore.Save(path, [SeedProvider()]);

        using FileLock held = FileLock.Acquire(
            Path.ChangeExtension(path, ".lock"),
            WaitBudgets.StoreClaimHold
        );
        Task<(int ExitCode, string Output)> child = TestChildProcess.RunAsync(
            typeof(StoreCrossProcessLockTests),
            ProviderChild,
            new Dictionary<string, string?>
            {
                [ProviderPathVariable] = path,
                [ReadyPathVariable] = ready,
            }
        );

        await WaitForReadyAsync(ready);
        await Task.Delay(WaitBudgets.StoreClaimContention, TestContext.Current.CancellationToken);
        Assert.False(child.IsCompleted, "the child saved while another process held the claim");
        Assert.DoesNotContain("child", File.ReadAllText(path), StringComparison.Ordinal);

        held.Dispose();
        (int exitCode, string output) = await child;
        Assert.True(exitCode == 0, $"the child process failed:\n{output}");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        string[] names =
        [
            .. document
                .RootElement.GetProperty("providers")
                .EnumerateArray()
                .Select(provider => provider.GetProperty("name").GetString()!),
        ];
        Assert.Contains("child", names);
    }

    [Fact]
    public async Task A_migration_reads_again_under_the_claim()
    {
        string? authPath = Environment.GetEnvironmentVariable(AuthPathVariable);
        if (TestChildProcess.IsChild(MigrationChild))
        {
            File.WriteAllText(Environment.GetEnvironmentVariable(ReadyPathVariable)!, "ready");

            // AuthStore.Load migrates a missing provider entry through ProviderStore.Load, which is
            // the read-modify-write this test contends on.
            _ = AuthStore.Load(authPath!);
            return;
        }

        using var temp = new TempDirectory();
        string childAuthPath = temp.File("auth.json");
        string providersPath = temp.File("providers.json");
        string ready = temp.File("migration.ready");

        // The auth key is the "openai" preset, so the child's load has one entry to migrate.
        File.WriteAllText(
            childAuthPath,
            """{"schemaVersion": 1, "keys": { "openai": "secret" }}"""
        );
        File.WriteAllText(providersPath, """{"schemaVersion": 1, "providers": []}""");

        using FileLock held = FileLock.Acquire(
            Path.ChangeExtension(providersPath, ".lock"),
            WaitBudgets.StoreClaimHold
        );
        Task<(int ExitCode, string Output)> child = TestChildProcess.RunAsync(
            typeof(StoreCrossProcessLockTests),
            MigrationChild,
            new Dictionary<string, string?>
            {
                [AuthPathVariable] = childAuthPath,
                [ReadyPathVariable] = ready,
            }
        );

        await WaitForReadyAsync(ready);
        await Task.Delay(WaitBudgets.StoreClaimContention, TestContext.Current.CancellationToken);
        Assert.False(child.IsCompleted, "the migration wrote while another process held the claim");

        // A provider entry lands after the child's first read and before its claim is granted; a
        // migration that did not re-read under the claim would replace it with its stale document.
        File.WriteAllText(
            providersPath,
            """
            {
              "schemaVersion": 1,
              "providers": [
                {
                  "name": "parent",
                  "kind": "openai",
                  "baseUrl": "https://parent.example/v1",
                  "credentialReference": "auth:parent"
                }
              ]
            }
            """
        );

        held.Dispose();
        (int exitCode, string output) = await child;
        Assert.True(exitCode == 0, $"the child process failed:\n{output}");

        AuthStore auth = AuthStore.Load(childAuthPath);
        string[] names =
        [
            .. ProviderStore.Load(providersPath, auth, []).Entries.Select(e => e.Name),
        ];
        Assert.Contains("openai", names);
        Assert.Contains("parent", names);
    }

    [Fact]
    public void A_settings_claim_refusal_names_the_file_and_the_lock()
    {
        using var temp = new TempDirectory();
        string path = temp.File("settings.json");
        SettingsStore.WriteFavorites(["seed"], path);

        using FileLock held = FileLock.Acquire(
            Path.ChangeExtension(path, ".lock"),
            WaitBudgets.StoreClaimHold
        );
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            SettingsStore.AcquireWriteLock(path, WaitBudgets.StoreClaimRefusal)
        );

        Assert.Contains(path, error.Message, StringComparison.Ordinal);
        Assert.Contains(
            Path.ChangeExtension(path, ".lock"),
            error.Message,
            StringComparison.Ordinal
        );
        Assert.Equal(["seed"], SettingsStore.Resolve(path, _ => null).Favorites);
    }

    [Fact]
    public void A_provider_claim_refusal_names_the_file_and_the_lock()
    {
        using var temp = new TempDirectory();
        string path = temp.File("providers.json");
        ProviderStore.Save(path, [SeedProvider()]);

        using FileLock held = FileLock.Acquire(
            Path.ChangeExtension(path, ".lock"),
            WaitBudgets.StoreClaimHold
        );
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ProviderStore.AcquireLock(path, WaitBudgets.StoreClaimRefusal)
        );

        Assert.Contains(path, error.Message, StringComparison.Ordinal);
        Assert.Contains(
            Path.ChangeExtension(path, ".lock"),
            error.Message,
            StringComparison.Ordinal
        );
        AuthStore auth = AuthStore.Load(temp.File("auth.json"));
        Assert.Equal("seed", Assert.Single(ProviderStore.Load(path, auth, []).Entries).Name);
    }

    [Fact]
    public void Reading_settings_and_providers_creates_no_lock_file()
    {
        using var temp = new TempDirectory();
        string settingsPath = temp.File("settings.json");
        string providersPath = temp.File("providers.json");
        SettingsStore.WriteFavorites(["seed"], settingsPath);
        ProviderStore.Save(providersPath, [SeedProvider()]);
        File.Delete(Path.ChangeExtension(settingsPath, ".lock"));
        File.Delete(Path.ChangeExtension(providersPath, ".lock"));

        _ = SettingsStore.Resolve(settingsPath, _ => null);
        AuthStore auth = AuthStore.Load(temp.File("auth.json"));
        _ = ProviderStore.Load(providersPath, auth, []);

        Assert.False(File.Exists(Path.ChangeExtension(settingsPath, ".lock")));
        Assert.False(File.Exists(Path.ChangeExtension(providersPath, ".lock")));
    }

    private static ProviderEntry SeedProvider() =>
        new("seed", "openai", new Uri("https://seed.example/v1"), "auth:seed");

    private static ProviderEntry ChildProvider() =>
        new("child", "openai", new Uri("https://child.example/v1"), "auth:child");

    private static async Task WaitForReadyAsync(string path)
    {
        long deadline = Environment.TickCount64 + (long)WaitBudgets.SpawnWindow.TotalMilliseconds;
        while (!File.Exists(path))
        {
            if (Environment.TickCount64 >= deadline)
            {
                throw new TimeoutException(
                    $"the child never reached the write: '{path}' did not appear within "
                        + $"{WaitBudgets.SpawnWindow.TotalSeconds:0} s."
                );
            }

            await Task.Delay(WaitBudgets.ChildMarkerPoll, TestContext.Current.CancellationToken);
        }
    }
}
