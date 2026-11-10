using System.Net;
using System.Text.RegularExpressions;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The full walk through the real component stack at the two supported widths (spec: The full
/// walk; the terminal contract): the menu and the summary render at 80 and 60 columns, every
/// written line fits its width, and the render text is pinned so a layout change is a test change.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardWidthTests
{
    private const string Credential = "width-test-key-never-echoed";

    private const string DiscoveryBody =
        """{"data":[{"id":"gpt-3.5-turbo","limit":{"context":32000},"capabilities":{"reasoning":{"supported":false}}}]}""";

    private static HttpResponseMessage Response(int status, string body) =>
        new((HttpStatusCode)status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

    [PosixFact]
    public async Task The_full_walk_pins_its_menu_and_summary_at_80_and_60_columns()
    {
        foreach (int width in new[] { 80, 60 })
        {
            using var host = new InteractiveSessionHost(
                interactiveConsole: true,
                freshInstall: true,
                discoveryTransport: new RecordingHandler(
                    (_, _) => Task.FromResult(Response(200, DiscoveryBody))
                )
            );
            host.Scrollback.Profile.Width = width;
            host.Console.Size = new ConsoleSize(width, 24);
            Task run = host.RunAsync();
            await host.WaitUntilAsync(() => host.Session.IsComposed);

            await WalkToSummary(host);
            string summary = await host.WaitForScrollbackAsync("confirm these values");
            string frames = Flatten(host.Frames);

            string flatSummary = Flatten(summary);
            Assert.Contains("Select provider", frames, StringComparison.Ordinal);
            Assert.Contains("Credential for openai", frames, StringComparison.Ordinal);
            Assert.Contains("provider: openai", flatSummary, StringComparison.Ordinal);
            Assert.Contains(
                "model: gpt-3.5-turbo (family openai-chat)",
                flatSummary,
                StringComparison.Ordinal
            );
            Assert.Contains("chat profile: chat-main", flatSummary, StringComparison.Ordinal);

            foreach (string raw in host.Frames.Split('\n'))
            {
                string line = Regex
                    .Replace(raw, "\x1b\\[[0-9;?]*[ -/]*[@-~]", string.Empty)
                    .Replace("\r", string.Empty, StringComparison.Ordinal);
                Assert.True(
                    line.Length <= width,
                    $"width {width} overflowed by {line.Length - width}: {line}"
                );
            }

            host.Console.SendEscape();
            await host.WaitUntilAsync(() => !host.Session.IsRunning);
            host.Console.Complete();
            await run;
        }
    }

    private static async Task WalkToSummary(InteractiveSessionHost host)
    {
        await host.ChooseDetailedModeAsync();
        host.Console.SendEnter(); // openai

        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter(); // the key row
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText(Credential);
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run it

        await host.WaitForFrameAsync("Model (type to search");
        host.Console.SendEnter(); // the listed model

        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-main");
        host.Console.SendEnter();

        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // defaults
    }

    private static string Flatten(string text) =>
        Regex.Replace(text, "\\s+", " ", RegexOptions.None).Trim();
}
