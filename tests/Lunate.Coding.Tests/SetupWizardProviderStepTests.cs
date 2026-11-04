using Lunate.Ai;
using Lunate.Tui;

namespace Lunate.Coding.Tests;

[Trait("Category", "SetupWizardProviderStep")]
[Collection("setup-wizard")]
public sealed class SetupWizardProviderStepTests
{
    [PosixFact]
    public async Task The_picker_keeps_preset_order_and_places_custom_last_at_eighty_columns()
    {
        using var host = new InteractiveSessionHost(interactiveConsole: true, freshInstall: true);
        host.Console.Size = new ConsoleSize(80, 24);
        host.Scrollback.Profile.Width = 80;
        Task run = host.RunAsync();
        await host.WaitUntilAsync(() => host.Session.IsComposed);

        await host.WaitForFrameAsync("Select provider");
        for (var index = 0; index < ProviderPresets.All.Count; index++)
        {
            ProviderPreset preset = ProviderPresets.All[index];
            await host.WaitForFrameAsync($"> {preset.Id} — {preset.Name} (region not specified)");
            if (index + 1 < ProviderPresets.All.Count)
            {
                host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
            }
        }

        host.Console.Send(new KeyEvent(KeyKind.Down, null, false, false, false));
        await host.WaitForFrameAsync("> Custom OpenAI-compatible endpoint…");
        host.Console.SendEscape();
        await host.WaitUntilAsync(() => !host.Session.IsRunning);
        host.Console.Complete();
        await run;

        Assert.False(File.Exists(AuthStore.ProviderStoreFilePath(host.Temp.File("auth.json"))));
    }
}
