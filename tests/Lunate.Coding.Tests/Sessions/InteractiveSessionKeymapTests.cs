using Lunate.Tui;

namespace Lunate.Coding.Tests;

[CollectionDefinition("interactive-session-keymap", DisableParallelization = true)]
public sealed class InteractiveSessionKeymapCollection
{
    public const string Name = "interactive-session-keymap";
}

/// <summary>
/// The user keymap file in a running session: the file under the session's home
/// (<c>.lunate/keymap.json</c>), overridden by <c>LUNATE_KEYMAP</c> and overridden again by
/// <c>--keymap</c>, is applied once at startup as the catalogue's overlay — and the rebound key
/// drives its action through the real key pump afterwards. A file that cannot be applied fails
/// the load: the shipped keys stand and the session says why. The catalogue is static, so every
/// test restores the shipped rows around itself.
/// </summary>
[Collection(InteractiveSessionKeymapCollection.Name)]
public sealed class InteractiveSessionKeymapTests
{
    [Fact]
    public async Task A_keymap_file_rebinds_a_key_which_drives_its_action()
    {
        KeyBinding[] before = Pristine();
        try
        {
            using var host = new InteractiveSessionHost();
            File.WriteAllText(
                HostKeymap(host),
                """{"version":1,"bindings":[{"id":"ctrl-o.transcript","keys":[{"kind":"Character","ctrl":true,"text":"t"}]}]}"""
            );
            Task run = host.RunAsync();
            await host.WaitUntilAsync(() => host.Session.IsComposed);

            // The overlay is in the catalogue...
            string shippedText = Assert
                .Single(before, row => row.Id == "ctrl-o.transcript")
                .Key.Text!;
            KeyBinding binding = Assert.Single(KeyBindingCatalog.BindingsFor("ctrl-o.transcript"));
            Assert.Equal("t", binding.Key.Text);
            Assert.Equal("o", shippedText);

            // ...and the rebound key drives the binding through the real key pump.
            host.Console.SendCtrl("t");
            await host.WaitUntilAsync(() => host.Console.InAlternateScreen);
            host.Console.SendText("q");
            await host.WaitUntilAsync(
                () => !host.Console.InAlternateScreen,
                InteractiveSessionHost.DefaultWait
            );

            host.Console.SendText("/quit");
            host.Console.SendEnter();
            await run;
        }
        finally
        {
            KeyBindingCatalog.Replace(before);
        }
    }

    [Fact]
    public async Task The_file_beats_the_catalogue_with_one_startup_line()
    {
        KeyBinding[] before = Pristine();
        try
        {
            using var host = new InteractiveSessionHost();
            File.WriteAllText(
                HostKeymap(host),
                """{"version":1,"bindings":[{"id":"ctrl-o.transcript","keys":[{"kind":"Escape"}]}]}"""
            );
            Task run = host.RunAsync();
            await host.WaitUntilAsync(() => host.Session.IsComposed);

            await WaitForNoticeAsync(
                host,
                "keymap: ctrl-o.transcript and escape.interrupt both answer Esc"
            );

            host.Console.SendEscape();
            await host.WaitUntilAsync(() => host.Console.InAlternateScreen);
            host.Console.SendText("q");
            await host.WaitUntilAsync(
                () => !host.Console.InAlternateScreen,
                InteractiveSessionHost.DefaultWait
            );

            host.Console.SendText("/quit");
            host.Console.SendEnter();
            await run;
        }
        finally
        {
            KeyBindingCatalog.Replace(before);
        }
    }

    [Fact]
    public async Task An_unknown_id_fails_the_load_and_the_shipped_keys_stand()
    {
        KeyBinding[] before = Pristine();
        try
        {
            using var host = new InteractiveSessionHost();
            File.WriteAllText(
                HostKeymap(host),
                """{"version":1,"bindings":[{"id":"ctrl-o.transcriptt","keys":[{"kind":"Character","ctrl":true,"text":"t"}]}]}"""
            );
            Task run = host.RunAsync();
            await host.WaitUntilAsync(() => host.Session.IsComposed);

            string notice = await WaitForNoticeAsync(host, "is not a binding id");
            Assert.Contains("'ctrl-o.transcript'", notice, StringComparison.Ordinal);

            // Nothing of the file was applied: the shipped key still stands.
            KeyBinding binding = Assert.Single(KeyBindingCatalog.BindingsFor("ctrl-o.transcript"));
            Assert.Equal("o", binding.Key.Text);

            host.Console.SendText("/quit");
            host.Console.SendEnter();
            await run;
        }
        finally
        {
            KeyBindingCatalog.Replace(before);
        }
    }

    [Fact]
    public async Task Two_of_the_files_own_bindings_in_conflict_fail_the_load_wholesale()
    {
        KeyBinding[] before = Pristine();
        try
        {
            using var host = new InteractiveSessionHost();
            File.WriteAllText(
                HostKeymap(host),
                """{"version":1,"bindings":[{"id":"ctrl-o.transcript","keys":[{"kind":"Escape"}]},{"id":"ctrl-l.redraw","keys":[{"kind":"Escape"}]}]}"""
            );
            Task run = host.RunAsync();
            await host.WaitUntilAsync(() => host.Session.IsComposed);

            string notice = await WaitForNoticeAsync(host, "that is a typo, not a preference");
            Assert.Contains("'ctrl-o.transcript'", notice, StringComparison.Ordinal);
            Assert.Contains("'ctrl-l.redraw'", notice, StringComparison.Ordinal);

            // The load failed wholesale: the shipped keys still stand.
            Assert.Equal(before, KeyBindingCatalog.All);

            host.Console.SendText("/quit");
            host.Console.SendEnter();
            await run;
        }
        finally
        {
            KeyBindingCatalog.Replace(before);
        }
    }

    [Fact]
    public async Task LUNATE_KEYMAP_selects_the_file()
    {
        KeyBinding[] before = Pristine();
        string envFile = ScratchFile("env");
        try
        {
            // The selected file rebinds the transcript key to Ctrl+T; the home carries no
            // keymap.json at all, so the only way Ctrl+T can appear is the named file.
            File.WriteAllText(
                envFile,
                """{"version":1,"bindings":[{"id":"ctrl-o.transcript","keys":[{"kind":"Character","ctrl":true,"text":"t"}]}]}"""
            );
            using var host = new InteractiveSessionHost(environment: name =>
                name == "LUNATE_KEYMAP" ? envFile : PrintModeTestSupport.Environment()(name)
            );
            Task run = host.RunAsync();
            await host.WaitUntilAsync(() => host.Session.IsComposed);

            KeyBinding binding = Assert.Single(KeyBindingCatalog.BindingsFor("ctrl-o.transcript"));
            Assert.Equal("t", binding.Key.Text);
            host.Console.SendCtrl("t");
            await host.WaitUntilAsync(() => host.Console.InAlternateScreen);
            host.Console.SendText("q");
            await host.WaitUntilAsync(
                () => !host.Console.InAlternateScreen,
                InteractiveSessionHost.DefaultWait
            );

            host.Console.SendText("/quit");
            host.Console.SendEnter();
            await run;
        }
        finally
        {
            File.Delete(envFile);
            KeyBindingCatalog.Replace(before);
        }
    }

    [Fact]
    public async Task The_keymap_flag_beats_the_env_file()
    {
        KeyBinding[] before = Pristine();
        string flagPath = ScratchFile("flag");
        string envPath = ScratchFile("env");
        try
        {
            File.WriteAllText(
                flagPath,
                """{"version":1,"bindings":[{"id":"ctrl-o.transcript","keys":[{"kind":"Character","ctrl":true,"text":"t"}]}]}"""
            );
            File.WriteAllText(
                envPath,
                """{"version":1,"bindings":[{"id":"ctrl-o.transcript","keys":[{"kind":"Character","ctrl":true,"text":"b"}]}]}"""
            );

            using var host = new InteractiveSessionHost(
                launch: new LaunchOptions { Keymap = flagPath },
                environment: name =>
                    name == "LUNATE_KEYMAP" ? envPath : PrintModeTestSupport.Environment()(name)
            );
            Task run = host.RunAsync();
            await host.WaitUntilAsync(() => host.Session.IsComposed);

            // The flag's file won: Ctrl+T is the transcript key, not the env file's Ctrl+B.
            KeyBinding binding = Assert.Single(KeyBindingCatalog.BindingsFor("ctrl-o.transcript"));
            Assert.Equal("t", binding.Key.Text);
            host.Console.SendCtrl("t");
            await host.WaitUntilAsync(() => host.Console.InAlternateScreen);
            host.Console.SendText("q");
            await host.WaitUntilAsync(
                () => !host.Console.InAlternateScreen,
                InteractiveSessionHost.DefaultWait
            );

            host.Console.SendText("/quit");
            host.Console.SendEnter();
            await run;
        }
        finally
        {
            File.Delete(flagPath);
            File.Delete(envPath);
            KeyBindingCatalog.Replace(before);
        }
    }

    /// <summary>
    /// The shipped catalogue, reloaded from the embedded file: a test starts from what ships, never
    /// from an overlay an earlier test's restore left in the static catalogue.
    /// </summary>
    private static KeyBinding[] Pristine()
    {
        KeyBindingCatalog.Reload();
        return [.. KeyBindingCatalog.All];
    }

    /// <summary>The session home's own keymap file: <c>&lt;home&gt;/.lunate/keymap.json</c>.</summary>
    private static string HostKeymap(InteractiveSessionHost host)
    {
        string store = System.IO.Path.Combine(host.Temp.File("home"), ".lunate");
        Directory.CreateDirectory(store);
        return System.IO.Path.Combine(store, "keymap.json");
    }

    /// <summary>A scratch file under the host's temp directory (<c>$TMPDIR</c> on POSIX).</summary>
    private static string ScratchFile(string kind) =>
        System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"lunate-keymap-{kind}-{Guid.NewGuid():N}.json"
            )
        );

    private static async Task<string> WaitForNoticeAsync(InteractiveSessionHost host, string text)
    {
        await host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return host.Frames.Contains(text, StringComparison.Ordinal);
        });
        return host.Frames;
    }
}
