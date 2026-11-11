using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// Driver-level pins for the walk's step contracts the unit and commit tests do not reach: the
/// thinking question asks only the levels the chosen model declares (from the discovery listing's
/// own metadata) and the chosen level lands in the settings file; a preset exposing the System
/// One family offers its native Jev profile while any other provider is labelled adapter; and a
/// walk that skips every step and confirms leaves every store file unwritten.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardStepPins
{
    private const string Credential = "sk-pin-credential-never-echoed";

    private const string ReasoningBody =
        """{"data":[{"id":"pin-reasoner","capabilities":{"reasoning":{"supported":true,"effort":["low","medium","high"]}},"api":{"chat-completions":{}}}]}""";

    [PosixFact]
    public async Task A_declared_thinking_level_is_offered_and_written_through_the_store()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: Recording(ReasoningBody)
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.ChooseDetailedModeAsync();
        host.Console.SendEnter(); // the first preset row (openai)
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText(Credential);
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("Model (type to search");
        host.Console.SendEnter(); // the listed pin-reasoner row
        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // the default row skips the check

        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-main");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Thinking level for pin-reasoner");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> medium");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> high");
        host.Console.SendEnter();

        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip
        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEnter(); // skip
        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // accept the defaults

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        Assert.Contains(
            "chat profile: chat-main (thinking high)",
            summary,
            StringComparison.Ordinal
        );
        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("setup written");

        string providers = File.ReadAllText(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"))
        );
        using JsonDocument settings = JsonDocument.Parse(
            File.ReadAllText(AuthStore.SettingsFilePath(host.Temp.File("auth.json")))
        );
        JsonElement profile = settings.RootElement.GetProperty("profiles").GetProperty("chat-main");
        Assert.Equal("openai", profile.GetProperty("provider").GetString());
        Assert.Equal("pin-reasoner", profile.GetProperty("model").GetString());
        Assert.Equal("high", profile.GetProperty("reasoningLevel").GetString());
        Assert.Equal(
            "chat-main",
            settings.RootElement.GetProperty("usages").GetProperty("chat").GetString()
        );
        Assert.Equal(
            "chat-main",
            settings.RootElement.GetProperty("usages").GetProperty("chat").GetString()
        );
        Assert.DoesNotContain(Credential, providers, StringComparison.Ordinal);
        Assert.DoesNotContain(Credential, host.Frames + host.Diagnostics, StringComparison.Ordinal);

        host.Console.Complete();
        await run;
    }

    [PosixFact]
    public async Task A_systemone_preset_offers_its_native_jev_profile()
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.ChooseDetailedModeAsync();
        await PickRowAsync(host, "Select provider", "openrouter — OpenRouter");
        await host.WaitForFrameAsync("Credential for openrouter");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for openrouter");
        host.Console.SendText(Credential);
        host.Console.SendEnter();
        await SkipConnectionTestAsync(host);
        await EnterManualModelAsync(host, "deep/deepseek-chat");
        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // the default row skips the check
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-deep");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Decision profile (the provider exposes the native Jev");
        host.Console.SendText("jev");
        await host.WaitForFrameAsync("> openrouter/typesafe/jev-1.14 (native Jev)");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Decision check");
        host.Console.SendEnter(); // the default row skips the check
        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // accept the defaults

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        Assert.Contains(
            "decision profile: decisions (native, pinned openrouter/typesafe/jev-1.14)",
            summary,
            StringComparison.Ordinal
        );

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEscape(); // abort at the summary: nothing is written
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;

        Assert.False(
            File.Exists(AuthStore.SettingsFilePath(host.Temp.File("auth.json"))),
            "an aborted walk writes nothing"
        );
    }

    [PosixFact]
    public async Task Opencode_go_labels_its_jev_line_with_the_pay_as_you_go_billing_notice()
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.ChooseDetailedModeAsync();
        await PickRowAsync(host, "Select provider", "opencode-go — OpenCode Go");
        await host.WaitForFrameAsync("Credential for opencode-go");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for opencode-go");
        host.Console.SendText(Credential);
        host.Console.SendEnter();
        await SkipConnectionTestAsync(host);
        await EnterManualModelAsync(host, "any-model");
        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // the default row skips the check
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-go");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Decision profile (the provider exposes the native Jev");
        host.Console.SendText("jev");
        await host.WaitForFrameAsync("> opencode-go/jev-1.13 (native Jev");
        // The picker truncates the long label at the terminal width; pin the billing notice that
        // fits before the cut (the full sentence lives in the source, not the clipped frame).
        Assert.Contains(
            "billed to your OpenCode pay-as-you-go",
            host.LastFrame,
            StringComparison.Ordinal
        );
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Decision check");
        host.Console.SendEnter(); // the default row skips the check
        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // accept the defaults

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        Assert.Contains(
            "decision profile: decisions (native, pinned opencode-go/jev-1.13)",
            summary,
            StringComparison.Ordinal
        );

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEscape(); // abort at the summary: nothing is written
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;

        Assert.False(
            File.Exists(AuthStore.SettingsFilePath(host.Temp.File("auth.json"))),
            "an aborted walk writes nothing"
        );
    }

    [PosixFact]
    public async Task A_provider_without_systemone_labels_the_decision_profile_adapter()
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.ChooseDetailedModeAsync();
        host.Console.SendEnter(); // the first preset row (openai)
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText(Credential);
        host.Console.SendEnter();
        await SkipConnectionTestAsync(host);
        await EnterManualModelAsync(host, "any-model");
        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // the default row skips the check
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-main");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync(
            "Decision profile (this provider serves no native Jev profile"
        );
        Assert.Contains(
            "Decision profile (this provider serves no native Jev profile",
            host.LastFrame,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "openai/any-model (adapter, openai-chat)",
            host.LastFrame,
            StringComparison.Ordinal
        );

        host.Console.SendEscape(); // the walk ends here, nothing written
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;

        Assert.False(File.Exists(AuthStore.SettingsFilePath(host.Temp.File("auth.json"))));
    }

    [PosixFact]
    public async Task Skip_everything_confirmed_writes_no_file_and_loads_valid()
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        // Skipping the provider short-circuits every answer that depends on it: the credential,
        // connection test, model, model check, chat profile, fast and decision steps all leave
        // their value untouched, so the walk lands on the decisions step with every dependent
        // step named skipped in the summary.
        await host.ChooseDetailedModeAsync();
        await PickRowAsync(host, "Select provider", "Skip provider setup");
        await PickRowAsync(
            host,
            "Decisions settings",
            "Done — write these choices and leave the menu"
        );

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        // The summary wraps at the terminal width; flatten the wrapped lines so the pinned
        // whole line is one readable string (the exact order is the same content the summary
        // unit test's skipped-steps case pins at 80 columns).
        string flattened = string.Join(
            " ",
            summary.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim())
        );
        Assert.Contains(
            "skipped: provider, credential, connection test, model, model check, chat profile, "
                + "fast usage, decision profile, decisions settings",
            flattened,
            StringComparison.Ordinal
        );
        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("setup written");
        // The walk posts one commit notice and nothing repaints it with a second wording: wait for
        // the sentence itself, the line a user reads, not just the shared prefix.
        await host.WaitUntilAsync(() =>
            PlainFrames(host.Frames)
                .Contains(
                    "setup written: the files this walk changed are saved now and read at "
                        + "startup; this session keeps its current composition, so the new "
                        + "settings apply from the next session.",
                    StringComparison.Ordinal
                )
        );

        Assert.False(File.Exists(AuthStore.SettingsFilePath(host.Temp.File("auth.json"))));
        Assert.False(
            File.Exists(AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"))),
            "a confirmed no-change walk writes nothing"
        );
        Assert.False(File.Exists(host.Temp.File("auth.json")));
        _ = SettingsStore.Resolve(
            AuthStore.SettingsFilePath(host.Temp.File("auth.json")),
            PrintModeTestSupport.Environment()
        );

        Assert.DoesNotContain(Credential, host.Frames + host.Diagnostics, StringComparison.Ordinal);
        host.Console.Complete();
        await run;
    }

    [PosixFact]
    public async Task Routing_asks_each_profiles_description_and_warns_once_when_one_is_blank()
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
        File.WriteAllText(
            host.Temp.File("settings.json"),
            """{"schemaVersion": 2, "profiles": { "ops": { "provider": "openai", "model": "gpt-4.1" } }, "decisions": { "routingProfiles": ["ops"] }}"""
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.ChooseDetailedModeAsync();
        host.Console.SendEnter(); // the first preset row (openai)
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter(); // the "enter a key" row
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText(Credential);
        host.Console.SendEnter();
        await SkipConnectionTestAsync(host);
        await EnterManualModelAsync(host, "any-model");
        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // the default row skips the check
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-main");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip
        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEnter(); // skip

        await PickRowAsync(host, "Decisions settings", "routing (decisions.routing)");
        await host.WaitForFrameAsync("Routing (decisions.routing)");
        // The question's arm window drops keys until it expires; the virtual scheduler's clock
        // is what measures it, so advance past the window before the first answer key.
        host.Advance(500);
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> shadow");
        host.Advance(500);
        host.Console.SendEnter();

        // The plural answer: which candidates participate. Space toggles, Enter confirms and the
        // count is visible while toggling (spec: The walk is navigable).
        await host.WaitForFrameAsync("Routing profiles");
        host.Advance(500);
        host.Console.Send(new KeyEvent(KeyKind.Space, null, false, false, false));
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        host.Console.Send(new KeyEvent(KeyKind.Space, null, false, false, false));
        await host.WaitForFrameAsync("2 selected");
        host.Advance(500);
        host.Console.SendEnter();

        // The candidates are the walk's new chat profile plus the settings file's configured one;
        // each is asked what it is for, and the blank one is warned about exactly once. Each
        // prompt answers only after its 300 ms arm window has passed on the virtual scheduler.
        // Wait for each profile prompt by name. The old loop matched the generic prefix twice
        // when the first answer had not been consumed yet, then typed into the same prompt again;
        // that made the second Enter land on a stale frame and the walk never reached the summary.
        foreach (string profile in new[] { "chat-main", "ops" })
        {
            await host.WaitForFrameAsync($"What is profile '{profile}'");
            host.Advance(500);
            if (profile == "chat-main")
            {
                host.Console.SendText("for staged edit batches");
            }

            // Typing restarts the arm window too; advance past it before Enter.
            host.Advance(500);
            host.Console.SendEnter();
        }

        // The menu returned with the routing row showing the change; Up twice reaches Done.
        await host.WaitForFrameAsync("routing (decisions.routing) — shadow");
        host.Console.Send(new KeyEvent(KeyKind.Up, null, false, false, false));
        host.Console.Send(new KeyEvent(KeyKind.Up, null, false, false, false));
        await host.WaitForFrameAsync("> Done");
        host.Console.SendEnter();
        string summary = await host.WaitForScrollbackAsync("routing description");
        Assert.Contains(
            "routing description (chat-main): for staged edit batches",
            Flatten(summary),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "routing description (ops)",
            Flatten(summary),
            StringComparison.Ordinal
        );
        // The confirm picker's repaint carries the notice widget: wait for it (which advances
        // the virtual scheduler), then pin the warn-once text with the wrap flattened.
        await host.WaitForFrameAsync("Write this configuration?");
        await host.WaitForScrollbackAsync("routing description");
        string frames = Flatten(host.Frames);
        Assert.True(
            frames.Contains(
                "profile 'ops' has no description; routing warns once",
                StringComparison.Ordinal
            ),
            $"the blank routing description never warned:\n{frames}"
        );

        host.Console.SendEscape(); // the walk ends here, nothing written
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;

        // The seed file is the pre-walk file: an abort leaves it byte-identical.
        Assert.Equal(
            """{"schemaVersion": 2, "profiles": { "ops": { "provider": "openai", "model": "gpt-4.1" } }, "decisions": { "routingProfiles": ["ops"] }}""",
            File.ReadAllText(AuthStore.SettingsFilePath(host.Temp.File("auth.json"))),
            ignoreLineEndingDifferences: false
        );
    }

    /// <summary>Scrollback and frames wrap at the terminal width ""); flatten so a pinned
    /// phrase survives the wrap.</summary>
    private static string Flatten(string text) =>
        string.Join(
            " ",
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim())
        );

    /// <summary>Terminal writes with the ANSI control sequences removed and whitespace collapsed,
    /// so a notice wrapped across live-area lines reads as one phrase.</summary>
    private static string PlainFrames(string frames) =>
        Regex.Replace(
            Regex.Replace(frames, "\u001b\\[[0-9;?]*[ -/]*[@-~]", string.Empty),
            "\\s+",
            " "
        );

    private static HttpMessageHandler Recording(string body) =>
        new RecordingHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    }
                )
        );

    /// <summary>Moves the picker's selection to the named row and submits it, naming the step by
    /// its title prefix so a re-ordered walk fails the pin instead of silently skipping it.</summary>
    private static async Task PickRowAsync(InteractiveSessionHost host, string title, string row)
    {
        await host.WaitForFrameAsync(title);
        string previous = host.LastFrame;
        while (!host.LastFrame.Contains($"> {row}", StringComparison.Ordinal))
        {
            host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
            await host.WaitUntilAsync(() =>
            {
                // The picker's repaint is scheduler-scheduled: advance the session's virtual
                // clock the way WaitForFrameAsync does, or the new frame never paints.
                host.Advance(33);
                return host.LastFrame != previous;
            });
            previous = host.LastFrame;
        }

        host.Console.SendEnter();
    }

    /// <summary>The connection test's run/skip picker: skip is the second row.</summary>
    private static async Task SkipConnectionTestAsync(InteractiveSessionHost host)
    {
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> Skip the connection test");
        host.Console.SendEnter();
    }

    /// <summary>The model step with no listing: the manual entry row, then the id.</summary>
    private static async Task EnterManualModelAsync(InteractiveSessionHost host, string modelId)
    {
        await host.WaitForFrameAsync("Model (discovery listed nothing — enter an id manually)");
        host.Console.SendEnter(); // the "enter a model id manually" row
        await host.WaitForFrameAsync("Model id");
        host.Console.SendText(modelId);
        host.Console.SendEnter();
    }
}
