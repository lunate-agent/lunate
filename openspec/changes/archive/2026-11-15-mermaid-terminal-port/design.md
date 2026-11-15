## Context

#852/#879 landed a Mermaid renderer in `src/Lunate.Tui/Mermaid`: a bounded parser plus a layout
written by hand for this repository. Its output is visibly broken — `docs/mermaid.md` shows box
borders misaligned, arrow glyphs detached from their edges, and the TUI and Provider APIs boxes
overlapping — because nothing proved the layout. The card's decision (2026-11-15) also settles that
the renderer becomes its own library: `src/Mermaid.Terminal`, `tests/Mermaid.Terminal.Tests`, the
NuGet id `Mermaid.Terminal` (checked free 2026-11-15), no Lunate reference, packed by the release
workflow and by `pack.yml`'s dry run.

The in-force ADRs constrain this design: ADR-0002 (layering and MEAI model types), ADR-0019
(Xenoatom terminal UI) and ADR-0037 (CI gate sized by event) are reviewed in the change's ADR
manifest. None covers a renderer library; ADR-0039 records this boundary.

## Goals / Non-Goals

**Goals:**

- Replace the hand-written layout and drawing with a faithful C# port of `mermaid-ascii`'s
  flowchart and sequence layout and drawing, inside `Lunate.Tui`'s grammar.
- Keep the documented fallbacks and the 40-column floor exactly as `docs/mermaid.md` states them.
- Compare our output with the original tool's own committed output for `flowchart TD`,
  `flowchart LR` and `sequenceDiagram`.
- Ship `Mermaid.Terminal` as a Lunate-free package with a README usage example and the rendered
  output.
- Correct `docs/mermaid.md` to the new drawing and credit the ported renderer.

**Non-Goals:**

- Porting mermaid-ascii's whole grammar: subgraphs, `style`/`classDef`/`click`, ER diagrams,
  `alt`/`loop`/`par` fragments, actors with activation and the rest stay outside the bounded
  grammar and keep falling back. The card keeps the tui-customization fallback rules.
- New NuGet packages, a new diagram type, or a repository split.
- Matching mermaid-ascii's `--max-width` wrapping; the width rule is still our all-or-nothing fit.

## Decisions

**Port scope: layout and drawing, not the parser.** `mermaid-ascii`'s graph package is ~2,500 lines
including subgraphs, styles, parallel edges and fan-out; its sequence package is ~2,100. The
existing parsers already implement the bounded grammar the docs promise, including the exact
one-line error messages `docs/mermaid.md` shows. The port therefore replaces the layout/drawing half
and keeps a parser for the same grammar, moved into the library. Ported layout pieces: node grid
mapping (3×3 cells per node with 4-cell level pitch), column/row sizing, A* grid path routing,
arrowheads and corners, junction-character merging, box centring, fan-out layout, sequence
participant/lifeline/message/note drawing. This is what makes the picture correct; porting the
grammar would add fallbacks the docs do not promise.

**Alternatives.** `mmdflux`/`termiflow` (Rust) were the card's alternatives; mermaid-ascii is the
first choice, is MIT and its committed testdata (Unicode and ASCII) makes golden comparison possible
without a Go toolchain. Writing the parser from upstream too was rejected because upstream's parser
accepts far more than our docs promise, which would silently widen the supported surface.

**Golden comparison.** The original tool's own `pkg/graph/testdata/{ascii,extended-chars}` and
`cmd/testdata/sequence{,ascii}` files are the tool's verified output: each holds the source, `---`,
and the output its suite asserts. Fixtures for this change copy those pairs for the three required
diagram kinds, and `MermaidRendererTests` renders the source and asserts exact equality. Our own
drawing must match byte for byte, including trailing spaces (the upstream drawing pads every line to
the full grid). A test that reads only the fixture files is not enough; the renderer test compares.

**Public API.** `MermaidRenderer.Render(string source, int width, MermaidGlyphSet glyphs)` returns
`MermaidRenderResult`, either `MermaidDiagramResult` (lines of `MermaidSpan` runs tagged Frame or
Label) or `MermaidFallbackResult` (source lines and one reason). `MermaidGlyphSet` is `Unicode` or
`Ascii`. The span kinds are public because the host must paint frame and labels differently; a plain
string list would force every host to re-parse the drawing. `MinimumWidth` is a public constant the
host uses for its documentation and the library enforces the same rule.

**Glyph sets.** Unicode uses mermaid-ascii's light box-drawing set (`┌ ┐ └ ┘ ─ │ ┬ ┴ ├ ┤ ┼`) with
`► ▼ ◄ ▲` heads and `┄`/`┆` dotted strokes; ASCII uses `+ - |` with `> v < ^`. The old rounded
corners and small triangles are removed: matching the original tool's output is the acceptance, and
a different glyph set cannot match it.

**Host boundary.** `Lunate.Tui` keeps only `MermaidDiagram` as an adapter: it splits the fence into
lines, calls the library with the terminal width and capability, maps spans to `StyledLine` runs
(frame dim, label plain) or writes the fallback source and reason, and keeps the `mermaid` label
line. The old `MermaidCanvas`, `MermaidGlyphs`, flowchart and sequence layout files are deleted; the
parser/model code moves into `Mermaid.Terminal`.

**Packaging.** The library gets `IsPackable`, `PackageId Mermaid.Terminal`, the product `<Version>`,
MIT licence expression, the repository URL/type, the shared brand icon and its own `README.md`.
`pack-dry-run.sh` packs it and checks its metadata under the derived `TOOL` version, and
`release.yml` packs, checks and pushes it beside the other packages. The README's usage example is
itself rendered and asserted by a test, so the printed output cannot go stale.

## Risks / Trade-offs

- **[Port drift]** A mechanical port can diverge on a corner case our fixtures do not cover. ->
  Port the routing and drawing function-for-function, keep the upstream names/comments where they
  matter, and keep golden pairs for TD, LR, sequence, ASCII and Unicode.
- **[Our grammar is narrower than upstream's fixtures]** Some upstream fixtures use operators our
  parser refuses. -> Choose fixtures inside the documented grammar; the golden requirement is for
  the three diagram kinds, not for every upstream feature.
- **[Wider drawings]** The ported layout can be wider than the old hand layout (extra routing rows),
  so a diagram that used to draw can now fall back in a narrow terminal. -> This is the documented
  all-or-nothing width rule; update the affected Tui goldens and keep the 40-column floor.
- **[Trailing whitespace]** mermaid-ascii pads each drawing line to the grid width, so golden lines
  end in spaces and docs/`text` fences would show them. -> The host trims line ends when it maps to
  `StyledLine` (existing Tui tests compare display strings); the library keeps the padded lines and
  the goldens compare them, which is what "matches the original tool" means.

## Migration Plan

No data migration. The change ships in one pull request: library, host adapter, tests, docs, ADR,
changelog fragment, pack steps, then archive. Rollback is reverting the pull request; the data format
and session format are untouched.

## Open Questions

None. The card's maintainer decision settles the project name, NuGet id, package boundary and pack
step; the ADR records the boundary, not a new choice.
