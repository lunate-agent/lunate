using System.Net;
using System.Text;
using System.Text.Json;
using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The model step's provider-scoped resolution (spec: The model step resolves the id in the walked
/// provider's scope): a discovered bare id the catalogue holds under several providers completes the
/// walk instead of aborting, an id one catalogue entry answers is staged without a prompt, and an id
/// several unrelated providers serve is offered as qualified ids whose chosen one is written (#675).
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardAmbiguousModelTests
{
    private const string Credential = "ambiguous-model-test-key-please-never-echo";

    [PosixFact]
    public async Task A_custom_endpoint_model_one_catalog_entry_answers_is_written_without_a_prompt()
    {
        // `kimi-k2` is one catalogue entry's wire id (`opencode/kimi-k2`), so the walked provider's
        // scope answers it: no qualified-id picker, and the listing's id is staged as given.
        using var host = CustomEndpointHost("kimi-k2");
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await WalkCustomEndpointToModelAsync(host);
        host.Console.SendEnter(); // the listed model

        await host.WaitForFrameAsync("Model check");
        Assert.DoesNotContain("several catalogue providers", host.Frames, StringComparison.Ordinal);
        host.Console.SendEnter(); // the default row skips the check

        await FinishWalkAsync(host);
        host.Console.Complete();
        await run;

        Assert.DoesNotContain("setup failed", host.Frames, StringComparison.Ordinal);
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath(host)));
        Assert.Equal(
            "kimi-k2",
            settings
                .RootElement.GetProperty("profiles")
                .GetProperty("chat-main")
                .GetProperty("model")
                .GetString()
        );
    }

    [PosixFact]
    public async Task An_id_several_unrelated_providers_serve_offers_the_qualified_ids_and_writes_the_chosen_one()
    {
        // The `openai` preset owns none of the three entries the catalog holds this id under, so
        // the qualified ids are offered and the chosen one is what the profile records.
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            secretPrompt: new ScriptedPrompt(Credential, "kimi-k2.7-code", "chat-main"),
            discoveryTransport: Response("""{"data":[{"id":"gpt-3.5-turbo"}]}""")
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.ChooseDetailedModeAsync();
        host.Console.SendEnter(); // openai
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("Model (type to search");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> enter a model id manually");
        host.Console.SendEnter();

        // The visible prompt reads the typed id from the scripted answer queue.
        await host.WaitForFrameAsync("several catalogue providers");
        await host.WaitForFrameAsync("> moonshotai/kimi-k2.7-code (openai-chat)");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> opencode/kimi-k2.7-code (openai-chat)");
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // the default row skips the check
        await host.WaitForFrameAsync("Thinking level for opencode/kimi-k2.7-code");
        host.Console.SendEnter(); // the default level
        await FinishWalkAsync(host);
        host.Console.Complete();
        await run;

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath(host)));
        Assert.Equal(
            "opencode/kimi-k2.7-code",
            settings
                .RootElement.GetProperty("profiles")
                .GetProperty("chat-main")
                .GetProperty("model")
                .GetString()
        );
    }

    [PosixFact]
    public async Task A_custom_endpoint_model_the_catalog_holds_under_several_providers_completes_the_walk()
    {
        // The reported regression (#675): the custom-endpoint route's discovery listing serves bare
        // wire ids, and this one is three catalogue entries' wire id. The walk must run to the
        // summary without asking and write a configuration the next session can resolve.
        using var host = CustomEndpointHost("kimi-k2.7-code");
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await WalkCustomEndpointToModelAsync(host);
        host.Console.SendEnter(); // the listed model

        await host.WaitForFrameAsync("Model check");
        Assert.DoesNotContain("several catalogue providers", host.Frames, StringComparison.Ordinal);
        Assert.DoesNotContain("is ambiguous", host.Frames, StringComparison.Ordinal);
        host.Console.SendEnter(); // the default row skips the check

        await FinishWalkAsync(host);
        host.Console.Complete();
        await run;

        Assert.DoesNotContain("setup failed", host.Frames, StringComparison.Ordinal);
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath(host)));
        Assert.Equal(
            "kimi-k2.7-code",
            settings
                .RootElement.GetProperty("profiles")
                .GetProperty("chat-main")
                .GetProperty("model")
                .GetString()
        );
        Assert.Equal(
            "kimi-k2.7-code",
            settings
                .RootElement.GetProperty("providers")
                .GetProperty("local")
                .GetProperty("model")
                .GetString()
        );

        // The written configuration resolves on the next start: the profile's bare id resolves
        // through its own provider entry, and the entry's endpoint and wire id survive.
        AgentSettings written = SettingsStore.Resolve(SettingsPath(host), _ => null);
        AuthStore auth = AuthStore.Load(host.Temp.File("auth.json"));
        ProviderStore providers = ProviderStore.Load(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json")),
            auth,
            written.Providers
        );
        ResolvedModelProfile resolved = ChatModelResolver.ResolveChatProfile(
            written,
            providers.Entries,
            HarnessFactory.LoadCatalog(written, host.Temp.File("models.json"))
        )!;
        Assert.Equal("kimi-k2.7-code", resolved.Model.WireId);
        Assert.Equal("https://local.example.test/v1", resolved.Provider.BaseUrl.ToString());
    }

    [Fact]
    public void A_custom_endpoints_profile_resolves_on_the_next_start_when_the_entry_owns_no_catalogue_entry()
    {
        // The reported case (#708): the endpoint's entry lives in the provider store, so the
        // settings-section catalog carries no `local/kimi-k2`; the profile names the bare id the
        // endpoint serves and the catalog knows that id only under `opencode`.
        using var temp = new TempDirectory();
        File.WriteAllText(
            AuthStore.SettingsFilePath(temp.File("auth.json")),
            """
            {
              "schemaVersion": 2,
              "profiles": {
                "chat-main": { "provider": "local", "model": "kimi-k2" }
              },
              "usages": { "chat": "chat-main" }
            }
            """
        );
        ProviderStore.Save(
            AuthStore.ProviderStoreFilePath(temp.File("auth.json")),
            [LocalProvider()]
        );

        AgentSettings written = SettingsStore.Resolve(temp.File("settings.json"), _ => null);
        AuthStore auth = AuthStore.Load(temp.File("auth.json"));
        ProviderStore providers = ProviderStore.Load(
            AuthStore.ProviderStoreFilePath(temp.File("auth.json")),
            auth,
            written.Providers
        );
        ResolvedModelProfile resolved = ChatModelResolver.ResolveChatProfile(
            written,
            providers.Entries,
            HarnessFactory.LoadCatalog(written, temp.File("models.json"))
        )!;

        Assert.Equal("local/kimi-k2", resolved.Model.Id);
        Assert.Equal("kimi-k2", resolved.Model.WireId);
        Assert.Equal("https://local.example.test/v1", resolved.Provider.BaseUrl.ToString());
    }

    private static ProviderEntry LocalProvider()
    {
        bool valid = ProviderEntry.TryCreate(
            "local",
            "openai",
            new Uri("https://local.example.test/v1"),
            "auth:local",
            headers: null,
            region: null,
            out ProviderEntry? entry,
            out string? error
        );
        Assert.True(valid, error);
        return Assert.IsType<ProviderEntry>(entry);
    }

    private static string SettingsPath(InteractiveSessionHost host) =>
        AuthStore.SettingsFilePath(host.Temp.File("auth.json"));

    private static InteractiveSessionHost CustomEndpointHost(string listedModel) =>
        new(
            interactiveConsole: true,
            freshInstall: true,
            secretPrompt: new ScriptedPrompt(
                "local",
                "https://local.example.test/v1",
                "",
                "",
                Credential,
                "chat-main"
            ),
            discoveryTransport: Response($$"""{"data":[{"id":"{{listedModel}}"}]}""")
        );

    /// <summary>The custom-endpoint route up to the model picker: id, endpoint, family, placement,
    /// region, models URL, key and the connection test.</summary>
    private static async Task WalkCustomEndpointToModelAsync(InteractiveSessionHost host)
    {
        await host.ChooseDetailedModeAsync();
        for (var row = 0; row < ProviderPresets.All.Count; row++)
        {
            host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        }

        await host.WaitForFrameAsync("> Custom OpenAI-compatible endpoint…");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Custom API family");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential placement");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for local");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("Model (type to search");
    }

    /// <summary>The model check's skip row, the fast and decision profiles, the decisions defaults
    /// and the summary's confirmation, in the order the walk asks them.</summary>
    private static async Task FinishWalkAsync(InteractiveSessionHost host)
    {
        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip the fast profile
        await host.WaitForFrameAsync("Add another provider?");
        host.Console.SendEnter(); // Done
        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEnter(); // skip the decision profile
        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // accept the defaults
        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter(); // the default row writes
        await host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return File.Exists(SettingsPath(host));
        });
    }

    private static HttpMessageHandler Response(string body) =>
        new RecordingHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    }
                )
        );

    private sealed class ScriptedPrompt(params string?[] answers) : ISecretPrompt
    {
        private readonly Queue<string?> _answers = new(answers);

        public Task<string?> ReadAsync(string prompt, CancellationToken cancellationToken) =>
            Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : null);
    }
}
