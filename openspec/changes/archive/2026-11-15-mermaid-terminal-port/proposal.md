## Why

The Mermaid renderer that landed with #852 draws flowcharts with a hand-written layout, and
`docs/mermaid.md` is the proof that it is wrong: box borders misalign, arrow glyphs float away from
their edges, and the TUI and Provider APIs boxes overlap. A layout invented for this repository has
no authority to be correct; the drawing must come from a renderer that has been proven to lay
terminal Mermaid out correctly.

## What Changes

- **New library `Mermaid.Terminal`** (`src/Mermaid.Terminal`, `tests/Mermaid.Terminal.Tests`): a
  port of the layout and drawing of [`mermaid-ascii`](https://github.com/AlexanderGrooff/mermaid-ascii)
  (Go, MIT), with no dependency on any Lunate project and its own NuGet package, README and
  changelog entry. Public API parses a Mermaid source and renders it to lines for a width and a
  glyph set (Unicode or ASCII), or returns the fallback result for unsupported input.
- **BREAKING (visual):** the Unicode drawing changes from the hand-written rounded frame and
  `▸`/`▾` arrowheads to mermaid-ascii's box-drawing set and `►`/`▼`/`◄`/`▲` arrowheads; the layout
  of every existing diagram changes to the ported renderer's. ASCII keeps `+ - |` and `<>^v`.
- `Lunate.Tui` consumes the library through a project reference. Its Mermaid entry point keeps the
  documented fallback rules: unsupported type, parse failure and a drawing wider than the terminal
  still show the source plus one reason line, and a terminal narrower than 40 columns never draws.
- Golden tests in `tests/Mermaid.Terminal.Tests` compare our output with the original tool's own
  committed output for `flowchart TD`, `flowchart LR` and `sequenceDiagram`, in Unicode and ASCII.
- `docs/mermaid.md` shows the corrected drawing of its own example; its glyph table describes the
  ported set. `docs/third-party-licences.md` credits the ported renderer and its MIT licence.
- The new package joins the release workflow's pack step and the `pack.yml` dry run.

## Capabilities

### New Capabilities

- `mermaid-terminal`: the standalone renderer — parse a bounded Mermaid source, lay it out, render
  it to text lines for a width and glyph set, and return the documented fallback for anything it
  does not support.

### Modified Capabilities

- `tui`: a `mermaid` fence is drawn by the `Mermaid.Terminal` library; the existing fallback rules
  and the 40-column floor stay, and the glyphs are the ported renderer's Unicode/ASCII sets.

## Impact

- New: `src/Mermaid.Terminal/`, `tests/Mermaid.Terminal.Tests/`, `adr/0039-mermaid-terminal-library.md`.
- Changed: `src/Lunate.Tui/Mermaid/**` (hand-written layout replaced by a thin adapter),
  `src/Lunate.Tui/Lunate.Tui.csproj` (project reference), Tui Mermaid golden fixtures,
  `docs/mermaid.md`, `docs/third-party-licences.md`, `lunate.slnx`, `.github/workflows/release.yml`,
  `scripts/pack-dry-run.sh`, `changes/<pr>.md`.
- No new NuGet dependency: the renderer is source, not a package.
