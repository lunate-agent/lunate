using System.Net;
using System.Text;
using System.Text.Json;
using Lunate.Ai;
using Lunate.Tui;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Lunate.Coding.Tests;

/// <summary>
/// The three usage routes driven through the wizard itself (spec: Fast and decision usages from any
/// provider): the fast step picks the chat model, the fast step picks another configured provider's
/// model, and the decision step adds a provider inline and comes back to the same picker. Each test
/// drives the selection, the check and the commit over the scripted console and the temp stores, so
/// the picker's own rows are what the route is proved against — not a hand-recorded answer.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardUsageWalkTests
{
    private const string Credential = "usage-walk-key-please-never-echo";

    private const string SetupWrittenPrefix = "setup written:";

    private const string DiscoveryBody =
        """{"data":[{"id":"gpt-3.5-turbo","limit":{"context":32000},"capabilities":{"reasoning":{"supported":false}}}]}""";

    /// <summary>Route 1: the fast step offers the model the chat profile already runs; picking it
    /// reuses the chat profile — no second profile, no second check, no second request.</summary>
    [PosixFact]
    public async Task Fast_from_the_provider_configured_in_this_run_reuses_the_chat_profile()
    {
        var factory = new RecordingFactory(new ScriptedChatClient().Enqueue(Scripts.Text("OK")));
        using var host = new InteractiveSessionHost(
            factory: factory,
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler((_, _) => Task.FromResult(Response(200)))
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await WalkToFastStepAsync(host, sendModelCheck: true);

        // The chat model's row is in the flat list under its provider; picking it is the answer.
        host.Console.SendText("gpt-3.5-turbo");
        await host.WaitForFrameAsync("> openai/gpt-3.5-turbo (openai-chat)");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Add another provider?");
        host.Console.SendEnter(); // Done: the provider list closes before the decision step
        await host.WaitForFrameAsync("Decision profile");
        Assert.DoesNotContain("Fast usage check", host.Frames, StringComparison.Ordinal);
        host.Console.SendEnter(); // the decision step's default: skip
        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // the default row: Done

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        string flat = Flatten(summary);
        // The reuse route binds the chat profile as the fast usage and writes no second profile.
        Assert.Contains("usage fast profile: chat-main", flat, StringComparison.Ordinal);
        Assert.Contains("model check: passed", flat, StringComparison.Ordinal);

        await ConfirmAndWaitAsync(host);
        host.Console.Complete();
        await run;

        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(SettingsPath(host)));
        JsonElement usages = store.RootElement.GetProperty("usages");
        Assert.Equal("chat-main", usages.GetProperty("chat").GetString());
        Assert.Equal("chat-main", usages.GetProperty("fast").GetString());
        Assert.False(store.RootElement.GetProperty("profiles").TryGetProperty("fast", out _));
        // One check, for the chat model: the reuse never sent a second request.
        Assert.Single(factory.Created);
    }

    /// <summary>Route 2: a provider already configured on the machine supplies the fast model. The
    /// fast step's own check resolves that provider's entry and stored credential, and the commit
    /// writes its profile and the usage — the run's own chat answer untouched.</summary>
    [PosixFact]
    public async Task Fast_from_another_configured_provider_runs_the_check_and_writes_its_profile()
    {
        var factory = new RecordingFactory(new ScriptedChatClient().Enqueue(Scripts.Text("OK")));
        using var host = TwoProviderHost(factory);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);
        await OpenFullWalkAsync(host);

        await WalkToFastStepAsync(host, sendModelCheck: false);

        host.Console.SendText("claude-sonnet-5-5");
        await host.WaitForFrameAsync(
            "> anthropic/claude-sonnet-5-5 (anthropic-messages, thinking budget)"
        );
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Fast usage check: estimated cost");
        host.Console.SendUp(); // the default row is the skip
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Add another provider?");
        host.Console.SendEnter(); // Done
        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEnter(); // the decision step's default: skip
        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // the default row: Done

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        string flat = Flatten(summary);
        Assert.Contains("profile fast", flat, StringComparison.Ordinal);
        Assert.Contains("provider: anthropic", flat, StringComparison.Ordinal);
        Assert.Contains("model: claude-sonnet-5-5", flat, StringComparison.Ordinal);
        Assert.Contains("usage fast profile: fast", flat, StringComparison.Ordinal);
        Assert.Contains("fast check: passed", flat, StringComparison.Ordinal);

        await ConfirmAndWaitAsync(host);
        host.Console.Complete();
        await run;

        // The check asked the anthropic entry for its own credential, not the run's typed key.
        (ModelInfo created, string? key) = Assert.Single(factory.Created);
        Assert.Equal("claude-sonnet-5-5", created.Id);
        Assert.Equal("anthropic-key", key);

        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(SettingsPath(host)));
        JsonElement root = store.RootElement;
        Assert.Equal("fast", root.GetProperty("usages").GetProperty("fast").GetString());
        Assert.Equal("chat-main", root.GetProperty("usages").GetProperty("chat").GetString());
        JsonElement fast = root.GetProperty("profiles").GetProperty("fast");
        Assert.Equal("anthropic", fast.GetProperty("provider").GetString());
        Assert.Equal("claude-sonnet-5-5", fast.GetProperty("model").GetString());
        JsonElement chat = root.GetProperty("profiles").GetProperty("chat-main");
        Assert.Equal("openai", chat.GetProperty("provider").GetString());
    }

    /// <summary>Route 3: the decision step's inline row runs the provider sub-walk, returns to the
    /// same picker with the earlier answers intact, and the added provider, its typed key and the
    /// native decision profile land in one review and one commit.</summary>
    [PosixFact]
    public async Task Decision_from_a_provider_set_up_inline_returns_to_the_picker_and_commits()
    {
        using var handler = new UsageRouteHandler();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        var factory = new ChatClientFactory(loggerFactory) { BaseTransportFactory = () => handler };
        using var host = new InteractiveSessionHost(
            factory: factory,
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler((_, _) => Task.FromResult(Response(200)))
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await WalkToFastStepAsync(host, sendModelCheck: false);
        host.Console.SendEnter(); // the fast step's default: skip
        await host.WaitForFrameAsync("Add another provider?");
        host.Console.SendEnter(); // Done
        await host.WaitForFrameAsync("Decision profile");

        // The inline row is part of the same picker, not a separate step.
        host.Console.SendText("inline");
        await host.WaitForFrameAsync("> Set up another provider inline…");
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Select provider");
        await PickProviderRowAsync(host, "opencode-go — OpenCode Go");
        await host.WaitForFrameAsync("Credential for opencode-go");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for opencode-go");
        host.Console.SendText("inline-go-key");
        await host.WaitForFrameAsync(
            "API key for opencode-go: " + new string('*', "inline-go-key".Length)
        );
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> Skip the connection test");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model (discovery listed nothing");
        host.Console.SendEnter(); // "enter a model id manually"
        await host.WaitForFrameAsync("Model id");
        host.Console.SendText("opencode-go/jev-1.13");
        host.Console.SendEnter();

        // The sub-walk returns to the usage picker it was opened from, with its new row.
        await host.WaitForNoticeAsync(
            "provider 'opencode-go' added to this setup run; pick its model for the usage"
        );
        await host.WaitForFrameAsync("Decision profile (the provider exposes the native Jev");
        host.Console.SendText("jev");
        await host.WaitForFrameAsync("> opencode-go/jev-1.13 (native Jev");
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Decision check: estimated cost");
        host.Console.SendUp(); // the default row is the skip
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // the default row: Done

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        string flat = Flatten(summary);
        // The inline provider is a providers.json entry, its typed key an auth.json one, and the
        // native decision profile (family systemone) lands under settings.json.
        Assert.Contains("provider opencode-go", flat, StringComparison.Ordinal);
        Assert.Contains("key opencode-go", flat, StringComparison.Ordinal);
        Assert.Contains("profile decisions", flat, StringComparison.Ordinal);
        Assert.Contains("model: opencode-go/jev-1.13", flat, StringComparison.Ordinal);
        Assert.Contains("api family: systemone", flat, StringComparison.Ordinal);
        Assert.Contains("decision check: passed", flat, StringComparison.Ordinal);
        // The answers the step already had survive the sub-walk.
        Assert.Contains("profile chat-main", flat, StringComparison.Ordinal);
        Assert.Contains(
            "model check: skipped — the model stays unverified",
            flat,
            StringComparison.Ordinal
        );
        Assert.Contains("left at default / unchanged", flat, StringComparison.Ordinal);
        Assert.Contains("decisions.approvals:", flat, StringComparison.Ordinal);

        await ConfirmAndWaitAsync(host);
        host.Console.Complete();
        await run;

        Assert.Equal(1, handler.SystemOneRequests);
        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(SettingsPath(host)));
        JsonElement root = store.RootElement;
        Assert.Equal("chat-main", root.GetProperty("usages").GetProperty("chat").GetString());
        Assert.True(root.GetProperty("profiles").TryGetProperty("chat-main", out _));
        JsonElement decision = root.GetProperty("profiles").GetProperty("decisions");
        Assert.Equal("opencode-go", decision.GetProperty("provider").GetString());
        Assert.Equal("opencode-go/jev-1.13", decision.GetProperty("model").GetString());
        Assert.Equal("systemone", decision.GetProperty("apiFamily").GetString());
        Assert.Equal("decisions", root.GetProperty("decisions").GetProperty("profile").GetString());

        AuthStore auth = AuthStore.Load(host.Temp.File("auth.json"), NoEnvironment);
        Assert.Equal("inline-go-key", auth.TryGet("opencode-go"));
        ProviderEntry[] entries = [.. ProviderStore.Load(ProviderPath(host), auth, []).Entries];
        Assert.Contains(entries, entry => entry.Name == "opencode-go");
    }

    /// <summary>Dismisses the start's own offer (the auth picker) and opens the full walk through
    /// the entry menu's "Walk everything": a machine with a configured provider is a configured
    /// install, so <c>/setup</c> offers the menu and the menu's last row walks every step.</summary>
    private static async Task OpenFullWalkAsync(InteractiveSessionHost host)
    {
        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        foreach (
            string row in new[]
            {
                "> Add a profile",
                "> Change the chat profile",
                "> Edit the decisions layer",
                "> Walk everything",
            }
        )
        {
            host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
            await host.WaitForFrameAsync(row);
        }

        // The picker's arm window restarts on each Down; Enter before it expires is dropped.
        host.Advance(500);
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Select provider");
    }

    /// <summary>The full walk up to the fast step: openai, its key, the connection test, the listed
    /// model, the model check (sent or skipped) and the chat profile name.</summary>
    private static async Task WalkToFastStepAsync(InteractiveSessionHost host, bool sendModelCheck)
    {
        await host.WaitForFrameAsync("Select provider");
        // Quick ends at the chat profile; the usages are Detailed-only steps, so the walk
        // switches depth first — the mode row sits above the presets and keeps the walk in place.
        host.Console.SendUp();
        await host.WaitForFrameAsync("> Setup mode: quick");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("> openai — OpenAI (region not specified)");
        host.Console.SendEnter(); // openai

        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText(Credential);
        await host.WaitForFrameAsync("API key for openai: " + new string('*', Credential.Length));
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test

        await host.WaitForFrameAsync("Model (type to search");
        host.Console.SendEnter(); // the listed model

        await host.WaitForFrameAsync("Model check: estimated cost");
        if (sendModelCheck)
        {
            host.Console.SendUp(); // the default row is the skip
        }

        host.Console.SendEnter();
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-main");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("fast usage");
    }

    /// <summary>Moves the provider picker to the row and confirms it. The preset list is short,
    /// so bare letters filter only above the threshold; navigation is the documented path.</summary>
    private static async Task PickProviderRowAsync(InteractiveSessionHost host, string row)
    {
        string previous = host.LastFrame;
        while (!host.LastFrame.Contains($"> {row}", StringComparison.Ordinal))
        {
            host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
            await host.WaitUntilAsync(() =>
            {
                host.Advance(33);
                return host.LastFrame != previous;
            });
            previous = host.LastFrame;
        }

        host.Console.SendEnter();
    }

    /// <summary>Confirms the summary and waits for the walk's one commit notice.</summary>
    private static async Task ConfirmAndWaitAsync(InteractiveSessionHost host)
    {
        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        await host.WaitUntilAsync(() =>
            host.Notices.Entries.Any(entry =>
                entry.Text.StartsWith(SetupWrittenPrefix, StringComparison.Ordinal)
            )
        );
    }

    /// <summary>A machine with two configured providers and no settings file: the run's openai
    /// provider and an anthropic entry whose key comes from the environment (a stored key would be
    /// offered first at the credential step and change what the walk types).</summary>
    private static InteractiveSessionHost TwoProviderHost(RecordingFactory factory)
    {
        Func<string, string?> environment = PrintModeTestSupport.Environment();
        var host = new InteractiveSessionHost(
            factory: factory,
            interactiveConsole: true,
            freshInstall: true,
            environment: name => name == "ANTHROPIC_API_KEY" ? "anthropic-key" : environment(name),
            discoveryTransport: new RecordingHandler((_, _) => Task.FromResult(Response(200)))
        );
        ProviderStore.Save(
            ProviderPath(host),
            [Entry("openai", "auth:openai"), Entry("anthropic", "env:ANTHROPIC_API_KEY")]
        );
        return host;
    }

    /// <summary>The whole frame is one logical line per screen; assertions read it as one line so a
    /// wrap at 80 columns cannot split the text they look for.</summary>
    private static string Flatten(string text) =>
        string.Join(
            " ",
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim())
        );

    private static HttpResponseMessage Response(int status) =>
        new((HttpStatusCode)status)
        {
            Content = new StringContent(DiscoveryBody, Encoding.UTF8, "application/json"),
        };

    private static string? NoEnvironment(string name) => null;

    private static string SettingsPath(InteractiveSessionHost host) =>
        AuthStore.SettingsFilePath(host.Temp.File("auth.json"));

    private static string ProviderPath(InteractiveSessionHost host) =>
        AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"));

    private static ProviderEntry Entry(string name, string credential)
    {
        bool valid = ProviderEntry.TryCreate(
            name,
            name,
            new Uri($"https://{name}.example.test/v1"),
            credential,
            headers: null,
            region: null,
            out ProviderEntry? entry,
            out string? error
        );
        Assert.True(valid, error);
        return entry!;
    }

    /// <summary>Records the model and the credential the check resolved through the factory.</summary>
    private sealed class RecordingFactory(IChatClient client) : IChatClientFactory
    {
        public List<(ModelInfo Model, string? ApiKey)> Created { get; } = [];

        public IChatClient Create(ModelInfo model)
        {
            Created.Add((model, null));
            return client;
        }

        public IChatClient Create(ModelInfo model, string? apiKey)
        {
            Created.Add((model, apiKey));
            return client;
        }
    }

    /// <summary>The inline provider's decision transport: the native check's own request shape, so
    /// the route cannot silently take the adapter path.</summary>
    private sealed class UsageRouteHandler : HttpMessageHandler
    {
        public int SystemOneRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            _ = await request.Content!.ReadAsStringAsync(cancellationToken);
            bool systemOne = request.RequestUri!.AbsolutePath.EndsWith(
                "/systemone",
                StringComparison.Ordinal
            );
            if (systemOne)
            {
                SystemOneRequests++;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    systemOne
                        ? """{"model":"jev-1.13","answers":{"transport_reachable":{"type":"noul","noul":0.91}}}"""
                        : """{"choices":[{"message":{"role":"assistant","content":"OK"},"finish_reason":"stop"}]}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        }
    }
}
