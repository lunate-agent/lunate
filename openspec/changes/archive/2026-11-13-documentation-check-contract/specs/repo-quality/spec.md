## ADDED Requirements

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
