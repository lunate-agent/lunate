using System.Net;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The 0.3.0 live-area wrap acceptance (spec: The full walk; the terminal contract): a wrapped
/// line is one logical line but several physical terminal rows, so the live area must clear exactly
/// its physical height. These tests drive the real host/console through a terminal-screen model —
/// not raw frame text — so a wrapped line that is not cleared leaves a duplicate row the assertion
/// can see.
/// </summary>
[Collection("setup-wizard")]
public sealed class WrappedLiveAreaTests
{
    private const string Credential = "wrap-test-key-never-echoed";

    private const string DiscoveryBody =
        """{"data":[{"id":"gpt-3.5-turbo","limit":{"context":32000},"capabilities":{"reasoning":{"supported":false}}}]}""";

    private static HttpResponseMessage Response() =>
        new((HttpStatusCode)200)
        {
            Content = new StringContent(
                DiscoveryBody,
                System.Text.Encoding.UTF8,
                "application/json"
            ),
        };

    [Theory]
    [InlineData(40)]
    [InlineData(45)]
    public async Task The_full_walk_paints_each_committed_line_exactly_once(int width)
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler((_, _) => Task.FromResult(Response())),
            unifiedScrollback: true
        );
        host.Console.Size = new ConsoleSize(width, 24);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await WalkToSummary(host);

        // The summary is committed before the confirm picker opens, so the whole stream — the
        // header, the live frames and the summary — is now in the scripted console's writes.
        var screen = new TerminalScreen(width);
        foreach (string write in host.Console.Writes)
        {
            screen.Feed(write);
        }

        IReadOnlyList<string> lines = screen.Lines;
        string content = string.Join("\n", lines);

        // Every committed line survives once: a wrapped line that the live area failed to clear
        // would leave a second copy of its text in the scrollback.
        foreach (
            string marker in new[]
            {
                "provider: openai",
                "connection test: passed",
                "model: gpt-3.5-turbo",
                "chat profile: chat-main",
            }
        )
        {
            Assert.Equal(1, Occurrences(content, marker));
        }

        // No physical row is wider than the terminal: the renderer clips, and the terminal model
        // counts cells the way CellText does.
        Assert.All(
            lines,
            line => Assert.True(CellText.Width(line) <= width, $"'{line}' overflows {width}")
        );

        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;
    }

    /// <summary>
    /// The reported symptom (#679): a status line repeated down the scrollback during the walk.
    /// The model-check notice is the same one-slot text the decision check posts, and the walk
    /// repaints it on every profile-name key. At a fixed 40 columns — the width the renderer and
    /// the terminal both use — the line is painted once, never once per repaint.
    /// </summary>
    [Fact]
    public async Task The_model_check_status_line_paints_once_while_the_walk_repaints_at_40_columns()
    {
        const int width = 40;
        var factory = new FakeChatClientFactory(
            new ScriptedChatClient().Enqueue(Scripts.Text("OK"))
        );
        using var host = new InteractiveSessionHost(
            factory: factory,
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler((_, _) => Task.FromResult(Response())),
            unifiedScrollback: true
        );
        host.Console.Size = new ConsoleSize(width, 24);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
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
        host.Console.SendUp(); // the send row, not the default skip
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Profile name");

        // Every typed key is a repaint with the notice still up: the redraw loop the report saw.
        foreach (char key in "chat-main")
        {
            host.Console.SendText(key.ToString());
            host.Advance(33);
        }

        // The wizard reads the name through its own prompt, and the prompt label and the draft
        // share one clipped line at 40 columns; submitting and waiting for the next step pins
        // that every repaint happened before the frame is read.
        host.Console.SendEnter();
        await host.WaitForFrameAsync("as the fast");
        var screen = new TerminalScreen(width);
        foreach (string write in host.Console.Writes)
        {
            screen.Feed(write);
        }

        IReadOnlyList<string> lines = screen.Lines;
        Assert.Equal(1, Occurrences(string.Join("\n", lines), "model check passed"));
        Assert.All(
            lines,
            line => Assert.True(CellText.Width(line) <= width, $"'{line}' overflows {width}")
        );

        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;
    }

    /// <summary>Drives every wizard step through the real console; the wait strings are the clipped
    /// forms of the step titles at 40/45 columns.</summary>
    private static async Task WalkToSummary(InteractiveSessionHost host)
    {
        await host.WaitForFrameAsync("Select provider");
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

        await host.WaitForFrameAsync("as the fast");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEnter(); // skip

        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter(); // defaults

        await host.WaitForFrameAsync("Write this configuration?");
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        for (
            var index = haystack.IndexOf(needle, StringComparison.Ordinal);
            index >= 0;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal)
        )
        {
            count++;
        }

        return count;
    }
}
