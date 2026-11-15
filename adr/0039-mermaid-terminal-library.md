# ADR-0039: The Mermaid renderer is its own Lunate-free library

Status: accepted — 2026-11-15

## Context

The Mermaid renderer that landed with #852 lived inside `Lunate.Tui` and laid diagrams out with code
written for this repository. The result is visibly wrong (`docs/mermaid.md` shows misaligned borders,
detached arrow glyphs and overlapping boxes) and, because it was internal to the TUI, no other host
could use it and no external renderer could prove it. The maintainer's decision on #882 settles that
the renderer becomes a standalone library: `src/Mermaid.Terminal` with `tests/Mermaid.Terminal.Tests`,
no reference to any Lunate project, shipped as its own NuGet package with its own README, version and
changelog entry, consumed by `Lunate.Tui` as a project reference. The NuGet id `Mermaid.Terminal` was
checked free on 2026-11-15. A move to its own repository comes later, once others use it.

The in-force ADRs reviewed for this change are ADR-0002 (layering and MEAI model types), ADR-0019
(Xenoatom terminal UI) and ADR-0037 (CI gate sized by event). None of them covers a renderer library;
ADR-0019 puts terminal rendering in `Lunate.Tui`, and this ADR narrows that to the TUI's use of the
renderer, not to the renderer's placement.

## Decision

1. **`Mermaid.Terminal` is a project and a package of its own.** It lives at `src/Mermaid.Terminal`
   with `tests/Mermaid.Terminal.Tests`, has `PackageId Mermaid.Terminal`, carries the product version
   and MIT licence, and references nothing from Lunate — no project reference, no Lunate type. Its
   assembly's only references are the base class library.

2. **Its public boundary is parse-and-render.** `MermaidRenderer.Render` takes a Mermaid source, a
   width and a glyph set (Unicode or ASCII) and returns either the rendered lines with their frame
   and label runs, or the fallback result (source lines plus one reason) for unsupported input. The
   width rule, the 40-column floor and the fallback reasons live in the library, not in the host.

3. **The layout and drawing are a port of mermaid-ascii.** The MIT-licensed Go renderer by Alexander
   Grooff is the source for flowchart and sequence layout and drawing; the port is credited in
   `docs/third-party-licences.md`. It is a port, never a dependency or a new package. The bounded
   grammar the repository documents stays ours, so an unsupported statement still degrades instead
   of widening the surface.

4. **`Lunate.Tui` consumes it as a project reference and keeps only an adapter.** The TUI owns the
   `mermaid` label line and the painting of frame versus label runs; the drawing itself never lives
   in the TUI again. `Lunate.Tui`'s layering (it references no other Lunate project) is unchanged
   because the library is not a Lunate project.

5. **The package joins the release.** `release.yml`'s pack/check/push steps and
   `scripts/pack-dry-run.sh` (what `pack.yml` runs) both pack `Mermaid.Terminal` beside the other
   release packages; a package that is not in the dry run is not in the release.

## Consequences

- **The renderer is usable outside Lunate.** Its README is a real usage example with rendered
  output, and a test asserts that output, so the page cannot drift from the code.
- **The TUI loses its private Mermaid implementation.** `src/Lunate.Tui/Mermaid` keeps only the
  adapter; the canvas, glyph table, layout and drawing code move or are deleted.
- **A visual break is accepted.** The old rounded frame and `▸`/`▾` heads are replaced by the ported
  renderer's `┌ ┐ └ ┘ ─ │` and `► ▼ ◄ ▲` (ASCII `+ - |` and `> < ^ v`), because matching the original
  tool is the acceptance criterion. `docs/mermaid.md` and its glyph table change with it.
- **The port must be kept in step deliberately.** Upstream can change; this repository owns the
  port, its fixtures and its bugs. The golden pairs are the record of the version that was ported.
- **No new NuGet package, so the licence table gains a port, not a dependency row.** The credit names
  the upstream project, its author and its MIT licence.
- **The project stays in this repository for now.** A later move to its own repository is a new ADR,
  not an edit to this one.
