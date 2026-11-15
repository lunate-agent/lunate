using System.Text.RegularExpressions;
using Lunate.Extensibility;
using Lunate.Extensibility.Abstractions;

namespace Lunate.Coding.Tests;

/// <summary>
/// The user theme file in a running session: <c>theme.json</c> in the session's home (the store
/// <c>$LUNATE_HOME</c> moves) overlays the selected built-in, <c>--theme &lt;path&gt;</c> reads a
/// file of its own and the flag wins, and a file that cannot be applied is discarded whole with one
/// warning line while the built-in theme stands.
/// </summary>
[Collection(InteractiveSessionTuiSettingsCollection.Name)]
public sealed class InteractiveSessionThemeFileTests
{
    [Fact]
    public async Task A_theme_file_under_the_home_recolours_the_live_frame()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            environment: TrueColorEnvironment
        );
        WriteHomeTheme(host, """{"prompt":"#ff0000"}""");

        Task run = host.RunAsync();
        await host.WaitUntilAsync(Painted(host, "\u001b[38;2;255;0;0m"));

        await QuitAsync(host, run);
    }

    [Fact]
    public async Task The_theme_flag_path_wins_over_the_home_file()
    {
        string flagPath = ScratchTheme("""{"prompt":"#00ff00"}""");
        try
        {
            using var host = new InteractiveSessionHost(
                interactiveConsole: true,
                environment: TrueColorEnvironment,
                launch: new LaunchOptions { Theme = flagPath }
            );
            WriteHomeTheme(host, """{"prompt":"#ff0000"}""");

            Task run = host.RunAsync();
            await host.WaitUntilAsync(Painted(host, "\u001b[38;2;0;255;0m"));

            Assert.DoesNotContain("\u001b[38;2;255;0;0m", host.Frames, StringComparison.Ordinal);
            await QuitAsync(host, run);
        }
        finally
        {
            File.Delete(flagPath);
        }
    }

    [Fact]
    public async Task The_tui_theme_setting_paints_at_startup()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            environment: TrueColorEnvironment
        );
        WriteSettings(host, """{"schemaVersion": 2, "tui": {"theme": "mono"}}""");

        Task run = host.RunAsync();

        // The setting is the selection when no flag names one, so the repaint after the theme is
        // resolved carries no colour attribute: the default theme's prompt tone is gone.
        await host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return host.LastFrame.Contains("> Ask anything", StringComparison.Ordinal)
                && !Regex.IsMatch(host.LastFrame, @"\x1b\[[0-9;]*m", RegexOptions.CultureInvariant);
        });

        await QuitAsync(host, run);
    }

    [Fact]
    public async Task The_theme_flag_name_wins_over_the_tui_theme_setting()
    {
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            environment: TrueColorEnvironment,
            launch: new LaunchOptions { Theme = "ansi16" }
        );
        WriteSettings(host, """{"schemaVersion": 2, "tui": {"theme": "mono"}}""");

        Task run = host.RunAsync();

        // ansi16 pins the sixteen-colour system over the setting's colourless frame, so the prompt
        // marker paints ANSI blue — the flag decides the selection before the setting is read.
        await host.WaitUntilAsync(Painted(host, "\u001b[34m"));
        Assert.DoesNotContain("\u001b[38;2", host.LastFrame, StringComparison.Ordinal);

        await QuitAsync(host, run);
    }

    [Fact]
    public async Task A_theme_flag_path_keeps_the_tui_theme_setting_as_its_base()
    {
        string flagPath = ScratchTheme("""{"prompt":"colour:208"}""");
        try
        {
            using var host = new InteractiveSessionHost(
                interactiveConsole: true,
                environment: TrueColorEnvironment,
                launch: new LaunchOptions { Theme = flagPath }
            );
            WriteSettings(host, """{"schemaVersion": 2, "tui": {"theme": "ansi16"}}""");

            Task run = host.RunAsync();

            // The flag names a file, not a built-in, so the setting is still the base theme and
            // pins the sixteen-colour plan: the file's 256 index paints the nearest of the sixteen
            // (bright yellow) rather than the exact entry a true-colour base would emit.
            await host.WaitUntilAsync(Painted(host, "\u001b[93m"));
            Assert.DoesNotContain("\u001b[38;5;208m", host.LastFrame, StringComparison.Ordinal);
            Assert.DoesNotContain("\u001b[38;2", host.LastFrame, StringComparison.Ordinal);

            await QuitAsync(host, run);
        }
        finally
        {
            File.Delete(flagPath);
        }
    }

    [Fact]
    public async Task An_extension_accent_segment_takes_the_accent_token_not_the_code_token()
    {
        var renderers = new ToolResultRendererRegistry();
        renderers.Register(
            "ext.renderer",
            new FixedRenderer("read", [new TextSegment("extension saw the read", TextStyle.Accent)])
        );
        using var host = new InteractiveSessionHost(
            toolResultRenderers: renderers,
            interactiveConsole: true,
            environment: TrueColorEnvironment,
            unifiedScrollback: true
        );
        // The two tokens are told apart: the accent segment must paint the accent, not the code
        // tone an Accent segment used to borrow.
        WriteHomeTheme(host, """{"accent":"#ff0000","code":"#00ff00"}""");
        File.WriteAllText(host.Temp.File("a.txt"), "contents");
        host.Client.Enqueue(
            Scripts.Call("call-1", "read", Scripts.Args(("path", "a.txt"))),
            Scripts.ToolCalls()
        );
        host.Client.Enqueue(Scripts.Text("done"), Scripts.Stop());
        Task run = host.RunAsync();

        host.Console.SendText("go");
        host.Console.SendEnter();
        await host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return host.Frames.Contains("extension saw the read", StringComparison.Ordinal);
        });

        Assert.Contains(
            "\u001b[38;2;255;0;0mextension saw the read",
            host.Frames,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "\u001b[38;2;0;255;0mextension saw the read",
            host.Frames,
            StringComparison.Ordinal
        );

        await QuitAsync(host, run);
    }

    /// <summary>
    /// The warning names the theme file's path, and the live area caps a notice at four lines
    /// (<c>LiveAreaRenderer.MaxNoticeLines</c>): a long temp root — macOS's
    /// <c>/var/folders/…/T</c>, where a Linux runner's <c>/tmp</c> is short — spends them on the
    /// path, so the frame ends in an ellipsis and the phrase after the path never reaches the
    /// terminal (#888). The theory runs both path lengths, because the harness's own temp root is
    /// short everywhere but macOS: the long home stands in for the macOS one.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(160)]
    public async Task An_unknown_token_warns_and_the_built_in_theme_stands(int pathPadding)
    {
        // Disposed after the host, so the long home outlives the session that reads it.
        using var padding = pathPadding == 0 ? null : new TempDirectory();
        using var host = new InteractiveSessionHost(
            interactiveConsole: true,
            environment: TrueColorEnvironment,
            homeDirectory: padding is null
                ? null
                : System.IO.Path.Combine(padding.Root, new string('h', pathPadding), "home")
        );
        string themeFile = WriteHomeTheme(host, """{"highlight":"red"}""");

        Task run = host.RunAsync();

        // The notice slot holds the warning the session posted, and a paint of that notice says a
        // frame showed it; neither reads the capped frame text, so no terminal width or path
        // length can hide the phrase this test is about.
        string warning =
            $"theme: {themeFile} names the unknown token 'highlight'; the valid tokens are {Lunate.Tui.ThemeTokens.List}";
        await host.WaitForNoticeAsync(warning);
        await host.WaitUntilAsync(() =>
        {
            host.Advance(33);
            return host
                .Notices.Changes(Lunate.Tui.NoticeSlotChange.Painted)
                .Any(entry => string.Equals(entry.Text, warning, StringComparison.Ordinal));
        });

        // The default prompt tone is still what paints: the file did not half-apply.
        await host.WaitUntilAsync(Painted(host, "\u001b[38;2;135;175;215m"));

        await QuitAsync(host, run);
    }

    /// <summary>Advances the scheduler until the live frame carries <paramref name="sgr"/>.</summary>
    private static Func<bool> Painted(InteractiveSessionHost host, string sgr) =>
        () =>
        {
            host.Advance(33);
            return host.LastFrame.Contains(sgr, StringComparison.Ordinal);
        };

    private static async Task QuitAsync(InteractiveSessionHost host, Task run)
    {
        host.Console.SendText("/quit");
        host.Console.SendEnter();
        await run;
    }

    private static string? TrueColorEnvironment(string name) =>
        name switch
        {
            "COLORTERM" => "truecolor",
            "OPENAI_API_KEY" => "test-key",
            _ => PrintModeTestSupport.Environment()(name),
        };

    /// <summary>The session home's own theme file: <c>&lt;home&gt;/.lunate/theme.json</c>.</summary>
    private static string WriteHomeTheme(InteractiveSessionHost host, string json)
    {
        string store = System.IO.Path.Combine(
            host.Options.HomeDirectory ?? host.Temp.File("home"),
            ".lunate"
        );
        Directory.CreateDirectory(store);
        string path = System.IO.Path.Combine(store, "theme.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static void WriteSettings(InteractiveSessionHost host, string json) =>
        File.WriteAllText(host.Temp.File("settings.json"), json);

    /// <summary>An extension block whose one segment carries the style under test.</summary>
    private sealed class FixedRenderer(string toolName, IReadOnlyList<TextSegment> segments)
        : IToolResultRenderer
    {
        public string ToolName => toolName;

        public IReadOnlyList<TextSegment> Render(ToolResultView view) => segments;
    }

    /// <summary>A scratch token file under the process temp root (<c>$TMPDIR</c> on POSIX).</summary>
    private static string ScratchTheme(string json)
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"lunate-theme-{Guid.NewGuid():N}.json"
            )
        );
        File.WriteAllText(path, json);
        return path;
    }
}
