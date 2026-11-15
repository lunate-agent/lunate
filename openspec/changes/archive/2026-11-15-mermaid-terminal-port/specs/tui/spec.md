## ADDED Requirements

### Requirement: Mermaid fences are drawn by the renderer library

A fenced code block labelled `mermaid` SHALL be drawn by the `Mermaid.Terminal` library's ported
layout and drawing. The TUI SHALL keep the fence label line, paint the frame runs with the dim
attribute and the label runs plain, and pass the terminal's width and Unicode/ASCII capability to
the library.

#### Scenario: A supported diagram is drawn

- **GIVEN** an answer whose fence is a supported `flowchart`/`graph` (TD, TB or LR) or `sequenceDiagram`
- **WHEN** the Markdown renderer draws it in a terminal wide enough
- **THEN** the answer carries the library's drawing, its frame dim and its labels plain, under the fence's `mermaid` label line

#### Scenario: A terminal without Unicode gets ASCII glyphs

- **GIVEN** a terminal that reports no Unicode support
- **WHEN** the same fence is drawn
- **THEN** the frame is `+`, `-` and `|` and the arrowheads are `<`, `>`, `^` and `v`, with the same layout

### Requirement: The documented Mermaid fallbacks stay

The TUI SHALL keep the three fallbacks: an unsupported diagram type, a parse failure and a drawing
wider than the terminal each show the fence's source followed by exactly one line
`mermaid: <reason> — showing the source`. A terminal narrower than 40 columns SHALL never draw a
diagram. Rendering SHALL be all-or-nothing and SHALL never throw.

#### Scenario: An unsupported diagram type shows its source

- **GIVEN** a fence whose first statement is `stateDiagram-v2`
- **WHEN** the answer is drawn
- **THEN** the source appears unchanged followed by `mermaid: unsupported diagram type 'stateDiagram-v2' — showing the source`

#### Scenario: A parse failure shows its source and the parser's line

- **GIVEN** a fence whose second line is `participant A as`
- **WHEN** the answer is drawn
- **THEN** the source appears unchanged followed by `mermaid: line 2: a participant needs a display name after 'as' — showing the source`

#### Scenario: A drawing too wide shows its source

- **GIVEN** a supported diagram that is 64 columns wide and a 58-column terminal
- **WHEN** the answer is drawn
- **THEN** the source appears unchanged followed by `mermaid: diagram is 64 columns wide, this terminal has 58 — showing the source`

#### Scenario: A terminal below the floor never draws

- **GIVEN** a supported diagram and a terminal of 39 columns
- **WHEN** the answer is drawn
- **THEN** the source appears unchanged followed by `mermaid: this terminal has 39 columns, 40 is the minimum — showing the source`
