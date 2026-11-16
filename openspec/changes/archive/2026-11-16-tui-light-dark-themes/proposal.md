# Light and dark themes

## Why

The `tui-customization` change gave the interface one palette behind ten semantic tokens, but the
selectable built-ins are `default`, `ansi16` and `mono` — a colour-depth choice, not a choice of
terminal background. A user on a light terminal gets the shipped palette's pastel inks, which wash
out against white and can be unreadable; the only escape is to rewrite every token in `theme.json`.
Issue #378 asks for the light/dark selection the theme part of that change promised, and the review
of the implementation found it absent from both the code and the synced requirement.

## What Changes

- Add two built-in themes: `light` and `dark`.
  - `dark` paints the palette the product ships (the same tokens as `default`), so the existing dark
    default is now nameable.
  - `light` paints dark inks for a light-background terminal: teal accent, grey muted, maroon error,
    green success, olive warning, navy code and prompt, purple notice, maroon/green diffs.
- Both are selected by the existing order — `--theme <name>`, then the `tui.theme` setting, then
  `default` — with no new flag, setting or environment variable.
- `theme.json` still overlays whichever built-in is selected, `NO_COLOR` still wins, and neither new
  theme changes the colour depth the terminal's detection reports: a theme chooses inks, not depth
  (`ansi16` and `mono` keep that job).
- The settings schema's allowed values, the generated settings table, the CLI help, the theme guides
  and the launch-option documentation list the five names.
- Two new theme goldens, one per new built-in; the existing goldens are untouched.

No breaking changes: `default` keeps the palette and stays the setting's default value, and every
stored `tui.theme` value remains valid.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `tui`: the **Theme tokens** requirement names five built-in themes and adds the light/dark
  selection and its scenarios.

## Impact

- `src/Lunate.Tui/TuiTheme.cs` — two built-in palettes and the selectable-name list.
- `src/Lunate.Coding/settings-schema.json`, `src/Lunate.Coding/CliHelp.cs`,
  `src/Lunate.Coding/LaunchOptions.cs` — the accepted names in the schema, help and doc comment.
- `docs/themes.md`, `docs/themes-and-keys.md`, `docs/settings.md` (generated) — the two names.
- Tests: `TuiThemeTests`, `ThemeGoldenTests` (two new goldens), `SettingsSchemaTests`,
  `SettingsStoreTuiTests`, `InteractiveSessionTuiSettingsTests`, `InteractiveSessionThemeFileTests`.
