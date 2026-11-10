using System.Net;
using System.Text;
using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// The picker's open and dismiss transitions cross threads: the key pump dismisses the picker it
/// confirmed while the wizard walk's continuation opens the next step's picker. The dismissal may
/// only clear the picker it confirmed; the newer one is the walk's (#730).
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardPickerTransitionTests
{
    private const string Credential = "picker-transition-test-key-please-never-echo";

    /// <summary>
    /// The walk opens the next step's picker while the pump dismisses the previous one. Before the
    /// fix the dismissal cleared the new picker's state mid-open (or nulled the picker after it was
    /// installed and posted no picker last): the next frame never appeared and the walk awaited a
    /// pick forever, which is issue #730. Open and dismiss are now ordered messages to one owner,
    /// so the next picker can be installed only after the previous dismissal was applied — the
    /// flow needs no held thread or sleep to be correct.
    /// </summary>
    [PosixFact]
    public async Task A_dismissal_in_flight_does_not_clear_the_picker_the_walk_is_opening()
    {
        using var host = new InteractiveSessionHost(
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
            discoveryTransport: Response("""{"data":[{"id":"local-model-v1"}]}""")
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        for (var row = 0; row < ProviderPresets.All.Count; row++)
        {
            host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        }

        await host.WaitForFrameAsync("> Custom OpenAI-compatible endpoint…");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Custom API family");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential placement");

        host.Console.SendEnter(); // confirm the placement pick; the walk now opens "Credential for local"
        await host.WaitForFrameAsync("Credential for local");
        Assert.Contains("Credential for local", host.LastFrame, StringComparison.Ordinal);

        host.Console.SendEnter();
        // The scripted prompt answers the masked key read without painting a frame, so the walk's
        // next painted step is the connection test picker.
        await host.WaitForFrameAsync("Connection test (one discovery request");

        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;
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
