## 1. Library boundary

- [x] 1.1 Create `src/Mermaid.Terminal` (net10.0, packable as `Mermaid.Terminal`, README, public API
      files, no Lunate reference) and `tests/Mermaid.Terminal.Tests`; add both to `lunate.slnx`.
- [x] 1.2 Define the public API: `MermaidRenderer.Render(source, width, glyphs)`,
      `MermaidRenderResult` with diagram/fallback results, `MermaidLine`/`MermaidSpan` with frame and
      label kinds, `MermaidGlyphSet`, and the public `MinimumWidth`; assert the assembly references
      no Lunate assembly in a test.
- [x] 1.3 Write the README usage example and a test that renders it and asserts the printed output.

## 2. Port the parsers

- [x] 2.1 Move the bounded flowchart parser (`flowchart`/`graph`, TD/TB/LR, labels, edges, `<br/>`)
      into the library, keeping its error messages and line numbers.
- [x] 2.2 Move the bounded sequence parser (participants, messages, notes) into the library, keeping
      its error messages and line numbers.

## 3. Port flowchart layout and drawing

- [x] 3.1 Port the node grid mapping, column/row sizing and A* path routing from mermaid-ascii.
- [x] 3.2 Port box drawing, line/arrowhead/corner drawing, junction merging and fan-out layout.
- [x] 3.3 Add flowchart golden pairs from mermaid-ascii's own testdata for `flowchart TD` and
      `flowchart LR`, Unicode and ASCII, and assert exact equality.

## 4. Port sequence layout and drawing

- [x] 4.1 Port the participant row, lifelines, messages and notes drawing from mermaid-ascii.
- [x] 4.2 Add sequence golden pairs from mermaid-ascii's own testdata, Unicode and ASCII, and assert
      exact equality.

## 5. Fallbacks and host adapter

- [x] 5.1 Port the unsupported-type, parse-failure and width fallbacks into the library with the
      documented reason strings; render never throws.
- [x] 5.2 Replace `Lunate.Tui`'s Mermaid implementation with an adapter over the library: keep the
      `mermaid` label line, paint frame dim and labels plain, show the fallback source and reason;
      delete the old canvas/layout/glyph/parser files and add the project reference.
- [x] 5.3 Update the Tui Mermaid golden fixtures (flowchart TD/LR, sequence, ASCII, mixed, width) to
      the ported output and run `MarkdownGoldenTests`/`MarkdownRendererTests`.

## 6. Documentation, packaging and decision

- [x] 6.1 Correct `docs/mermaid.md` to the ported rendering of its own example and its new glyph set.
- [x] 6.2 Credit the ported renderer and its MIT licence in `docs/third-party-licences.md`; add the
      ADR link where the documentation map expects it.
- [x] 6.3 Add `Mermaid.Terminal` to `scripts/pack-dry-run.sh` and `.github/workflows/release.yml`
      (pack, metadata check, push), and to `changes/<pr>.md`.
- [x] 6.4 Run `dotnet csharpier check .`, the touched builds and tests, `openspec validate
      mermaid-terminal-port --type change --strict`; then sync specs and archive as the last commit.
