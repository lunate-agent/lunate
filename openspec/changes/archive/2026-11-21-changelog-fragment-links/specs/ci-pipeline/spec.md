## MODIFIED Requirements

### Requirement: Changelog assembly

A committed script, `scripts/changelog-assemble`, SHALL use only the Python 3 standard library and SHALL provide three assembly modes: `--check` SHALL succeed only when the unreleased section of `CHANGELOG.md` carries the entries the fragments produce, `--write` SHALL rewrite that section from the fragments, and `--release <version>` SHALL perform the whole release pull request edit described by the *One-command release* requirement. The generated entries SHALL live in a marker-delimited region under the `## [Unreleased]` section, one region per heading, so text a person wrote there stays where it is. Within a heading, entries SHALL be ordered by ascending pull-request number; headings SHALL appear in the order Added, Changed, Fixed. `--write` SHALL be idempotent: a second run SHALL produce the same bytes. Content outside the unreleased section SHALL NOT change, except for the promotion `--release` makes.

Every relative link destination in an entry SHALL be resolved against the fragment's own directory and emitted relative to the repository root, because a fragment is written from `changes/` and the assembled entry is read from the root: `../adr/x.md` in `changes/1234.md` SHALL be assembled as `adr/x.md`, and `README.md` in `changes/1234.md` as `changes/README.md`. An `#anchor`, a root path (`/adr/x.md`), a URL carrying a scheme (`https:`, `http:`, `mailto:`, `tel:`, `data:`) and a link written inside an inline code span SHALL be carried as written, because each already resolves from any directory. The same SHALL hold for a reference-style definition the entry carries. A destination that resolves outside the repository from `changes/` SHALL be refused when the fragment is read, naming the fragment, the link and the path it resolves to relative to the repository root, so that `--check`, `--write`, `--check --tag` and `--release` all refuse the tree and write nothing.

Every mode that assembles SHALL first refuse a `CHANGELOG.md` whose shape the assembler cannot read reliably: within one version section a level-2 or level-3 heading SHALL appear at most once, in the unreleased section every non-blank line SHALL sit under a level-3 heading, and `## [Unreleased]` SHALL appear at most once in the file. Each finding SHALL name `CHANGELOG.md`, the line carrying it and what was found there, and a run SHALL report every finding before it writes anything. The assembly SHALL count the unreleased section's entries that the generated marker region does not own, before and after the rewrite, and SHALL refuse the write, naming both counts, when they differ, so a write never carries fewer entries than it read.

`--check` SHALL accept an optional `--tag <tag>`, valid only with `--check`, which SHALL additionally require, for `<version>` being the tag with its leading `v` removed, that the first level-2 heading below `## [Unreleased]` in `CHANGELOG.md` is exactly `## [<version>] - <YYYY-MM-DD>` with an exactly dashed and calendar-valid date, and that `<Version>` in `Directory.Build.props` is `<version>`. A tag without its leading `v` SHALL be refused by name. Each failure SHALL print what the check expected and what it found, and one run SHALL report every failure it finds. The Release workflow SHALL run `scripts/changelog-assemble --check --tag <tag>` at the tag, before anything is published.

The script SHALL provide a `--self-test` mode that reads no tree, exits non-zero when a case fails and names the failing case, and proves at least that a `../` link is rewritten for the root, that a link inside `changes/` is rewritten into `changes/`, that an anchor and a URL are carried as written, and that a link leaving the repository is refused with a message naming the fragment, the link and the resolved path.

#### Scenario: A missing fragment fails the check

- **GIVEN** a tree with `changes/1234.md` whose entry is absent from `CHANGELOG.md`
- **WHEN** `scripts/changelog-assemble --check` runs
- **THEN** it fails naming the fragment

#### Scenario: Write makes the check pass

- **GIVEN** the same tree
- **WHEN** `scripts/changelog-assemble --write` runs, then `--check` runs
- **THEN** the check passes

#### Scenario: Write is idempotent

- **GIVEN** an assembled tree
- **WHEN** `scripts/changelog-assemble --write` runs a second time
- **THEN** the file's bytes are unchanged

#### Scenario: A repeated heading in one version section fails by name

- **GIVEN** a `CHANGELOG.md` whose unreleased section carries `### Added` twice
- **WHEN** `scripts/changelog-assemble --check` runs
- **THEN** it fails naming `CHANGELOG.md`, the line of the second heading and the heading found there

#### Scenario: Text outside a heading in the unreleased section fails by name

- **GIVEN** a `CHANGELOG.md` whose unreleased section carries a bullet above its first `###` heading
- **WHEN** `scripts/changelog-assemble --check` and `--write` run
- **THEN** both fail naming the line and the text found there, and `--write` leaves the file unchanged

#### Scenario: A second unreleased section fails by name

- **GIVEN** a `CHANGELOG.md` carrying `## [Unreleased]` twice
- **WHEN** `scripts/changelog-assemble --check` runs
- **THEN** it fails naming both lines that carry the heading

#### Scenario: A clean changelog passes

- **GIVEN** a `CHANGELOG.md` with one section per version, at most one of each heading per section, and every unreleased entry under a heading
- **WHEN** `scripts/changelog-assemble --check` runs
- **THEN** it passes

#### Scenario: A write never carries fewer entries than it read

- **GIVEN** an unreleased section whose assembly would keep fewer entries than the section carries
- **WHEN** the assembly runs
- **THEN** it refuses, naming the count it read and the count it would write, and nothing is written

#### Scenario: Entries follow pull-request order under their heading

- **GIVEN** fragments `changes/1200.md` (Fixed), `changes/1199.md` (Fixed) and `changes/1201.md` (Added)
- **WHEN** the assembly writes the unreleased section
- **THEN** Fixed lists 1199 before 1200, and Added precedes Fixed

#### Scenario: Release leaves the check passing

- **GIVEN** a tree released by `--release <version>`
- **WHEN** `--check` runs
- **THEN** it passes, because the promoted section is no longer part of the unreleased section

#### Scenario: A correct tag passes the version heading check

- **GIVEN** a tree whose first heading below `## [Unreleased]` is `## [0.4.0] - 2026-11-14` and whose `Directory.Build.props` carries `<Version>0.4.0</Version>`
- **WHEN** `scripts/changelog-assemble --check --tag v0.4.0` runs
- **THEN** it passes

#### Scenario: A missing version heading fails by name

- **GIVEN** a tree with no level-2 heading below `## [Unreleased]`
- **WHEN** the tag check runs for `v0.4.0`
- **THEN** it fails naming the tag, the absent heading and the expected `## [0.4.0] - <YYYY-MM-DD>`

#### Scenario: A heading for another version fails by name

- **GIVEN** a tree whose first heading below `## [Unreleased]` is `## [0.3.0] - 2026-11-09`
- **WHEN** the tag check runs for `v0.4.0`
- **THEN** it fails printing the heading it found and the heading it expected

#### Scenario: A date the release process does not spell fails by name

- **GIVEN** a tree whose first heading below `## [Unreleased]` is `## [0.4.0] - 2026-1-2`
- **WHEN** the tag check runs for `v0.4.0`
- **THEN** it fails naming the date it found and the expected `YYYY-MM-DD` spelling

#### Scenario: A Directory.Build.props mismatch fails by name

- **GIVEN** a correct `## [0.4.0] - 2026-11-14` heading and `<Version>0.3.0</Version>`
- **WHEN** the tag check runs for `v0.4.0`
- **THEN** it fails naming `Directory.Build.props`, the version it found and the tag's version

#### Scenario: The tag check is a check-only option

- **GIVEN** a tree with `--write` or `--release` and `--tag v0.4.0`
- **WHEN** the script runs
- **THEN** it exits with a usage error and writes nothing

#### Scenario: A link out of the fragment's folder is rewritten for the root

- **GIVEN** `changes/1234.md` whose entry links `[ADR-0043](../adr/0043-contract-unions.md)`
- **WHEN** `scripts/changelog-assemble --write` assembles it into `CHANGELOG.md`
- **THEN** the entry carries `[ADR-0043](adr/0043-contract-unions.md)`, and `scripts/docs-check` finds no link escaping the repository in the result

#### Scenario: A link inside the fragment's folder is rewritten into it

- **GIVEN** `changes/1234.md` whose entry links `[the fragment rules](README.md)`
- **WHEN** the assembly writes the unreleased section
- **THEN** the entry carries `[the fragment rules](changes/README.md)`

#### Scenario: An anchor and a URL are carried as written

- **GIVEN** `changes/1234.md` whose entry links `[the section](../docs/dev/README.md#documentation-map)` and `[the site](https://example.com/lunate)`
- **WHEN** the assembly writes the unreleased section
- **THEN** the entry carries `docs/dev/README.md#documentation-map` and the URL unchanged

#### Scenario: A link that escapes the repository is refused by name

- **GIVEN** `changes/1234.md` whose entry links `[the guide](../../docs/dev/guide.md)`
- **WHEN** `scripts/changelog-assemble --check` runs
- **THEN** it fails naming `changes/1234.md`, the link and the path it resolves to (`../docs/dev/guide.md`), and `--write` writes nothing

#### Scenario: The self-test proves the rewrite and its refusal

- **GIVEN** a tree the assembler has never read
- **WHEN** `scripts/changelog-assemble --self-test` runs
- **THEN** it exits 0, having proven the rewrite, the carried forms and the escaping-link refusal
