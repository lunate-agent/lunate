using System.Net;
using System.Text;
using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

[Collection("setup-wizard")]
public sealed class SetupWizardModelStepTests
{
    private const string Credential = "model-step-test-key";

    [PosixFact]
    public async Task The_model_picker_filters_discovery_results_and_shows_the_family()
    {
        using var host = new InteractiveSessionHost(
            freshInstall: true,
            interactiveConsole: true,
            discoveryTransport: Response(
                200,
                """{"data":[{"id":"gpt-3.5-turbo","api":{"chat-completions":{}}},{"id":"gpt-4o-mini","api":{"chat-completions":{}}}]}"""
            )
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await ReachModelPickerAsync(host);
        Assert.Contains("gpt-3.5-turbo (openai-chat)", host.LastFrame, StringComparison.Ordinal);
        host.Console.SendText("gpt-4o-mini");
        await host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return host.LastFrame.Contains("gpt-4o-mini (openai-chat)", StringComparison.Ordinal)
                && !host.LastFrame.Contains("gpt-3.5-turbo", StringComparison.Ordinal);
        });
        Assert.DoesNotContain("gpt-3.5-turbo", host.LastFrame, StringComparison.Ordinal);
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Model check: estimated cost");
        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;
    }

    [PosixFact]
    public async Task Skipping_a_failed_discovery_allows_a_manually_entered_model_id()
    {
        using var host = new InteractiveSessionHost(
            freshInstall: true,
            interactiveConsole: true,
            discoveryTransport: Response(500, "{}")
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText(Credential);
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("connection test failed");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        host.Console.SendEnter();

        await host.WaitForFrameAsync("discovery listed nothing — enter an id manually");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model id");
        host.Console.SendText("local-model-v1");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model check: estimated cost");
        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;
    }

    private static async Task ReachModelPickerAsync(InteractiveSessionHost host)
    {
        await host.WaitForFrameAsync("Select provider");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("API key for openai");
        host.Console.SendText(Credential);
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("Model (type to search");
    }

    private static HttpMessageHandler Response(int status, string body) =>
        new RecordingHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage((HttpStatusCode)status)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    }
                )
        );
}
