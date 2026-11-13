## MODIFIED Requirements

### Requirement: Documentation entry points and guideline
`README.md` SHALL be the project entry point (what it is, status, quick start, links); `docs/dev/README.md` SHALL map every documentation kind to its home and state the writing guideline; `AGENTS.md` SHALL point to the map so every session finds it.

#### Scenario: One hop to every document kind
- **GIVEN** a reader starting at `README.md`
- **WHEN** they follow the documentation link
- **THEN** the map names the home of the guide, the specs, the in-flight changes, the ADRs and the spikes

#### Scenario: The guideline states where facts live
- **GIVEN** a contributor adding documentation
- **WHEN** they read the guideline
- **THEN** it tells them which document kind owns which fact and that behavior changes update the specs first

### Requirement: Architecture documentation
`docs/dev/architecture.md` SHALL present the system's structure as Mermaid diagrams: the component map (projects, dependency directions, external boundaries), the model pipeline order, one run as a sequence (model calls, tool execution, events, spans), and the event path from emission to consumers. Each diagram SHALL stay small, SHALL link to the spec or ADR that owns its detail, and SHALL be updated in the same change that alters the pictured structure.

#### Scenario: One hop from the map to the picture and back
- **GIVEN** a reader starting at the documentation map
- **WHEN** they follow the architecture entry
- **THEN** the page shows the four diagrams and each diagram links to its spec or ADR home

#### Scenario: Structure changes update the page
- **GIVEN** a change that alters the project graph or a pictured flow
- **WHEN** the change is reviewed
- **THEN** `docs/dev/architecture.md` is updated in the same change, and the review can verify each changed edge because the diagrams are text

### Requirement: ADR number verification
The documentation verification gate SHALL check primary ADR filename numbers for uniqueness and density, using filenames only. The check SHALL run in the `verify-docs` profile without invoking a build or test suite. Files named `NNNN-addendum-*.md` SHALL be treated as addenda to their parent ADR, not as separate primary ADR numbers. On a duplicate or gap, the diagnostic SHALL identify both relevant files, state any missing number, and report the next free number (one greater than the highest primary ADR number). The author-facing numbering rule SHALL be documented in `docs/dev/guide.md` because the repository has no ADR README.

#### Scenario: Duplicate primary numbers fail with actionable paths
- **GIVEN** two primary ADR files claim the same number
- **WHEN** the ADR numbering check runs
- **THEN** it fails and names both files and the next free number

#### Scenario: A gap in primary numbers fails with actionable paths
- **GIVEN** primary ADR files `0001-first.md` and `0003-third.md` with no `0002` file
- **WHEN** the ADR numbering check runs
- **THEN** it fails, names both gap-bracketing files, identifies `0002` as missing, and reports `0004` as the next free number

#### Scenario: A dense primary sequence passes
- **GIVEN** primary ADR files numbered consecutively from `0001` with no duplicate
- **WHEN** the ADR numbering check runs
- **THEN** it passes

#### Scenario: Numbered addenda do not create duplicate primary numbers
- **GIVEN** an ADR and one or more matching `NNNN-addendum-*.md` files
- **WHEN** the ADR numbering check runs
- **THEN** the addenda are excluded from primary-number uniqueness and density checks

#### Scenario: The check runs in the docs-only profile
- **GIVEN** the repository verification gate runs with `--profile docs`
- **WHEN** it reaches the documentation checks
- **THEN** ADR numbering is checked without a build or test suite
