using System.Net;
using System.Text;
using System.Text.Json;
using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The quick/detailed mode behavior through the real component stack (spec: Quick and detailed
/// modes): Quick reaches a working configuration and names every default it skipped; Detailed asks
/// the thinking level, fast usage, decision profile and decisions switches Quick leaves alone.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardModeSessionTests
{
    private const string Credential = "sk-mode-credential-never-echoed";

    private const string ReasoningBody =
        """{"data":[{"id":"pin-reasoner","capabilities":{"reasoning":{"supported":true,"effort":["low","medium","high"]}},"api":{"chat-completions":{}}}]}""";

    [PosixFact]
    public async Task Quick_reaches_a_working_configuration_and_names_its_defaults()
    {
        using var host = Host();
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        Assert.Contains("Setup mode: quick", host.LastFrame, StringComparison.Ordinal);
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
        host.Console.SendEnter(); // skip: the model stays unverified
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-main");
        host.Console.SendEnter();

        // Quick ends at the chat profile: no thinking question, fast/decision/decisions steps.
        await host.WaitForFrameAsync("Add another provider?");
        host.Console.SendEnter(); // Done: one provider, on to the summary
        string summary = await host.WaitForScrollbackAsync("Left at default:");
        Assert.DoesNotContain("Thinking level for", host.Frames, StringComparison.Ordinal);
        Assert.DoesNotContain("Also use profile", host.Frames, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Decision profile (this provider serves",
            host.Frames,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("Decisions settings", host.Frames, StringComparison.Ordinal);
        Assert.Contains("fast usage", summary, StringComparison.Ordinal);
        Assert.Contains("decision profile", summary, StringComparison.Ordinal);
        Assert.Contains("decisions.approvals", summary, StringComparison.Ordinal);

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("setup written");
        host.Console.Complete();
        await run;

        using JsonDocument settings = JsonDocument.Parse(
            File.ReadAllText(AuthStore.SettingsFilePath(host.Temp.File("auth.json")))
        );
        JsonElement root = settings.RootElement;
        Assert.Equal("chat-main", root.GetProperty("usages").GetProperty("chat").GetString());
        Assert.False(root.GetProperty("usages").TryGetProperty("fast", out _));
        Assert.False(root.TryGetProperty("decisions", out _));
        JsonElement profile = root.GetProperty("profiles").GetProperty("chat-main");
        Assert.Equal("pin-reasoner", profile.GetProperty("model").GetString());
        Assert.False(profile.TryGetProperty("reasoningLevel", out _));
        Assert.DoesNotContain(Credential, host.Frames + host.Diagnostics, StringComparison.Ordinal);
    }

    [PosixFact]
    public async Task Detailed_walks_every_setting_quick_hides()
    {
        using var host = Host();
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
        host.Console.SendEnter(); // skip
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-main");
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Thinking level for pin-reasoner");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> high");
        host.Console.SendEnter();

        // The fast step's flat list: the chat model's own row is the reuse answer. The list ends
        // with the inline row and Skip, so the model row is two rows up from the default.
        await host.WaitForFrameAsync("fast usage");
        host.Console.SendUp();
        host.Console.SendUp();
        await host.WaitForFrameAsync("> openai/pin-reasoner (openai-chat");
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Add another provider?");
        host.Console.SendEnter(); // Done: one provider, on to the decision step

        await host.WaitForFrameAsync(
            "Decision profile (this provider serves no native Jev profile"
        );
        host.Console.SendUp();
        host.Console.SendUp();
        await host.WaitForFrameAsync("> openai/pin-reasoner (adapter, openai-chat");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Decision check");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Decisions settings");
        Assert.Contains(
            "approval gate (decisions.approvals) — off (default)",
            host.LastFrame,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "routing (decisions.routing) — unset (default)",
            host.LastFrame,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "questions (decisions.questions) — off (default)",
            host.LastFrame,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "decide tool (decisions.tool) — off (default)",
            host.LastFrame,
            StringComparison.Ordinal
        );
        host.Console.SendEnter(); // accept the defaults

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        Assert.Contains("setup mode: detailed", summary, StringComparison.Ordinal);
        Assert.Contains(
            "chat profile: chat-main (thinking high)",
            summary,
            StringComparison.Ordinal
        );
        Assert.Contains("fast usage: chat-main", summary, StringComparison.Ordinal);
        Assert.Contains("decision profile: decisions (adapter)", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Left at default:", summary, StringComparison.Ordinal);

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("setup written");
        host.Console.Complete();
        await run;

        using JsonDocument settings = JsonDocument.Parse(
            File.ReadAllText(AuthStore.SettingsFilePath(host.Temp.File("auth.json")))
        );
        JsonElement root = settings.RootElement;
        Assert.Equal("chat-main", root.GetProperty("usages").GetProperty("chat").GetString());
        Assert.Equal("chat-main", root.GetProperty("usages").GetProperty("fast").GetString());
        Assert.Equal("decisions", root.GetProperty("decisions").GetProperty("profile").GetString());
        JsonElement profile = root.GetProperty("profiles").GetProperty("chat-main");
        Assert.Equal("high", profile.GetProperty("reasoningLevel").GetString());
    }

    private static InteractiveSessionHost Host() =>
        new(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler(
                (_, _) =>
                    Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                ReasoningBody,
                                Encoding.UTF8,
                                "application/json"
                            ),
                        }
                    )
            )
        );
}
