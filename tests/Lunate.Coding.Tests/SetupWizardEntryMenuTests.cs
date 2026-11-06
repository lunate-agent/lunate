using System.Text.Json;
using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// <c>/setup</c> on a configured install opens the entry menu and its scoped entry walks (spec:
/// Re-runs open an entry menu; /setup opens the wizard — the entry menu on a configured install,
/// the full walk on a first run): add a provider, add a profile, change the chat profile, edit the
/// decisions layer, or walk everything, prefilled with the current values; each entry writes only
/// what changed through the same store writers the full walk uses.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardEntryMenuTests
{
    private const string Credential = "entry-menu-test-key-please-never-echo";

    private const string ConfiguredSettings = """
        {
          "schemaVersion": 2,
          "model": "gpt-4.1",
          "profiles": {
            "chat-main": { "provider": "openai", "model": "gpt-4.1" },
            "chat-alt": { "provider": "openai", "model": "gpt-4.1" }
          },
          "usages": { "chat": "chat-main" }
        }
        """;

    private const string SoleSettings = """
        {
          "schemaVersion": 2,
          "model": "gpt-4.1",
          "profiles": { "chat-main": { "provider": "openai", "model": "gpt-4.1" } },
          "usages": { "chat": "chat-main" }
        }
        """;

    [PosixFact]
    public async Task Setup_on_a_configured_install_opens_the_entry_menu()
    {
        using var host = ConfiguredHost();
        byte[] settings = File.ReadAllBytes(SettingsPath(host));
        byte[] providers = File.ReadAllBytes(ProviderPath(host));
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        await host.WaitForFrameAsync("Add a provider");
        await host.WaitForFrameAsync("Add a profile");
        await host.WaitForFrameAsync("Change the chat profile");
        await host.WaitForFrameAsync("Edit the decisions layer");
        await host.WaitForFrameAsync("Walk everything");

        host.Console.SendEscape();
        await host.WaitForFrameAsync("setup closed — nothing was changed");
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        Assert.Equal(settings, File.ReadAllBytes(SettingsPath(host)));
        Assert.Equal(providers, File.ReadAllBytes(ProviderPath(host)));
        Assert.Empty(host.Client.Requests);

        host.Console.Complete();
        await run;
    }

    [PosixFact]
    public async Task A_first_run_keeps_the_full_walk()
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Select provider");
        Assert.DoesNotContain(
            "Setup — what do you want to configure?",
            host.Frames,
            StringComparison.Ordinal
        );

        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;
    }

    [PosixFact]
    public async Task Add_a_provider_walks_provider_credential_and_test_then_writes_only_those()
    {
        using var host = ConfiguredHost();
        byte[] settings = File.ReadAllBytes(SettingsPath(host));
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        host.Console.SendEnter(); // the default row: Add a provider
        await host.WaitForFrameAsync("Select provider");
        for (var down = 0; down < 3; down++)
        {
            host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        }

        await host.WaitForFrameAsync("> deepseek");
        // The picker's arm window restarts on each Down; Enter before it expires is dropped.
        host.Advance(500);
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for deepseek");
        host.Console.SendEnter(); // the masked prompt
        await host.WaitForFrameAsync("API key for deepseek");
        host.Console.SendText(Credential);
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test (fails: no transport in this run)
        await host.WaitForFrameAsync("connection test failed");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        host.Console.SendEnter(); // skip the test for now

        string summary = await host.WaitForScrollbackAsync("skipped:");
        Assert.Contains("provider: deepseek", summary, StringComparison.Ordinal);
        Assert.Contains("credential: the key you typed", summary, StringComparison.Ordinal);
        Assert.Contains(
            "skipped: model, model check, chat profile, fast usage",
            summary,
            StringComparison.Ordinal
        );

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter(); // the default row writes

        host.Console.Complete();
        await run;
        await host.WaitUntilAsync(() => File.Exists(host.Temp.File("auth.json")));
        await host.WaitUntilAsync(() =>
            File.Exists(ProviderPath(host))
            && File.ReadAllText(ProviderPath(host)).Contains("deepseek", StringComparison.Ordinal)
        );

        string providers = File.ReadAllText(ProviderPath(host));
        string auth = File.ReadAllText(host.Temp.File("auth.json"));
        Assert.Contains("deepseek", providers, StringComparison.Ordinal);
        Assert.Contains(Credential, auth, StringComparison.Ordinal);
        Assert.DoesNotContain(Credential, providers, StringComparison.Ordinal);
        Assert.Equal(settings, File.ReadAllBytes(SettingsPath(host)));
    }

    [PosixFact]
    public async Task Add_a_profile_prefills_the_current_provider_and_writes_a_new_profile()
    {
        using var host = ConfiguredHost();
        byte[] providers = File.ReadAllBytes(ProviderPath(host));
        byte[]? auth = AuthSnapshot(host);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> Add a profile");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model (type to search");
        Assert.Contains("gpt-4o (openai-chat)", host.LastFrame, StringComparison.Ordinal);
        host.Console.SendText("gpt-4o-mini");
        await host.WaitForFrameAsync("> gpt-4o-mini");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model check: estimated cost");
        host.Console.SendEnter(); // the default row skips the check
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("helper");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip
        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEnter(); // skip

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        Assert.Contains(
            "model: gpt-4o-mini (family openai-chat)",
            summary,
            StringComparison.Ordinal
        );
        Assert.Contains("credential: auth:openai", summary, StringComparison.Ordinal);
        Assert.Contains("chat profile: helper", summary, StringComparison.Ordinal);
        Assert.Contains(
            "skipped: connection test, fast usage, decision profile, decisions settings",
            summary,
            StringComparison.Ordinal
        );

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();

        host.Console.Complete();
        await run;
        await host.WaitUntilAsync(() =>
            File.ReadAllText(SettingsPath(host)).Contains("\"helper\"", StringComparison.Ordinal)
        );

        string raw = File.ReadAllText(SettingsPath(host));
        using JsonDocument store = JsonDocument.Parse(raw);
        Assert.True(store.RootElement.TryGetProperty("profiles", out JsonElement profiles), raw);
        JsonElement helper = default;
        Assert.True(profiles.TryGetProperty("helper", out helper), raw);
        Assert.Equal("openai", helper.GetProperty("provider").GetString());
        Assert.Equal("gpt-4o-mini", helper.GetProperty("model").GetString());
        Assert.Equal(
            "chat-main",
            store.RootElement.GetProperty("usages").GetProperty("chat").GetString()
        );
        Assert.Equal(providers, File.ReadAllBytes(ProviderPath(host)));
        Assert.Equal(auth, AuthSnapshot(host));
    }

    [PosixFact]
    public async Task Change_the_chat_profile_repoints_the_usage_only()
    {
        using var host = ConfiguredHost();
        byte[] providers = File.ReadAllBytes(ProviderPath(host));
        byte[]? auth = AuthSnapshot(host);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> Change the chat profile");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Profile to use for chat");
        Assert.Contains("> chat-main (current)", host.LastFrame, StringComparison.Ordinal);
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        host.Console.SendEnter(); // chat-alt
        string summary = await host.WaitForScrollbackAsync("confirm these values");
        Assert.Contains("model: gpt-4.1 (family openai-chat)", summary, StringComparison.Ordinal);
        Assert.Contains("chat usage: chat-alt", summary, StringComparison.Ordinal);
        Assert.Contains("skipped: connection test, model check", summary, StringComparison.Ordinal);

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();

        host.Console.Complete();
        await run;
        await host.WaitUntilAsync(() =>
            File.ReadAllText(SettingsPath(host))
                .Contains("\"chat\": \"chat-alt\"", StringComparison.Ordinal)
        );

        string settings = File.ReadAllText(SettingsPath(host));
        Assert.Contains("\"chat\": \"chat-alt\"", settings, StringComparison.Ordinal);
        Assert.Contains("\"chat-main\"", settings, StringComparison.Ordinal);
        Assert.Equal(providers, File.ReadAllBytes(ProviderPath(host)));
        Assert.Equal(auth, AuthSnapshot(host));
    }

    [PosixFact]
    public async Task Change_the_chat_profile_to_the_current_profile_writes_nothing()
    {
        using var host = ConfiguredHost();
        byte[] settings = File.ReadAllBytes(SettingsPath(host));
        byte[] providers = File.ReadAllBytes(ProviderPath(host));
        byte[]? auth = AuthSnapshot(host);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> Add a profile");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> Change the chat profile");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Profile to use for chat");
        host.Console.SendEnter(); // the current profile: no change
        await host.WaitForFrameAsync("setup cancelled — nothing was changed");

        host.Console.Complete();
        await run;
        await host.WaitUntilAsync(() => !host.Session.IsRunning);

        Assert.Equal(settings, File.ReadAllBytes(SettingsPath(host)));
        Assert.Equal(providers, File.ReadAllBytes(ProviderPath(host)));
        Assert.Equal(auth, AuthSnapshot(host));
    }

    [PosixFact]
    public async Task Edit_the_decisions_layer_walks_only_the_decisions_step()
    {
        using var host = ConfiguredHost();
        byte[] providers = File.ReadAllBytes(ProviderPath(host));
        byte[]? auth = AuthSnapshot(host);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> Edit the decisions layer");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Decisions settings");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> approval gate off — screen staged actions");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("The approval gate (decisions.approvals)");
        host.Advance(500);
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> shadow");
        host.Advance(500);
        host.Console.SendEnter();
        string summary = await host.WaitForScrollbackAsync("confirm these values");
        Assert.Contains("decisions.approvals: shadow", summary, StringComparison.Ordinal);
        Assert.Contains(
            "skipped: provider, credential, connection test",
            summary,
            StringComparison.Ordinal
        );

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();

        host.Console.Complete();
        await run;
        await host.WaitUntilAsync(() =>
            File.ReadAllText(SettingsPath(host)).Contains("\"shadow\"", StringComparison.Ordinal)
        );

        string settings = File.ReadAllText(SettingsPath(host));
        Assert.Contains("\"shadow\"", settings, StringComparison.Ordinal);
        Assert.Equal(providers, File.ReadAllBytes(ProviderPath(host)));
        Assert.Equal(auth, AuthSnapshot(host));
    }

    private static HttpResponseMessage Response(int status, string body) =>
        new((System.Net.HttpStatusCode)status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

    private static InteractiveSessionHost ConfiguredHost()
    {
        var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler((_, _) => Task.FromResult(Response(401, "{}")))
        );
        bool valid = ProviderEntry.TryCreate(
            "openai",
            "openai",
            new Uri("https://provider.example.test/v1"),
            "auth:openai",
            headers: null,
            region: null,
            out ProviderEntry? entry,
            out string? error
        );
        Assert.True(valid, error);
        ProviderStore.Save(ProviderPath(host), [entry!]);
        File.WriteAllText(host.Temp.File("settings.json"), ConfiguredSettings);
        return host;
    }

    private static string SettingsPath(InteractiveSessionHost host) =>
        AuthStore.SettingsFilePath(host.Temp.File("auth.json"));

    private static string ProviderPath(InteractiveSessionHost host) =>
        AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"));

    /// <summary>The auth store's bytes when the walk has one to compare, else null: an entry that
    /// writes nothing must also create nothing (auth.json's absence is its pre-run state).</summary>
    private static byte[]? AuthSnapshot(InteractiveSessionHost host)
    {
        string path = host.Temp.File("auth.json");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}
