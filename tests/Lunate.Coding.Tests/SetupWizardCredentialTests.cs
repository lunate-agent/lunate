using System.Net;
using System.Text;
using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

[Trait("Category", "SetupWizardCredential")]
[Collection("setup-wizard")]
public sealed class SetupWizardCredentialTests
{
    [PosixTheory]
    [InlineData("env:SETUP_WIZARD_TEST_KEY")]
    [InlineData("auth:stored-key")]
    public async Task A_named_credential_reference_is_accepted(string reference)
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for openai");
        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> name an env: or auth: reference instead");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential reference");
        host.Console.SendText(reference);
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;

        Assert.False(File.Exists(AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"))));
        Assert.False(File.Exists(host.Temp.File("settings.json")));
        Assert.False(File.Exists(host.Temp.File("auth.json")));
    }

    [PosixFact]
    public async Task A_set_standard_environment_key_is_offered_first_and_stored_as_a_reference()
    {
        const string secret = "env-key-must-never-appear-in-output";
        var transport = new RecordingHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{"data":[{"id":"gpt-3.5-turbo"}]}""",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                )
        );
        Func<string, string?> environment = name =>
            name == "OPENAI_API_KEY" ? secret : PrintModeTestSupport.Environment()(name);
        using var host = new InteractiveSessionHost(
            freshInstall: true,
            environment: environment,
            interactiveConsole: true,
            discoveryTransport: transport
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Credential for openai");
        Assert.Contains(
            "use OPENAI_API_KEY from the environment",
            host.LastFrame,
            StringComparison.Ordinal
        );
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Connection test (one discovery request");
        host.Console.SendEnter(); // run the test
        await host.WaitForFrameAsync("Model (type to search");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Model check");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText("chat-main");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("fast usage");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Decision profile");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Decisions settings");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("model: gpt-3.5-turbo");
        host.Console.Complete();
        await run;

        string providers = File.ReadAllText(
            AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"))
        );
        string settings = File.ReadAllText(AuthStore.SettingsFilePath(host.Temp.File("auth.json")));
        Assert.Equal(secret, transport.Requests.Single().Headers.Authorization?.Parameter);
        Assert.Contains("env:OPENAI_API_KEY", providers, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, providers, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, settings, StringComparison.Ordinal);
        Assert.False(File.Exists(host.Temp.File("auth.json")));
        Assert.DoesNotContain(secret, host.Frames + host.Diagnostics, StringComparison.Ordinal);
    }
}
