## MODIFIED Requirements

### Requirement: Changelog assembly

A committed script, `scripts/changelog-assemble`, SHALL use only the Python 3 standard library and SHALL provide three modes: `--check` SHALL succeed only when the unreleased section of `CHANGELOG.md` carries the entries the fragments produce, `--write` SHALL rewrite that section from the fragments, and `--release <version>` SHALL perform the whole release pull request edit described by the *One-command release* requirement. The generated entries SHALL live in a marker-delimited region under the `## [Unreleased]` section, one region per heading, so text a person wrote there stays where it is. Within a heading, entries SHALL be ordered by ascending pull-request number; headings SHALL appear in the order Added, Changed, Fixed. `--write` SHALL be idempotent: a second run SHALL produce the same bytes. Content outside the unreleased section SHALL NOT change, except for the promotion `--release` makes.

`--check` SHALL accept an optional `--tag <tag>`, valid only with `--check`, which SHALL additionally require, for `<version>` being the tag with its leading `v` removed, that the first level-2 heading below `## [Unreleased]` in `CHANGELOG.md` is exactly `## [<version>] - <YYYY-MM-DD>` with an exactly dashed and calendar-valid date, and that `<Version>` in `Directory.Build.props` is `<version>`. A tag without its leading `v` SHALL be refused by name. Each failure SHALL print what the check expected and what it found, and one run SHALL report every failure it finds. The Release workflow SHALL run `scripts/changelog-assemble --check --tag <tag>` at the tag, before anything is published.

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
