using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// <c>lunate setup</c> on a terminal (spec: Setup command line — "`lunate setup` SHALL run the setup
/// wizard … `lunate setup` on a terminal with no flags and existing, resolvable configuration SHALL
/// still open the wizard; when no configuration exists, bare `lunate` and `lunate setup` behave
/// identically"): a flagless run opens the same wizard the interactive start does — the full walk
/// on a first run, the entry menu on a configured install — instead of walking the flag path with
/// every step skipped. Both cases run the production <c>Cli.Run</c> dispatch over the host's
/// scripted terminal. The flagless non-terminal refusal is pinned in
/// <see cref="SetupCommandTests"/>.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupCommandWizardTests
{
    private const string ConfiguredSettings = """
        {
          "schemaVersion": 2,
          "model": "gpt-4.1",
          "profiles": { "chat-main": { "provider": "openai", "model": "gpt-4.1" } },
          "usages": { "chat": "chat-main" }
        }
        """;

    [PosixFact]
    public async Task Lunate_setup_on_a_terminal_opens_the_full_walk_on_a_first_run()
    {
        // No environment at all: the truly empty machine, where the session composes on the
        // wizard's placeholder model and no catalog entry stands behind it.
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            environment: _ => null
        );
        Task<int> run = StartSetup(host);

        await host.WaitForFrameAsync("Select provider");
        Assert.DoesNotContain(
            "Setup — what do you want to configure?",
            host.Frames,
            StringComparison.Ordinal
        );

        host.Console.SendEscape();
        await host.WaitForFrameAsync("setup cancelled — nothing was changed");
        host.Console.Complete();

        Assert.Equal(0, await run);
        Assert.False(File.Exists(SettingsPath(host)), "an aborted wizard writes nothing");
        Assert.False(File.Exists(ProviderPath(host)), "an aborted wizard writes nothing");
    }

    [PosixFact]
    public async Task Lunate_setup_on_a_terminal_opens_the_entry_menu_on_a_configured_install()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler(
                (_, _) =>
                    Task.FromResult(
                        new HttpResponseMessage((System.Net.HttpStatusCode)401)
                        {
                            Content = new StringContent(
                                "{}",
                                System.Text.Encoding.UTF8,
                                "application/json"
                            ),
                        }
                    )
            )
        );
        Assert.True(
            ProviderEntry.TryCreate(
                "openai",
                "openai",
                new Uri("https://provider.example.test/v1"),
                "auth:openai",
                headers: null,
                region: null,
                out ProviderEntry? entry,
                out string? error
            ),
            error
        );
        ProviderStore.Save(ProviderPath(host), [entry!]);
        File.WriteAllText(SettingsPath(host), ConfiguredSettings);
        byte[] settings = File.ReadAllBytes(SettingsPath(host));
        byte[] providers = File.ReadAllBytes(ProviderPath(host));

        Task<int> run = StartSetup(host);

        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        await host.WaitForFrameAsync("Walk everything");
        Assert.DoesNotContain("Select provider", host.Frames, StringComparison.Ordinal);

        host.Console.SendEscape();
        await host.WaitForFrameAsync("setup closed — nothing was changed");
        host.Console.Complete();

        Assert.Equal(0, await run);
        Assert.Equal(settings, File.ReadAllBytes(SettingsPath(host)));
        Assert.Equal(providers, File.ReadAllBytes(ProviderPath(host)));
        Assert.Empty(host.Client.Requests);
    }

    /// <summary>The production dispatch: <c>lunate setup</c> with no flags on a terminal.</summary>
    private static Task<int> StartSetup(InteractiveSessionHost host)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        return Task.Run(() =>
            Cli.Run(
                ["setup"],
                TextWriter.Null,
                TextWriter.Null,
                ct,
                stdinIsTerminal: true,
                interactiveOptions: host.Options
            )
        );
    }

    private static string SettingsPath(InteractiveSessionHost host) =>
        AuthStore.SettingsFilePath(host.Temp.File("auth.json"));

    private static string ProviderPath(InteractiveSessionHost host) =>
        AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"));
}
