## Context

The `tui-customization` change shipped ten semantic tokens behind three built-ins: `default` (the
shipped palette with sixteen-colour fallbacks), `ansi16` (named ANSI colours, pinning the sixteen-
colour depth) and `mono` (no colour). None of them names the assumption the palette makes about the
terminal's *background*: every colour in `default` is a pastel ink chosen for a dark background, so a
user on a light terminal gets washed-out accent and warning colours. The only escape today is a
`theme.json` that rewrites all ten tokens, which is a per-user workaround, not a selection.

The pieces this change builds on are in place and stay untouched: `TuiTheme` (name, ten-token
palette, `Plan` for the colour depth, `Span`/`Wrap` for rendering), `ThemeColor` (named, 256-index
and hex forms with the capability downgrade), `ThemeFile` (the user overlay, discarded whole on a
failure) and `InteractiveSession.ApplyTheme` (the `--theme` > `tui.theme` > `default` order).

## Goals / Non-Goals

**Goals:**

- A user on either background selects a palette with one word: `--theme light` or `--theme dark`.
- `dark` is byte-for-byte the palette `default` paints, so nobody's current look changes and
  `default` remains the setting's default value.
- `light` is legible on a light background at every colour depth the product supports.
- The choice composes with what exists: `theme.json` overlays it, `NO_COLOR` still wins, and the
  colour-depth policies (`ansi16`, `mono`) still do their job independently.

**Non-Goals:**

- No second selection mechanism (no `--background`, no `tui.background`, no terminal-background
  detection). Detection is unreliable across terminals and would silently change the look of an
  existing session; the existing `--theme`/`tui.theme` order already carries the choice.
- No new tokens, no glyph changes, no syntax-highlighting scheme, no change to `ansi16` or `mono`.
- No change to the theme-file format or its error messages.

## Decisions

**Two new names in the existing selection, not a second axis.** `light` and `dark` join the built-in
name list that `TuiTheme.TryGet`, the settings parser, the schema's `allowed` list and the CLI help
already share. Alternative: a separate `tui.background` setting crossed with the depth policy, which
would double the name space, need a new settings key and produce combinations (`ansi16` × `light`)
whose meaning is unclear. Palette names stay one flat list.

**`dark` repeats `default`'s palette instead of aliasing it.** Both are built from the same token
table, but they are distinct names with distinct `Name` values, so `tui.theme` keeps `default` as its
documented default, the existing goldens keep their file name, and a reader of the settings table sees
the relationship. Alternative: make `dark` the canonical name and `default` an alias, which would
change the persisted default value and every doc that names it for no behavioural gain.

**`light` paints the named base ANSI colours, not hex pastels.** The light palette maps `accent` to
teal (6), `muted` to grey (8), `error` to maroon (1), `success` to green (2), `warning` to olive (3),
`code` to navy (4), `diff.add` to green (2), `diff.remove` to maroon (1), `prompt` to navy (4) and
`notice` to purple (5). Named colours render as their names at every depth, so the light theme is
legible on a sixteen-colour terminal as well as a true-colour one, and the inks are the dark half of
the ANSI palette, which is what a light background needs. Alternative: hex values tuned for white,
which downgrade to the nearest of the sixteen — and the built-in's pinned fallbacks exist precisely
because a pastel's nearest neighbour is often wrong.

**The new palettes choose inks, not depth.** `Plan` only special-cases `mono` and `ansi16`; `light`
and `dark` return the detected depth, so `NO_COLOR` and the detection rules still win and the new
names cannot raise a colourless session back to colour. A `light` selection on a mono terminal stays
colourless rather than forcing colour on.

**Evidence: one new golden per name, existing goldens untouched.** `ThemeGoldenTests` renders one
transcript under each built-in, so `light` and `dark` get a `light.txt` and a `dark.txt`; `dark.txt`
duplicates `default.txt` on purpose — it pins that the two names paint the same bytes. No existing
golden changes, because no existing palette changes.

**Docs and schema move with the code.** `settings-schema.json` is the source of the generated
settings table, so its `allowed` list and description are edited first and `scripts/docs-sync`
regenerates `docs/settings.md`; `docs/themes.md`, `docs/themes-and-keys.md`, `CliHelp` and the
`LaunchOptions.Theme` comment name the five built-ins.

## Risks / Trade-offs

- **The light palette uses ten tokens over seven usable ANSI inks, so some tokens share a colour**
  (success and `diff.add`; error and `diff.remove`; code and prompt). -> Accepted: the surfaces never
  put the sharing pair side by side as distinct meanings, `ansi16` already shares in the same way, and
  weight still separates them.
- **A user whose stored `tui.theme` is `default` still gets the dark palette on a light terminal.**
  -> Documented: `light` is the explicit choice; background detection is a non-goal because guessing
  wrong is worse than asking once.
- **Two more golden files to keep current.** -> They are additive and each is a one-line regeneration
  step from `ThemeGoldenTests`.

## Migration Plan

None. Every stored setting stays valid, every name that existed keeps its palette, and a missing
selection behaves exactly as before. Rollback is dropping the two names; no state is written.

## Open Questions

None. No in-force ADR is superseded: the palette list is a built-in theme table, not a durable
architectural commitment (the TUI foundation and the Mermaid library ADRs are untouched).
