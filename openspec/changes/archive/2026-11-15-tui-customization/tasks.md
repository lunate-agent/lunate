# Tasks — tui-customization

Four parts, in this order. Each part is a separate PR and is useful without the ones after it; the
change archives only when the last part lands.

## 1. Keymap

- [x] 1.1 Load `~/.lunate/keymap.json`, overlaying `keys.json` by binding id; `$LUNATE_KEYMAP` and
      `--keymap <path>` select the file. — #671; `KeymapFileTests`, `InteractiveSessionKeymapTests`.
- [x] 1.2 Refuse an unknown binding id with the id and up to three nearest catalogue ids; refuse a
      user binding that invents an id; allow an empty `keys` array to disable a binding. — #671;
      `KeymapFileTests.An_unknown_id_fails_the_load_naming_the_closest_catalogue_ids`,
      `KeymapFileTests.An_empty_keys_array_disables_a_binding`.
- [x] 1.3 Resolve conflicts: user file beats the catalogue with one startup line naming both ids; two
      user bindings in conflict fail the load. — #671;
      `KeymapFileTests.The_file_beats_the_catalogue_when_both_answer_one_key`,
      `KeymapFileTests.Two_of_the_files_own_bindings_in_conflict_fail_the_load_and_apply_nothing`,
      `InteractiveSessionKeymapTests.The_file_beats_the_catalogue_with_one_startup_line`.
- [x] 1.4 Tests: overlay, unknown id, invented id, disable, catalogue conflict, user-user conflict,
      and one test that a rebound key reaches its intent through the real key reader. — #671;
      `KeymapFileTests`, `InteractiveSessionKeymapTests`.

## 2. Themes

- [x] 2.1 Token set (`accent`, `muted`, `error`, `success`, `warning`, `code`, `diff.add`,
      `diff.remove`, `prompt`, `notice`) and the three built-ins; route every existing colour use
      through a token. — #856; `TuiThemeTests`, `ThemeGoldenTests`.
- [x] 2.2 Parse `~/.lunate/theme.json` (ANSI name, 256 index, hex) and fail on an unknown token with
      the valid list. — #856; `ThemeFileTests.An_unknown_token_fails_the_load_and_lists_the_ten_valid_ones`,
      `TuiThemeTests`.
- [x] 2.3 Downgrade by capability: hex to 256, to 16, to none; `NO_COLOR` wins over any theme. — #856,
      #911; `ThemeColorTests`, `ThemeGoldenTests`, `InteractiveSessionThemeFileTests`.
- [x] 2.4 Tests: each colour form, each downgrade, unknown token, `NO_COLOR` precedence, and a golden
      of one transcript under each built-in theme. — #856; `ThemeFileTests`, `ThemeColorTests`,
      `ThemeGoldenTests`, `InteractiveSessionThemeFileTests`.

## 3. Full screen

- [x] 3.1 `--fullscreen` and `tui.fullscreen: true`; enter after the first frame, leave on every exit
      path including an exception. — #878; `InteractiveSessionFullScreenTests`,
      `CliTests.Launch_options_parse_fullscreen`.
- [x] 3.2 Tests: enter/leave on normal quit, on `Ctrl+C`, and on a thrown exception; inline remains the
      default when neither flag nor setting is present. — #878; `InteractiveSessionFullScreenTests`.

## 4. Mermaid diagrams

- [x] 4.1 Parser for `flowchart`/`graph` (`TD`/`TB`/`LR`, labelled nodes, edges with and without
      labels) and `sequenceDiagram` (participants, `->`, `-->`, `->>`, `Note`); everything else
      classified as unsupported. — #879, #881, #884; `MermaidRendererTests`, `MermaidSequenceTests`.
- [x] 4.2 Flowchart layout and renderer with the box-drawing set; sequence renderer with lifelines and
      message labels. — #879, #881, #884; `MermaidRendererTests.A_flowchart_renders_boxed_labels_and_arrows`,
      `MermaidSequenceTests.A_sequence_draws_participant_boxes_lifelines_messages_and_notes`.
- [x] 4.3 Fallback rules: unsupported type, parse failure and width overflow each render the source
      with the exact one-line note from the design; never a partial diagram. — #879, #881, #884;
      `MermaidRendererTests`, `MermaidSequenceTests`,
      `MarkdownGoldenTests.Fixture_matches_the_committed_golden`.
- [x] 4.4 ASCII mode swaps the glyph set without changing geometry. — #879, #881;
      `MermaidRendererTests.Ascii_uses_the_same_geometry_with_the_portable_glyphs`,
      `MermaidSequenceTests.Ascii_sequence_keeps_the_geometry_and_swaps_the_glyphs`,
      `MarkdownGoldenTests.Ascii_mode_replaces_mermaid_glyphs`.
- [x] 4.5 Route a ```mermaid fence from the Markdown renderer to the diagram renderer; unsupported
      constructs in ordinary fences keep their current behaviour. — #879, #900; `MermaidAdapterTests`,
      `MarkdownGoldenTests`.
- [x] 4.6 Tests: one golden per supported diagram type (TD, LR, sequence), the three fallbacks, ASCII
      mode, and a document mixing a diagram with code fences and prose. — #879, #881;
      `MarkdownGoldenTests.Fixture_matches_the_committed_golden`, `MermaidSequenceTests`.

## 5. Docs and gate

- [x] 5.1 `docs/` page for the keymap, the theme tokens and the full-screen option; one example file
      each under `docs/` (not in the repo root). — #856, #878, #879, #881; `docs/themes.md`,
      `docs/themes-and-keys.md`, `docs/full-screen.md`, `docs/mermaid.md`, `docs/examples/`.
- [x] 5.2 `scripts/verify.sh` unchanged; the suites carry the new tests. — #671, #856, #878, #879, #881;
      `scripts/verify.sh` is untouched.
- [x] 5.3 Archive the change into `openspec/specs/tui/spec.md` in the last part's PR. — #881 archived it;
      `openspec/specs/tui/spec.md` carries the four requirements.
