## ADDED Requirements

### Requirement: One-command release

`scripts/changelog-assemble` SHALL provide a release mode,
`--release <version> [--date YYYY-MM-DD]`, that performs the whole release pull request edit in one
run against the repository root: it SHALL assemble the fragments into the unreleased section as
`--write` does, SHALL promote that section to `## [<version>] - <date>` with the generated marker
comments removed, SHALL leave a fresh empty `## [Unreleased]` section above the promoted one, SHALL
delete every fragment it consumed, and SHALL set `<Version>` in `Directory.Build.props` to the
released version. `--date` SHALL default to the current date in UTC and SHALL be read as
`YYYY-MM-DD`. A successful run SHALL leave `--check` passing and SHALL change exactly three kinds of
path: `CHANGELOG.md`, `Directory.Build.props` and the deleted `changes/<n>.md` fragments.

The mode SHALL refuse, naming the reason and writing nothing at all, when the requested version is
not newer than the current `<Version>` (compared by semantic-version precedence, so a prerelease
precedes the release it belongs to), when `CHANGELOG.md` already carries a `## [<version>]` heading,
when `CHANGELOG.md` carries no `## [Unreleased]` section, or when any fragment is invalid. Running
the same release twice SHALL therefore be refused the second time and SHALL NOT promote twice. A
tree with no fragments left SHALL still release: the empty unreleased section is promoted and the
version is bumped.

The release runbook (`docs/dev/distribution.md`, step 1) and `changes/README.md` SHALL name this
command instead of the manual promote, strip, delete and bump steps.

#### Scenario: A normal release does the whole edit

- **GIVEN** a tree whose `Directory.Build.props` carries `<Version>0.4.0</Version>` and whose `changes/` holds fragments
- **WHEN** `scripts/changelog-assemble --release 0.5.0 --date 2026-12-01` runs
- **THEN** `CHANGELOG.md` carries `## [0.5.0] - 2026-12-01` with the fragments' entries and no `changelog-fragments` markers, a fresh empty `## [Unreleased]` sits above it, the consumed fragments are deleted, `<Version>` reads `0.5.0`, and `--check` passes

#### Scenario: A prerelease releases

- **GIVEN** the same tree
- **WHEN** `scripts/changelog-assemble --release 0.5.0-rc.1 --date 2026-12-01` runs
- **THEN** the promoted heading is `## [0.5.0-rc.1] - 2026-12-01` and `<Version>` reads `0.5.0-rc.1`

#### Scenario: The date defaults to today in UTC

- **GIVEN** a tree with fragments
- **WHEN** `scripts/changelog-assemble --release 0.5.0` runs without `--date`
- **THEN** the promoted heading carries the current UTC date, and a second run is refused

#### Scenario: A version that is not newer is refused

- **GIVEN** a tree at `<Version>0.4.0</Version>`
- **WHEN** `--release 0.4.0` or `--release 0.3.9` runs
- **THEN** it fails naming the requested version and the current one, and `CHANGELOG.md`, `Directory.Build.props` and `changes/` are unchanged

#### Scenario: An existing release heading is refused

- **GIVEN** a `CHANGELOG.md` that already carries `## [0.5.0] - 2026-11-01`
- **WHEN** `--release 0.5.0` runs
- **THEN** it fails naming the existing heading, and nothing is written

#### Scenario: An invalid fragment is refused

- **GIVEN** a tree with `changes/1234.md` whose heading is `### Improved`
- **WHEN** `--release 0.5.0` runs
- **THEN** it fails naming `changes/1234.md` and the rule it broke, and nothing is written

#### Scenario: A second run is refused

- **GIVEN** a tree released to `0.5.0` by a first run
- **WHEN** `--release 0.5.0` runs again
- **THEN** it is refused, `CHANGELOG.md` still carries exactly one `## [0.5.0]` heading, and nothing is written

#### Scenario: A release with no fragments left still releases

- **GIVEN** a tree with no `changes/*.md` fragment and a `Directory.Build.props` at `0.4.0`
- **WHEN** `--release 0.5.0 --date 2026-12-01` runs
- **THEN** `CHANGELOG.md` carries an empty `## [0.5.0] - 2026-12-01` section under a fresh `## [Unreleased]`, `<Version>` reads `0.5.0`, and `--check` passes

## MODIFIED Requirements

### Requirement: Changelog assembly

A committed script, `scripts/changelog-assemble`, SHALL use only the Python 3 standard library and SHALL provide three modes: `--check` SHALL succeed only when the unreleased section of `CHANGELOG.md` carries the entries the fragments produce, `--write` SHALL rewrite that section from the fragments, and `--release <version>` SHALL perform the whole release pull request edit described by the *One-command release* requirement. The generated entries SHALL live in a marker-delimited region under the `## [Unreleased]` section, one region per heading, so text a person wrote there stays where it is. Within a heading, entries SHALL be ordered by ascending pull-request number; headings SHALL appear in the order Added, Changed, Fixed. `--write` SHALL be idempotent: a second run SHALL produce the same bytes. Content outside the unreleased section SHALL NOT change, except for the promotion `--release` makes.

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

### Requirement: The release pull request assembles the changelog

A pull request whose branch is `release/<version>` or whose title starts `chore(release)` SHALL be treated as the release pull request: it MAY edit `CHANGELOG.md`, and it SHALL run `scripts/changelog-assemble --release <version>` and commit the result. The changelog guard SHALL exempt exactly those two signals so the assembler is not blocked by the check it exists to satisfy. The first release after the fragments land SHALL consume the fragments and SHALL leave already-released history unchanged.

The Release workflow SHALL fail a tag whose commit is not on `main` or whose commit is not associated with a merged pull request carrying one of the two release signals, so a release cannot be cut from a release branch the lane has not merged. The lane's own release and merge execution SHALL be tracked in lunate-lane#21. After attaching the assets, the Release workflow SHALL read the published release back and SHALL fail unless it is not a draft and carries its `SHA256SUMS` asset. A committed script SHALL make both decisions and SHALL carry test cases for an unmerged release pull request, a tagged non-release pull request, a draft release and a release without `SHA256SUMS`. The release runbook SHALL name the same verification.

#### Scenario: The release pull request may edit the changelog

- **GIVEN** a pull request on branch `release/0.4.0` that runs `--release 0.5.0` and commits `CHANGELOG.md`
- **WHEN** the changelog guard runs
- **THEN** it passes, and the release pull request may be released and merged

#### Scenario: A release pull request is recognised by its title too

- **GIVEN** a pull request titled `chore(release): 0.4.0` on a normal feature branch
- **WHEN** the changelog guard runs
- **THEN** it passes

#### Scenario: A tag outside main fails

- **GIVEN** a release tag on a commit that is not an ancestor of `origin/main`
- **WHEN** the Release workflow's gate runs
- **THEN** it fails and says to merge the release pull request before tagging

#### Scenario: A tag from a non-release pull request fails

- **GIVEN** a tagged commit whose associated merged pull request has neither a `release/<version>` branch nor a `chore(release)` title
- **WHEN** the Release workflow's gate runs
- **THEN** it fails and names the two release signals

#### Scenario: A merged release pull request may be released

- **GIVEN** the release pull request merged into `main` with the assembled changelog
- **WHEN** its commit is tagged and the Release workflow runs
- **THEN** the commit is on `main`, is associated with the merged release pull request, the changelog check passes, and the release is published

#### Scenario: A release without its assets fails

- **GIVEN** a tagged release whose GitHub release is still a draft or has no `SHA256SUMS` asset
- **WHEN** the Release workflow reads the release back
- **THEN** it fails and names the release as incomplete
