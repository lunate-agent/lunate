using System.Net;
using System.Text.Json;
using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

/// <summary>
/// One walk collects more than one provider (spec: Several providers in one run): the provider
/// list returns after each run, **Add another provider…** starts the preset list again, a repeated
/// name asks for a rename, and the confirmed commit writes every provider and credential.
/// </summary>
[Collection("setup-wizard")]
public sealed class SetupWizardMultipleProvidersTests
{
    private const string DiscoveryBody =
        """{"data":[{"id":"gpt-3.5-turbo","limit":{"context":32000},"capabilities":{"reasoning":{"supported":false}}}]}""";

    private static HttpResponseMessage Response(int status, string body) =>
        new((HttpStatusCode)status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

    private static string SettingsPath(InteractiveSessionHost host) =>
        AuthStore.SettingsFilePath(host.Temp.File("auth.json"));

    private static string ProviderPath(InteractiveSessionHost host) =>
        AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"));

    [PosixFact]
    public async Task Two_providers_are_collected_in_one_walk_and_written_together()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            freshInstall: true,
            discoveryTransport: new RecordingHandler(
                (_, _) => Task.FromResult(Response(200, DiscoveryBody))
            )
        );
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await ConfigureProviderAsync(host, "openai", "chat-main", "env:OPENAI_API_KEY");

        await host.WaitForFrameAsync("Add another provider?");
        host.Console.Send(new KeyEvent(KeyKind.Up, null, false, false, false));
        await host.WaitForFrameAsync("> Add another provider…");
        host.Console.SendEnter();

        await ConfigureProviderAsync(host, "anthropic", "chat-alt", "env:OPENAI_API_KEY");

        await host.WaitForFrameAsync("Add another provider?");
        Assert.Contains("openai — configured", host.LastFrame, StringComparison.Ordinal);
        Assert.Contains("anthropic — configured", host.LastFrame, StringComparison.Ordinal);
        host.Console.SendEnter(); // Done is the default

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        Assert.Contains("provider openai", summary, StringComparison.Ordinal);
        Assert.Contains("provider anthropic", summary, StringComparison.Ordinal);
        Assert.Contains("providers.json", summary, StringComparison.Ordinal);
        Assert.Equal(2, Count(summary, "credential reference", StringComparison.Ordinal));
        Assert.Equal(2, Count(summary, "env:OPENAI_API_KEY", StringComparison.Ordinal));

        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("setup written");
        host.Console.Complete();
        await run;

        Assert.False(
            File.Exists(host.Temp.File("auth.json")),
            "a reused env reference stores no key"
        );
        using JsonDocument providers = JsonDocument.Parse(File.ReadAllText(ProviderPath(host)));
        JsonElement[] entries =
        [
            .. providers.RootElement.GetProperty("providers").EnumerateArray(),
        ];
        Assert.Equal(
            ["anthropic", "openai"],
            entries.Select(entry => entry.GetProperty("name").GetString()!)
        );
        Assert.All(
            entries,
            entry =>
                Assert.Equal(
                    "env:OPENAI_API_KEY",
                    entry.GetProperty("credentialReference").GetString()
                )
        );

        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(SettingsPath(host)));
        JsonElement profiles = settings.RootElement.GetProperty("profiles");
        Assert.Equal(
            "openai",
            profiles.GetProperty("chat-main").GetProperty("provider").GetString()
        );
        Assert.Equal(
            "anthropic",
            profiles.GetProperty("chat-alt").GetProperty("provider").GetString()
        );
        Assert.Equal(
            "chat-alt",
            settings.RootElement.GetProperty("usages").GetProperty("chat").GetString()
        );
    }

    [PosixFact]
    public async Task A_duplicate_provider_name_asks_for_a_rename_and_keeps_both_entries()
    {
        using var host = ConfiguredHost();
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        host.Console.SendText("/setup");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("Setup — what do you want to configure?");
        host.Console.SendEnter(); // Add a provider is the default row

        await AddSkippedProviderAsync(host, "deepseek");

        await host.WaitForFrameAsync("Add another provider?");
        host.Console.Send(new KeyEvent(KeyKind.Up, null, false, false, false));
        await host.WaitForFrameAsync("> Add another provider…");
        host.Console.SendEnter();

        await host.WaitForFrameAsync("Select provider");
        await PickRowAsync(host, "Select provider", "deepseek");
        await host.WaitForFrameAsync("already configured — name this entry instead");
        host.Console.SendText("deepseek-work");
        host.Console.SendEnter();
        await PickRowAsync(host, "Credential for deepseek-work", "Skip credential setup");

        await host.WaitForFrameAsync("Add another provider?");
        host.Console.SendEnter(); // Done

        string summary = await host.WaitForScrollbackAsync("confirm these values");
        Assert.Contains("provider deepseek-work", summary, StringComparison.Ordinal);
        await host.WaitForFrameAsync("Write this configuration?");
        host.Console.SendEnter();
        await host.WaitForFrameAsync("setup written");
        host.Console.Complete();
        await run;

        using JsonDocument providers = JsonDocument.Parse(File.ReadAllText(ProviderPath(host)));
        JsonElement[] entries =
        [
            .. providers.RootElement.GetProperty("providers").EnumerateArray(),
        ];
        Assert.Equal(
            ["deepseek", "deepseek-work", "openai"],
            entries.Select(entry => entry.GetProperty("name").GetString()!)
        );
        JsonElement renamed = entries.Single(entry =>
            entry.GetProperty("name").GetString() == "deepseek-work"
        );
        // The renamed entry keeps the preset's kind, so its families and base URL still resolve.
        Assert.Equal("deepseek", renamed.GetProperty("kind").GetString());
    }

    private static async Task AddSkippedProviderAsync(
        InteractiveSessionHost host,
        string providerId
    )
    {
        await PickRowAsync(host, "Select provider", providerId);
        // A skipped credential also skips the connection test, so the provider list returns next.
        await PickRowAsync(host, $"Credential for {providerId}", "Skip credential setup");
    }

    private static async Task ConfigureProviderAsync(
        InteractiveSessionHost host,
        string providerId,
        string profileName,
        string credentialReference
    )
    {
        await PickRowAsync(host, "Select provider", providerId);
        await PickRowAsync(
            host,
            $"Credential for {providerId}",
            "name an env: or auth: reference instead"
        );
        await host.WaitForFrameAsync("Credential reference");
        host.Console.SendText(credentialReference);
        host.Console.SendEnter();

        await PickRowAsync(host, "Connection test", "Run the connection test");
        await host.WaitForFrameAsync("Model (type to search");
        host.Console.SendEnter(); // the listed model
        await PickRowAsync(host, "Model check", "Skip the model check");
        await host.WaitForFrameAsync("Profile name");
        host.Console.SendText(profileName);
        host.Console.SendEnter();
        // Quick's full walk ends at the chat profile; the provider list returns next.
    }

    private static InteractiveSessionHost ConfiguredHost()
    {
        var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
        bool valid = ProviderEntry.TryCreate(
            "openai",
            "openai",
            new Uri("https://provider.example.test/v1"),
            "auth:openai",
            headers: null,
            region: null,
            out ProviderEntry? entry,
            out string? error
        );
        Assert.True(valid, error);
        ProviderStore.Save(ProviderPath(host), [entry!]);
        File.WriteAllText(
            SettingsPath(host),
            """
            {
              "schemaVersion": 2,
              "model": "gpt-4.1",
              "profiles": { "chat-main": { "provider": "openai", "model": "gpt-4.1" } },
              "usages": { "chat": "chat-main" }
            }
            """
        );
        return host;
    }

    private static async Task PickRowAsync(InteractiveSessionHost host, string title, string row)
    {
        await host.WaitForFrameAsync(title);
        string previous = host.LastFrame;
        for (var step = 0; step < 24; step++)
        {
            if (host.LastFrame.Contains($"> {row}", StringComparison.Ordinal))
            {
                break;
            }

            host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
            await host.WaitUntilAsync(() =>
            {
                host.Advance(33);
                return host.LastFrame != previous;
            });
            previous = host.LastFrame;
        }

        Assert.Contains($"> {row}", host.LastFrame, StringComparison.Ordinal);
        host.Console.SendEnter();
    }

    private static int Count(string text, string value, StringComparison comparison)
    {
        var count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, comparison)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
