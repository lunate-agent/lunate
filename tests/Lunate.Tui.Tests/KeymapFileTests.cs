using System.Text.RegularExpressions;
using Microsoft.Reactive.Testing;

namespace Lunate.Tui.Tests;

/// <summary>
/// The user keymap file: <see cref="Lunate.Tui.KeymapFile"/> reads <c>~/.lunate/keymap.json</c> —
/// the catalogue's overlay by binding id — and <see cref="Lunate.Tui.KeyBindingCatalog.Overlay"/>
/// applies it. Every test restores the catalogue's shipped rows afterwards; the catalogue is
/// static, and rows an overlay drops for a conflict (or an empty-keys disable) cannot come back
/// through <see cref="Lunate.Tui.KeyBindingCatalog.Remap"/>, which never accepts an empty key list.
/// </summary>
[Collection("keymap-catalogue")]
public sealed class KeymapFileTests
{
    private static readonly KeyBinding[] Shipped = [.. KeyBindingCatalog.All];

    [Fact]
    public void A_user_entry_replaces_its_bindings_keys_and_nothing_else()
    {
        try
        {
            KeymapOutcome outcome = Apply(
                """{"version":1,"bindings":[{"id":"ctrl-d.quit","keys":[{"kind":"Character","ctrl":true,"text":"t"}]}]}"""
            );

            Assert.Null(outcome.Error);
            Assert.Empty(outcome.ConflictLines);

            KeyBinding row = Assert.Single(KeyBindingCatalog.BindingsFor("ctrl-d.quit"));
            Assert.Equal(KeyKind.Character, row.Key.Kind);
            Assert.Equal("t", row.Key.Text);
            Assert.True(row.Key.Ctrl);
            Assert.Equal(RoutedKey.Exit, row.Intent);
            Assert.Equal(KeyContext.None, row.Contexts);
            Assert.Equal("Quit on a second press within 800 ms", row.Description);

            // Every binding the file does not name is exactly what shipped.
            Assert.Equal(
                Shipped.Where(binding => binding.Id != "ctrl-d.quit"),
                KeyBindingCatalog.All.Where(binding => binding.Id != "ctrl-d.quit")
            );
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void An_unknown_id_fails_the_load_naming_the_closest_catalogue_ids()
    {
        try
        {
            KeymapOutcome outcome = Apply(
                """{"version":1,"bindings":[{"id":"ctrl-dd.quit","keys":[{"kind":"Character","ctrl":true,"text":"t"}]}]}"""
            );

            Assert.Empty(outcome.ConflictLines);
            Assert.NotNull(outcome.Error);
            string error = outcome.Error!;
            Assert.Contains("is not a binding id", error, StringComparison.Ordinal);
            Match names = Regex.Match(
                error,
                "the closest names are (.*)",
                RegexOptions.CultureInvariant
            );
            Assert.True(names.Success, $"the error names nothing to try: {error}");
            string[] suggested = [.. names.Groups[1].Value.Split(", ")];
            Assert.True(
                suggested.Length is >= 1 and <= 3,
                $"the error suggests {suggested.Length} names: {error}"
            );
            Assert.Equal(
                suggested.Order(StringComparer.Ordinal),
                suggested,
                StringComparer.Ordinal
            );
            Assert.Contains("'ctrl-d.quit'", suggested, StringComparer.Ordinal);
            // The filter is real: ids that are not within three edits are not suggested.
            Assert.DoesNotContain("up.history-previous", error, StringComparison.Ordinal);
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void An_id_given_twice_fails_the_load_and_applies_nothing()
    {
        try
        {
            KeymapOutcome outcome = Apply(
                """{"version":1,"bindings":[{"id":"ctrl-d.quit","keys":[{"kind":"Character","ctrl":true,"text":"t"}]},{"id":"ctrl-d.quit","keys":[{"kind":"Tab"}]}]}"""
            );

            string error = outcome.Error!;
            Assert.Contains("'ctrl-d.quit' is given twice", error, StringComparison.Ordinal);
            AssertCatalogueUnchanged();
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void Two_of_the_files_own_bindings_in_conflict_fail_the_load_and_apply_nothing()
    {
        try
        {
            KeymapOutcome outcome = Apply(
                """{"version":1,"bindings":[{"id":"ctrl-o.transcript","keys":[{"kind":"Escape"}]},{"id":"ctrl-l.redraw","keys":[{"kind":"Escape"}]}]}"""
            );

            string error = outcome.Error!;
            Assert.Contains("'ctrl-o.transcript'", error, StringComparison.Ordinal);
            Assert.Contains("'ctrl-l.redraw'", error, StringComparison.Ordinal);
            Assert.Contains("that is a typo, not a preference", error, StringComparison.Ordinal);
            AssertCatalogueUnchanged();
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void The_file_beats_the_catalogue_when_both_answer_one_key()
    {
        try
        {
            KeymapOutcome outcome = Apply(
                """{"version":1,"bindings":[{"id":"ctrl-o.transcript","keys":[{"kind":"Enter"}]}]}"""
            );

            Assert.Null(outcome.Error);
            // One line per key the file takes over, naming the owning pair:
            Assert.Contains(
                "keymap: ctrl-o.transcript and enter.submit.idle both answer Enter "
                    + "in context Idle; ctrl-o.transcript wins",
                outcome.ConflictLines,
                StringComparer.Ordinal
            );

            KeyEvent enter = new(KeyKind.Enter, null, false, false, false);
            // Idle: the shipped submit row lost the key, the file's row wins.
            Assert.Equal(RoutedKey.TranscriptViewer, KeyRouter.Route(enter, turnRunning: false));
            // Running: the shipped follow-up row lost the key too.
            Assert.Equal(RoutedKey.TranscriptViewer, KeyRouter.Route(enter, turnRunning: true));
            // The entry replaced the binding's keys: the shipped Ctrl+O is gone.
            Assert.NotEqual(
                RoutedKey.TranscriptViewer,
                KeyRouter.Route(
                    new KeyEvent(KeyKind.Character, "o", Ctrl: true, Shift: false, Alt: false)
                )
            );
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void An_empty_keys_array_disables_a_binding()
    {
        try
        {
            KeymapOutcome outcome = Apply(
                """{"version":1,"bindings":[{"id":"escape.interrupt","keys":[]}]}"""
            );

            Assert.Null(outcome.Error);
            Assert.Empty(KeyBindingCatalog.BindingsFor("escape.interrupt"));
            Assert.Equal(
                RoutedKey.Edit,
                KeyRouter.Route(new KeyEvent(KeyKind.Escape, null, false, false, false))
            );
            Assert.Equal(Shipped.Length - 1, KeyBindingCatalog.All.Count);
        }
        finally
        {
            Restore();
        }
    }

    [Theory]
    [InlineData("""{"bindings":[{"id":"ctrl-d.quit"}]}""", "no 'keys' array")]
    [InlineData("""{"bindings":[{"keys":[{"kind":"Tab"}]}]}""", "no 'id' string")]
    [InlineData(
        """{"bindings":[{"id":"ctrl-d.quit","keys":[{"kind":"Banana"}]}]}""",
        "not a KeyKind member"
    )]
    public void A_malformed_binding_fails_the_load_and_applies_nothing(string json, string fragment)
    {
        try
        {
            string error = Apply(json).Error!;
            Assert.Contains(fragment, error, StringComparison.Ordinal);
            AssertCatalogueUnchanged();
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void A_file_that_is_not_a_keymap_fails_the_load()
    {
        try
        {
            Assert.Contains(
                "has no 'bindings' array",
                Apply("{}").Error!,
                StringComparison.Ordinal
            );
            Assert.Contains(
                "is not readable JSON",
                Apply("{ not json").Error!,
                StringComparison.Ordinal
            );
            AssertCatalogueUnchanged();
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void A_missing_file_is_not_an_error()
    {
        try
        {
            using var dir = new TempDir();
            KeymapOutcome outcome = KeymapFile.Apply(
                System.IO.Path.Combine(dir.Path, "nowhere", "keymap.json")
            );

            Assert.Null(outcome.Error);
            Assert.Empty(outcome.ConflictLines);
            AssertCatalogueUnchanged();
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void A_rebound_key_reaches_its_intent_through_the_real_key_reader()
    {
        try
        {
            KeymapOutcome outcome = Apply(
                """{"version":1,"bindings":[{"id":"ctrl-d.quit","keys":[{"kind":"Character","ctrl":true,"text":"y"}]}]}"""
            );
            Assert.Null(outcome.Error);

            var scheduler = new TestScheduler();
            var keys = new List<KeyEvent>();
            using KeyReader reader = new(scheduler, keys.Add);
            reader.Feed([0x19]); // the byte Ctrl+Y produces
            scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);

            KeyEvent key = Assert.Single(keys);
            Assert.Equal("y", key.Text);
            Assert.True(key.Ctrl);
            // The bytes typed went into the real reader, and the key that came out reaches the
            // real router with the rebound meaning — not a hand-built KeyEvent.
            Assert.Equal(RoutedKey.Exit, KeyRouter.Route(key));
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void The_specs_scenario_enter_submits_on_ctrl_j_and_no_longer_in_idle()
    {
        try
        {
            KeymapOutcome outcome = Apply(
                """{"version":1,"bindings":[{"id":"enter.submit.idle","keys":[{"kind":"Character","ctrl":true,"text":"j"}]}]}"""
            );
            Assert.Null(outcome.Error);

            // Ctrl+J submits in the idle context, though the catalogue binds Ctrl+J to a newline.
            KeyEvent ctrlJ = new(KeyKind.Character, "j", Ctrl: true, Shift: false, Alt: false);
            Assert.Equal(RoutedKey.Submit, KeyRouter.Route(ctrlJ, turnRunning: false));

            // Enter no longer submits there, and the shipped Ctrl+J newline is gone.
            KeyEvent enter = new(KeyKind.Enter, null, false, false, false);
            Assert.NotEqual(RoutedKey.Submit, KeyRouter.Route(enter, turnRunning: false));
            Assert.NotEqual(RoutedKey.Edit, KeyRouter.Route(ctrlJ, turnRunning: false));

            // The displaced catalogue binding is named in one startup line, and the file wins.
            Assert.Contains(
                outcome.ConflictLines,
                line => line.Contains("newline.ctrl-j", StringComparison.Ordinal)
            );
        }
        finally
        {
            Restore();
        }
    }

    [Fact]
    public void The_path_resolves_flag_over_variable_over_the_home_file()
    {
        Assert.Equal(
            "/flag/keymap.json",
            KeymapFile.ResolvePath("/flag/keymap.json", "/home", _ => "/env/keymap.json")
        );
        Assert.Equal(
            "/env/keymap.json",
            KeymapFile.ResolvePath(null, "/home", _ => "/env/keymap.json")
        );
        // The fallback is the home's own keymap.json — asserted as "that file in that directory",
        // not by rebuilding the same Path.Combine the resolver uses.
        AssertHomeFile(KeymapFile.ResolvePath(null, "/home", _ => null));
        // A blank variable is unset, the same rule LUNATE_HOME itself follows.
        AssertHomeFile(
            KeymapFile.ResolvePath(null, "/home", name => name == KeymapFile.Variable ? " " : null)
        );
    }

    private static void AssertHomeFile(string path)
    {
        Assert.Equal(KeymapFile.FileName, Path.GetFileName(path));
        Assert.Equal(Path.GetFullPath("/home"), Path.GetDirectoryName(path));
    }

    private static KeymapOutcome Apply(string json)
    {
        using TempDir dir = new();
        string path = dir.File("keymap.json");
        File.WriteAllText(path, json);
        return KeymapFile.Apply(path);
    }

    private static void AssertCatalogueUnchanged() => Assert.Equal(Shipped, KeyBindingCatalog.All);

    private static void Restore() => KeyBindingCatalog.Replace(Shipped);

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            Directory.CreateTempSubdirectory("lunate-keymap-tests-").FullName;

        public string File(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
