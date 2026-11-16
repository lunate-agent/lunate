# Tasks — tui-light-dark-themes

Read `design.md` before starting; its decisions are binding. Tests come before behaviour and each
task names the test class that proves it.

## 1. Light and dark palettes

- [x] 1.1 `TuiThemeTests` red: `light` and `dark` resolve, the literals list the five names, `dark`
      paints every token exactly as `default` does, `light` paints a distinct palette for all ten
      tokens, and both return the detected colour depth from `Plan` (including `NO_COLOR`).
- [x] 1.2 Add `Dark` and `Light` to `TuiTheme`, extend `Names`, `Literals` and `TryGet`, and give
      `light` its named-ANSI palette; green 1.1.
- [x] 1.3 `ThemeGoldenTests`: add `light` and `dark` to the built-in set and commit `light.txt` and
      `dark.txt` (regenerate with `LUNATE_TUI_UPDATE_GOLDENS=1`); the three existing goldens stay
      byte-identical.

## 2. Selection surfaces

- [x] 2.1 `SettingsSchemaTests` and `SettingsStoreTuiTests` red: the schema's `allowed` list holds
      the five names in `TuiTheme.Names` order, and `tui.theme: "light"` / `"dark"` are accepted
      while the refusal message names the five literals.
- [x] 2.2 Update `settings-schema.json` and regenerate `docs/settings.md` with
      `scripts/docs-sync`; green 2.1.
- [x] 2.3 `InteractiveSessionThemeFileTests` red: `--theme light` paints the light accent and a
      `theme.json` still overlays only the token it names; `tui.theme: light` selects it across a
      restart. Also update the refusal-notice assertion in `InteractiveSessionTuiSettingsTests`.
- [x] 2.4 `CliHelp` (`--theme` line) and the `LaunchOptions.Theme` comment name the five built-ins;
      `CliTests` acceptance of `--theme light` covers the flag.

## 3. Documentation

- [x] 3.1 `docs/themes.md` and `docs/themes-and-keys.md` describe `light` and `dark`, what each
      paints, and that the choice is the existing `--theme` > `tui.theme` > `default` order.
- [x] 3.2 `scripts/docs-sync --check` and `scripts/docs-check` pass; `dotnet csharpier check .` is
      clean.

## 4. Verify and archive

- [x] 4.1 Run the named test classes: `TuiThemeTests`, `ThemeGoldenTests`, `SettingsSchemaTests`,
      `SettingsStoreTuiTests`, `InteractiveSessionThemeFileTests`,
      `InteractiveSessionTuiSettingsTests`, `CliTests`.
- [x] 4.2 `openspec validate tui-light-dark-themes --strict`, write `changes/918.md`, then archive
      this change as the branch's last commit.
