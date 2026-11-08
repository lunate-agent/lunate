using System.Net;
using System.Text.Json;
using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The wizard's trigger and walk through the real component stack (spec: The wizard trigger; The
/// full walk; Nothing written before the summary confirms): a fresh machine opens the walk and its
/// header prints no fix, a configured one starts normally, a scripted walk writes the stores only
/// at the confirmed summary, an abort leaves every file byte-identical, and a discovery failure
/// names the shape and re-opens the credential step.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardSessionTests
{
    private const string Credential = "sk-test-credential-please-never-echo";

    private const string DiscoveryBody =
        """{"data":[{"id":"gpt-3.5-turbo","limit":{"context":32000},"capabilities":{"reasoning":{"supported":false}}}]}""";

    private static HttpResponseMessage Response(int status, string body) =>
        new((HttpStatusCode)status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

    private static string SettingsPath(InteractiveSessionHost host) =>
        AuthStore.SettingsFilePath(host.Temp.File("auth.json"));

    private static string ProviderPath(InteractiveSessionHost host) =>
        AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"));

    [PosixFact]
    public async Task A_fresh_machine_opens_the_wizard_and_the_header_prints_no_fix()
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true)
        { };
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");

        string header = await host.WaitForScrollbackAsync("lunate");
        Assert.Contains(
            DisplayPath.For(Workspace.Canonicalize(host.Temp.Root)),
            header.Replace("\n", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "run /auth to configure a provider",
            header,
            StringComparison.Ordinal
        );

        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;
    }

    [PosixFact]
    public async Task A_configured_machine_starts_with_no_wizard()
    {
        using var host = ConfiguredHost();
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);
        host.Advance(200);

        Assert.DoesNotContain("Select provider", host.Frames, StringComparison.Ordinal);
        string header = await host.WaitForScrollbackAsync("lunate");
        Assert.DoesNotContain("setup —", header, StringComparison.Ordinal);

        host.Console.SendText("hello");
        await host.WaitUntilAsync(() => host.Session.Input.Text == "hello");
        host.Console.Complete();
        await run;
    }

    [PosixFact]
    public async Task The_walk_writes_the_stores_only_at_the_confirmed_summary()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler(
                (_, _) => Task.FromResult(Response(200, DiscoveryBody))
            )
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await WalkToSummary(host);
        Assert.False(File.Exists(SettingsPath(host)), "nothing is written before confirmation");
        Assert.False(File.Exists(ProviderPath(host)));
        Assert.False(File.Exists(host.Temp.File("auth.json")));

        await host.WaitForScrollbackAsync("confirm these values");
        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        // The walk switched the chat model and then posts its commit notice; the slot holds one
        // notice, so the switch line is read from the journal rather than a frame (which the commit
        // notice, posted after it, can replace before it is painted).
        await host.WaitForNoticeAsync("model: gpt-3.5-turbo");

        host.Console.Complete();
        await run;

        string providers = File.ReadAllText(ProviderPath(host));
        string settings = File.ReadAllText(SettingsPath(host));
        string auth = File.ReadAllText(host.Temp.File("auth.json"));
        Assert.Contains(Credential, auth, StringComparison.Ordinal);
        Assert.DoesNotContain(Credential, providers, StringComparison.Ordinal);
        Assert.DoesNotContain(Credential, settings, StringComparison.Ordinal);
        Assert.Contains("usages", settings, StringComparison.Ordinal);
        Assert.Contains("gpt-3.5-turbo", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("gpt-3.5-turbo", providers, StringComparison.Ordinal);
    }

    [PosixFact]
    public async Task An_abort_at_the_summary_leaves_every_file_byte_identical()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler(
                (_, _) => Task.FromResult(Response(200, DiscoveryBody))
            )
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await WalkToSummary(host);
        Assert.False(File.Exists(SettingsPath(host)));

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;

        Assert.False(File.Exists(SettingsPath(host)), "an abort writes nothing");
        Assert.False(File.Exists(ProviderPath(host)), "an abort writes nothing");
        Assert.False(File.Exists(host.Temp.File("auth.json")), "an abort writes nothing");
    }

    [PosixFact]
    public async Task A_discovery_failure_names_authentication_and_reopens_the_credential_step()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler((_, _) => Task.FromResult(Response(401, "{}")))
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText(Credential);
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("connection test failed");
        Assert.Contains("authentication", host.Frames, StringComparison.Ordinal);
        Assert.False(File.Exists(ProviderPath(host)));

        host.Console.SendEnter(); // the go-back row (the credential step asks again)
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter(); // the "enter a key" row of the re-opened step
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);

        host.Console.Complete();
        await run;

        Assert.False(File.Exists(ProviderPath(host)), "a cancelled walk writes nothing");
    }

    [PosixFact]
    public async Task An_empty_credential_reference_is_refused_and_the_step_stays_open()
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("name an env: or auth: reference instead");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential reference");
        host.Console.SendText("env:");
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Credential for openai");
        Assert.Contains("a credential reference is", host.Frames, StringComparison.Ordinal);
        Assert.DoesNotContain("connection test failed", host.Frames, StringComparison.Ordinal);
        Assert.False(File.Exists(ProviderPath(host)));
        Assert.False(File.Exists(host.Temp.File("auth.json")));

        host.Console.SendEscape();
        host.Console.Complete();
        await run;
    }

    [PosixFact]
    public async Task A_chat_profile_named_decisions_is_not_replaced_by_the_decision_profile()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler(
                (_, _) => Task.FromResult(Response(200, DiscoveryBody))
            )
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await WalkToFastStepAsync(host, "decisions");
        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip

        // The decision profile writes the fixed name `decisions`; a chat profile already carrying
        // that name would be replaced at commit time, leaving `usages.chat` pointing at a decision
        // profile (#585 N-5). The step names the clash and offers no decision profile at all.
        await host.WaitForFrameAsync("the chat profile in this walk is named 'decisions'");
        Assert.DoesNotContain("Decision profile (", host.Frames, StringComparison.Ordinal);

        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // accept the defaults (nothing written for the layer)

        string summary = Flatten(await host.WaitForScrollbackAsync("confirm these values"));
        Assert.Contains("chat profile: decisions", summary, StringComparison.Ordinal);
        Assert.Contains("skipped: fast usage, decision profile", summary, StringComparison.Ordinal);

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("model: gpt-3.5-turbo");
        host.Console.Complete();
        await run;

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath(host)));
        JsonElement root = settings.RootElement;
        Assert.Equal(
            "gpt-3.5-turbo",
            root.GetProperty("profiles").GetProperty("decisions").GetProperty("model").GetString()
        );
        Assert.Equal("decisions", root.GetProperty("usages").GetProperty("chat").GetString());
        Assert.False(
            root.TryGetProperty("decisions", out JsonElement layer)
                && layer.TryGetProperty("profile", out _),
            "the skipped decision step writes no decisions.profile"
        );
    }

    private static InteractiveSessionHost ConfiguredHost()
    {
        var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
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
        File.WriteAllText(
            host.Temp.File("settings.json"),
            """
            {
              "schemaVersion": 2,
              "model": "gpt-4.1",
              "profiles": { "chat-main": { "provider": "openai", "model": "gpt-4.1" } },
              "usages": { "chat": "chat-main" }
            }
            """
        );
        return host;
    }

    private static async Task WalkToSummary(InteractiveSessionHost host)
    {
        await WalkToFastStepAsync(host, "chat-main");

        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // accept the defaults (nothing written for the layer)

        // gpt-3.5-turbo declares no reasoning style in the listing or the catalog, so the walk
        // never asks the thinking question (spec: Thinking offers only supported levels).
        Assert.DoesNotContain("Thinking level for", host.Frames, StringComparison.Ordinal);
    }

    /// <summary>Walks provider, credential, connection test, model, model check and the chat
    /// profile name — everything up to the fast-usage question.</summary>
    private static async Task WalkToFastStepAsync(InteractiveSessionHost host, string profile)
    {
        await host.ChooseDetailedModeAsync();
        host.Console.SendEnter(); // the first preset row (openai)

        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText(Credential);
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test

        // The connection test passed; the model picker lists the discovery response.
        await host.WaitForFrameAsync("Model (type to search");
        Assert.Contains("gpt-3.5-turbo", host.Frames, StringComparison.Ordinal);
        host.Console.SendEnter(); // the listed model

        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // the default row skips: the model stays unverified but staged

        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText(profile);
        host.Console.SendEnter();
    }

    private static string Flatten(string text) =>
        string.Join(
            " ",
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim())
        );
}
