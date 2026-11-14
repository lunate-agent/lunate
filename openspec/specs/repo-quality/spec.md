# repo-quality Specification

## Purpose
Repository quality conventions: one deterministic C# formatter (CSharpier) that yields identical output for humans and models, documentation linting (markdownlint) in the verify gate, contribution templates, and a documentation map and writing guideline that tell every session where each kind of document lives.

## Requirements

### Requirement: Deterministic C# formatting
The repository SHALL format all C# code with CSharpier, pinned as a local dotnet tool in `.config/dotnet-tools.json` and configured by a committed `.csharpierrc.yaml`; the verify gate on both platforms SHALL check it (`dotnet csharpier check .`). Analyzer and style checks SHALL continue through `dotnet format style` and `dotnet format analyzers` with whitespace formatting and `IDE0055` disabled.

#### Scenario: Unformatted file fails the gate
- **GIVEN** a C# file that differs from CSharpier's output
- **WHEN** the verify gate runs
- **THEN** the format step fails and names the file

#### Scenario: Formatted tree passes
- **GIVEN** a tree formatted by CSharpier
- **WHEN** the verify gate runs
- **THEN** the format step passes

### Requirement: Documentation lint
Markdown documentation SHALL be linted with markdownlint-cli2 (pinned version, committed configuration) as part of the verify gate, covering `README.md`, `AGENTS.md`, `docs/` and `adr/`; `openspec/` is excluded because it is tool-managed.

#### Scenario: A violation fails the gate
- **GIVEN** a markdown file in scope with a lint violation
- **WHEN** the verify gate runs
- **THEN** the documentation lint step fails and reports the violation

#### Scenario: Clean documentation passes
- **GIVEN** documentation without violations
- **WHEN** the verify gate runs
- **THEN** the documentation lint step passes

### Requirement: Contribution templates
The repository SHALL provide a pull request template carrying the review-map sections and issue templates for bug reports and feature requests.

#### Scenario: Opening a pull request prefills the template
- **GIVEN** a new pull request
- **WHEN** the body is composed
- **THEN** the review-map sections (what this is, what lands, review map, verification, notes) are prefilled

#### Scenario: Opening an issue offers the templates
- **GIVEN** a new issue
- **WHEN** the reporter picks a template
- **THEN** a bug report or feature request form is offered

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

### Requirement: The gate has two stages

The repository SHALL define two gates in `AGENTS.md`: a fast gate for iteration (`dotnet csharpier check .`, `openspec validate <change> --strict`, a Release build and the test classes covering the change) and the full gate (`scripts/verify.sh`, `scripts/verify.ps1` on Windows). The fast gate SHALL be the condition for committing and pushing during a change; the full gate SHALL be the condition for marking a change ready for review and for calling it done; `ci-ok` SHALL be green before a merge. The full gate's contents SHALL NOT change with this definition.

#### Scenario: An intermediate commit

- **WHEN** an agent has a compiling change with its focused tests green and CSharpier clean
- **THEN** it may commit and push without running the full gate

#### Scenario: Asking for review

- **WHEN** a change is proposed for review
- **THEN** `scripts/verify.sh` has passed on the head being reviewed

#### Scenario: Archiving

- **WHEN** a change's implementation is reviewed and the fast gate is green
- **THEN** `openspec archive <change> --yes` is the final commit of that change's pull request, and the pull request merges once `ci-ok` is green

#### Scenario: Merging

- **WHEN** a pull request is merged
- **THEN** the `ci-ok` check is green

### Requirement: Canonical bytes for files Lunate owns

Files Lunate owns or generates — the install record, extension locks, `trust.json`, session files, fixtures, exports and logs — SHALL be written as UTF-8 without a byte-order mark with LF line endings on every platform. Their bytes SHALL NOT depend on the platform or the culture that produced them, and a digest over such a file SHALL be computed over exactly those bytes.

#### Scenario: A record written on one machine verifies on another
- **GIVEN** an install record written on Windows
- **WHEN** its `integrity` digest is recomputed on POSIX
- **THEN** the record verifies

#### Scenario: A lock's bytes carry no carriage return
- **GIVEN** an extension lock serialized with the canonical options
- **WHEN** the bytes are inspected
- **THEN** they contain no `\r`, and writing, reading and re-writing the lock yields identical bytes

#### Scenario: The serializer never follows the platform
- **GIVEN** a machine whose `Environment.NewLine` is CRLF
- **WHEN** any hashed document is serialized
- **THEN** the serializer's `NewLine` is `"\n"` and the digest is unchanged from a POSIX machine

### Requirement: Digests over user-owned content normalize line endings

A digest over content the user owns (single-file extension source, content-hashed skills, the MCP configuration) SHALL be computed over text normalized to LF, so the same content with CRLF and with LF hashes identically. A pure line-ending change SHALL therefore not re-prompt for trust, which the trust documentation states.

#### Scenario: CRLF and LF sources share a digest
- **GIVEN** one extension source with LF line endings and the same source with CRLF
- **WHEN** both are digested
- **THEN** the digests are equal

#### Scenario: A line-ending change does not re-prompt
- **GIVEN** a trusted single-file extension
- **WHEN** only its line endings change
- **THEN** no trust prompt appears and the trust record still matches

### Requirement: Committed fixtures keep their bytes

Committed text that is hashed, compared or generated SHALL be pinned to LF in `.gitattributes`, and test inputs whose line endings are themselves the subject SHALL be marked `-text` so git never converts them. The repository SHALL verify that fixtures outside those byte-exact corpora are LF before pinning them.

#### Scenario: A Windows checkout gets the committed bytes
- **GIVEN** a Windows checkout with `core.autocrlf=true`
- **WHEN** a golden frame is read
- **THEN** it carries LF, exactly as committed

#### Scenario: The edit corpus stays byte-exact
- **GIVEN** the CRLF files under `tests/fixtures/edit-corpus/`
- **WHEN** any platform checks them out
- **THEN** their bytes are unchanged

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

### Requirement: Source file names name a type the file declares

Every C# source file under `src/` SHALL be named after a type it declares, or after no type at all.
The leading segment of a file's base name — the text before the first `.`, so a partial-class file
such as `AgentHarness.Retries.cs` is judged by `AgentHarness` — SHALL NOT be the name of a type that
the file does not declare, so a file name never resolves to a type another file owns. A file whose
leading segment is not a type name anywhere under `src/` is an aggregate file and is allowed. The
repository SHALL enforce the rule with a test that scans `src/` on every platform, reads the
top-level type declarations of each file, and names every offending path together with the file that
declares the type.

#### Scenario: A file named after another file's type fails

- **GIVEN** a file under `src/` whose leading base-name segment is a type declared in a different file
- **WHEN** the source file name test runs
- **THEN** it fails and names both the offending file and the file that declares that type

#### Scenario: A partial-class file passes

- **GIVEN** `src/Lunate.Agent/AgentHarness.Retries.cs` declaring `partial class AgentHarness`
- **WHEN** the source file name test runs
- **THEN** it passes, because the file declares the type its name leads with

#### Scenario: An aggregate file passes

- **GIVEN** a file whose leading base-name segment is not a type name anywhere under `src/`, such as
  `src/Lunate.Extensibility.Abstractions/Hooks/HookPayloads.cs`
- **WHEN** the source file name test runs
- **THEN** it passes, because the name claims no type

#### Scenario: The renamed files pass on every platform

- **GIVEN** `RunFileChangeSink.cs`, `CommandDescriptor.cs`, `RemoteExtensionException.cs`,
  `UnixChildProcess.cs` and `WindowsChildProcess.cs`
- **WHEN** the source file name test runs on POSIX or Windows
- **THEN** it passes, and no file under `src/` is named after a type it does not declare

### Requirement: Documentation links, anchors, redirects and navigation

A committed checker, `scripts/docs-check`, SHALL keep the documentation tree honest about where its
pages are, using only the Python 3 standard library. Every relative link and every `#anchor` in the
root documents and the documentation pages SHALL resolve, every moved page SHALL be a redirect stub
that points at its target, and `docs/docs.json` SHALL be the machine-readable map of the tree.

The manifest SHALL carry `navigation` (groups of titled pages), `redirects` (one `from`/`to` pair per
moved page) and `proseOnly` (pages that have no runnable example yet, each with a reason). The check
SHALL report, naming the page and, for a marker, its line: a navigation path that is not a file, a
navigation path listed twice, a user-path page missing from `navigation`, a relative link to a
missing target, a fragment that names no heading, a link that escapes the repository, a redirect
whose source is not the standard four-line stub or whose marker or sentence does not point at its
target, a `proseOnly` entry without a reason or outside `navigation`, and a user-path page with
neither a marker nor a `proseOnly` entry. A maintainer page that moves into `docs/dev/` SHALL be a
rename, not a redirect, and needs no stub; a user-path page that moves or splits SHALL keep the stub.

Anchors SHALL be derived from headings outside fenced code blocks, using the renderer's rule
(lowercase, punctuation dropped, spaces as hyphens, a numeric suffix for a repeated heading) plus
explicit `id` and `name` attributes, so a heading quoted inside a fenced block is never an anchor.

A user-path page SHALL carry at least one runnable marker: a `snippet` marker whose source file
exists, a `config` marker above a fenced JSON block (checked as JSON, and a settings example also
against the settings schema the product reads), or a `run` marker above a fenced bash block. A `run`
marker SHALL name how the block is checked — `offline`, `help`, or `no` with a one-line reason — and
the check SHALL report a marker that sits above no fenced block, an unknown kind and a `no` without a
reason. `--run` SHALL execute the `offline` and `help` blocks against the published binary with an
isolated home and a temporary working directory, SHALL never execute a `no` block, and SHALL run no
network command in CI.

The checker SHALL have a self-test that covers its failure paths, and the verify gate SHALL run the
check, its self-test and — once the binary is published — its `--run` pass.

#### Scenario: A broken relative link or anchor fails the gate

- **GIVEN** a documentation page with a relative link whose target file does not exist, or whose `#anchor` names no heading
- **WHEN** `scripts/docs-check --check` runs
- **THEN** it fails and names the page and the link that does not resolve

#### Scenario: A manifest that misses or duplicates a page fails

- **GIVEN** a `docs/docs.json` naming a navigation path that does not exist, listing one path twice, or omitting a user-path page
- **WHEN** `scripts/docs-check --check` runs
- **THEN** it fails and names the path

#### Scenario: A moved user-path page is a stub, a maintainer page is a rename

- **GIVEN** a redirect entry whose source page is a stub carrying any content beyond the marker, the heading and the moved-page sentence
- **WHEN** `scripts/docs-check --check` runs
- **THEN** it fails and names the stub
- **AND** a redirect into `docs/dev/` whose source no longer exists passes, because a maintainer rename needs no stub

#### Scenario: A page without a runnable example and without a reason fails

- **GIVEN** a user-path page carrying no `snippet`, `config` or `run` marker and no `proseOnly` entry with a reason
- **WHEN** `scripts/docs-check --check` runs
- **THEN** it fails and names the page

#### Scenario: The checker's own failure paths are covered

- **GIVEN** the checker's self-test
- **WHEN** `scripts/docs-check --self-test` runs
- **THEN** it builds one small tree per failure path — missing navigation path, unlisted page, duplicate path, broken link, broken anchor, heading inside a fence, missing stub, stub with extra content, prose-only without a reason, unmarked page, invalid config example, refused settings key, `run: no` without a reason, navigation-region mismatch and a failing `run` block — and fails if any of them goes unreported
