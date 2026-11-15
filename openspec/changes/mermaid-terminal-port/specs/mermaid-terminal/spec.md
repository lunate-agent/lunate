## ADDED Requirements

### Requirement: The renderer has no Lunate dependency

`Mermaid.Terminal` SHALL be a separate project and NuGet package under `src/Mermaid.Terminal` that
references no Lunate project and no Lunate type. It SHALL be usable from a program that knows
nothing about Lunate.

#### Scenario: The assembly references only the BCL

- **GIVEN** the `Mermaid.Terminal` project and its test project
- **WHEN** their project and assembly references are inspected
- **THEN** no project reference names a `Lunate.*` project, and the library's compiled assembly references only the base class library
- **AND** a test renders a diagram from an assembly that holds no Lunate reference

### Requirement: Parse and render a Mermaid source to lines

The public API SHALL accept a Mermaid source, a terminal width and a glyph set (Unicode or ASCII),
and return either the rendered lines or the fallback result. A rendered line SHALL carry its text
and the kind of each run — frame or label — so a host can paint them differently.

#### Scenario: A flowchart renders to text lines

- **GIVEN** a `flowchart TD` source whose nodes and edges are inside the supported grammar
- **WHEN** it is rendered at a width that fits and with the Unicode glyph set
- **THEN** the result is a rendered diagram whose lines carry the boxed labels and the arrows between them
- **AND** every line's text is the concatenation of its runs

#### Scenario: The same source renders with ASCII glyphs

- **GIVEN** the same source
- **WHEN** it is rendered with the ASCII glyph set instead
- **THEN** the frame uses `+`, `-` and `|` and the arrowheads use `<`, `>`, `^` and `v`
- **AND** the line and column count is the same as the Unicode rendering

### Requirement: The layout and drawing are a port of mermaid-ascii

The flowchart and sequence layout and drawing SHALL be a port of the MIT-licensed `mermaid-ascii`
renderer (Alexander Grooff), not a hand-written layout. The repository SHALL credit the renderer and
its licence in `docs/third-party-licences.md`.

#### Scenario: Golden output matches the original tool

- **GIVEN** the committed golden pair for a `flowchart TD`, a `flowchart LR` and a `sequenceDiagram`, each carrying the original tool's own output for that source
- **WHEN** our renderer renders the source at the same width and glyph set
- **THEN** our output equals the original tool's output exactly

### Requirement: Unsupported input falls back to the source

The renderer SHALL return a fallback result — the source lines and one reason — instead of a partial
diagram when the diagram type is unsupported, the source does not parse inside the bounded grammar,
or the drawing does not fit the width. It SHALL never throw for such input.

#### Scenario: An unsupported diagram type falls back

- **GIVEN** a `stateDiagram-v2` source
- **WHEN** it is rendered
- **THEN** the result is a fallback naming `unsupported diagram type 'stateDiagram-v2'`

#### Scenario: A drawing wider than the terminal falls back

- **GIVEN** a supported flowchart whose drawing is wider than the supplied width
- **WHEN** it is rendered
- **THEN** the result is a fallback naming the drawing's width and the terminal's width

#### Scenario: A terminal below the floor never draws

- **GIVEN** a supported source and a width below 40 columns
- **WHEN** it is rendered
- **THEN** the result is a fallback naming the terminal width and the 40-column minimum

### Requirement: The library ships as its own package

`Mermaid.Terminal` SHALL be packable as the NuGet package `Mermaid.Terminal`, with its own README
carrying a usage example with the rendered output, its own licence and changelog entry. The package
SHALL be packed by the release workflow and by the `pack.yml` dry run.

#### Scenario: The dry run packs the renderer

- **GIVEN** the repository at this change
- **WHEN** `scripts/pack-dry-run.sh` runs
- **THEN** it produces `Mermaid.Terminal.<version>.nupkg` and checks its metadata like every other release package
