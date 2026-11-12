# ci-pipeline — changelog fragments

## ADDED Requirements

### Requirement: Per-pull-request changelog fragments

A pull request SHALL record each release-note line in its own fragment at `changes/<pull-request-number>.md` and SHALL NOT edit `CHANGELOG.md`; only the release pull request assembles the changelog. A fragment SHALL name exactly one heading the release can file it under — `### Added`, `### Changed` or `### Fixed` — followed by one or more Markdown bullets in the changelog's shape, each ending with the pull request reference `(#<number>)`. A fragment whose file name is not a decimal pull-request number, whose heading is not one of the three accepted headings, whose body carries text outside the heading and its bullets, or whose bullets do not carry its own pull-request number SHALL be reported as malformed. Two fragment files that resolve to the same pull-request number SHALL be reported as a duplicate, naming both files.

`changes/README.md` SHALL state the rule: a pull request writes its fragment and never the changelog.

#### Scenario: A fragment-only pull request passes

- **GIVEN** a pull request whose only prose change is `changes/1234.md`
- **WHEN** the gate runs
- **THEN** the changelog guard passes, and the fragment-only change selects the documentation profile

#### Scenario: A non-release pull request that edits the changelog fails

- **GIVEN** a pull request that adds `changes/1234.md` and also edits `CHANGELOG.md`
- **WHEN** the changelog guard runs
- **THEN** it fails with a message naming `changes/1234.md`, so the aggregate gate fails

#### Scenario: A malformed fragment is reported

- **GIVEN** `changes/1234.md` with no accepted heading, or a bullet that does not end with `(#1234)`
- **WHEN** the assembler checks the tree
- **THEN** it fails and names the fragment and the rule it broke

#### Scenario: Two fragments for one number are reported

- **GIVEN** `changes/1234.md` and `changes/01234.md`
- **WHEN** the assembler checks the tree
- **THEN** it fails and names both files as the same pull-request number

#### Scenario: An unknown heading is reported

- **GIVEN** `changes/1234.md` whose heading is `### Improved`
- **WHEN** the assembler checks the tree
- **THEN** it fails and names the fragment and the unknown heading

### Requirement: Changelog assembly

A committed script, `scripts/changelog-assemble`, SHALL use only the Python 3 standard library and SHALL provide two modes: `--check` SHALL succeed only when the unreleased section of `CHANGELOG.md` carries the entries the fragments produce, and `--write` SHALL rewrite that section from the fragments. The generated entries SHALL live in a marker-delimited region under the `## [Unreleased]` section, one region per heading, so text a person wrote there stays where it is. Within a heading, entries SHALL be ordered by ascending pull-request number; headings SHALL appear in the order Added, Changed, Fixed. `--write` SHALL be idempotent: a second run SHALL produce the same bytes. Content outside the unreleased section SHALL NOT change.

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

### Requirement: The release pull request assembles the changelog

A pull request whose branch is `release/<version>` or whose title starts `chore(release)` SHALL be treated as the release pull request: it MAY edit `CHANGELOG.md`, and it SHALL run `scripts/changelog-assemble --write` and commit the result. The changelog guard SHALL exempt exactly those two signals so the assembler is not blocked by the check it exists to satisfy. The first release after the fragments land SHALL consume the fragments and SHALL leave already-released history unchanged.

#### Scenario: The release pull request may edit the changelog

- **GIVEN** a pull request on branch `release/0.4.0` that runs `--write` and commits `CHANGELOG.md`
- **WHEN** the changelog guard runs
- **THEN** it passes, and the release pull request may be released and merged

#### Scenario: A release pull request is recognised by its title too

- **GIVEN** a pull request titled `chore(release): 0.4.0` on a normal feature branch
- **WHEN** the changelog guard runs
- **THEN** it passes

## MODIFIED Requirements

### Requirement: Aggregate merge gate

The CI workflow SHALL expose a single aggregate status check (`ci-ok`) that succeeds only when every verification job the selected profile requires has completed successfully and every job that profile does not select was skipped, so renaming, adding, or removing a matrix leg cannot silently drop required coverage. The repository ruleset SHALL require the aggregate check for `main` — a one-time maintainer configuration outside the workflow's control. The aggregate SHALL report a result on every pull request: the workflow SHALL NOT apply path filters to pull-request events. A required job that is skipped or cancelled SHALL NOT count as success, a job that ran although the profile did not select it SHALL fail the aggregate, and a run whose profile cannot be read SHALL fail rather than pass.

Two jobs MAY be conditional on the changed paths: the Windows PowerShell leg and the changelog fragment gate. The PowerShell skip SHALL count as success only when the path decision output says the leg was not required; a skip or cancellation while the decision says it was required SHALL fail the aggregate, and an unreadable decision SHALL fail it. The decision SHALL be produced by a committed script with a self-test, and the leg SHALL also run on every push to `main`, every tag, a nightly schedule and manual dispatch, so the filter cannot hide a regression indefinitely. The changelog gate SHALL run on every non-draft pull request regardless of profile, and SHALL fail when the pull request is not the release pull request and its changed paths include `CHANGELOG.md`; its skip SHALL count as success only outside a pull-request event, and a pull-request run in which it did not succeed SHALL fail the aggregate.

#### Scenario: Aggregate green when every job passes

- **GIVEN** a pull request whose verification jobs all succeed
- **WHEN** the aggregate check runs
- **THEN** it completes green

#### Scenario: Aggregate red when a job fails or is skipped

- **GIVEN** a pull request where any verification job fails or is skipped without a path decision saying it was not required
- **WHEN** the aggregate check runs
- **THEN** it fails, so the merge stays blocked

#### Scenario: A path-filtered leg skipped legitimately

- **GIVEN** a pull request that touches only prose and Linux-only code
- **WHEN** the PowerShell leg is skipped because the path decision says it is not required
- **THEN** the aggregate completes green

#### Scenario: A path-filtered leg skipped illegitimately

- **GIVEN** a pull request that changes a PowerShell script
- **WHEN** the PowerShell leg is skipped or cancelled although the path decision says it is required
- **THEN** the aggregate fails

#### Scenario: Aggregate reports on every pull request

- **GIVEN** a pull request that touches only documentation paths
- **WHEN** CI runs for the pull request
- **THEN** the aggregate check reports a result (no path filter can leave a required check pending)

#### Scenario: Aggregate green for a prose pull request

- **GIVEN** a pull request whose changed paths are all prose and whose docs leg succeeds
- **WHEN** the aggregate check runs
- **THEN** it completes green while the full matrix legs are skipped

#### Scenario: Aggregate red when the plan is unreadable
- **GIVEN** a run whose plan job produced no usable profile
- **WHEN** the aggregate check runs
- **THEN** it fails instead of passing vacuously

#### Scenario: Aggregate red when a non-release pull request edits the changelog

- **GIVEN** a pull request that edits `CHANGELOG.md` and is not the release pull request
- **WHEN** the aggregate check runs
- **THEN** it fails, and the changelog job names `changes/<pull-request-number>.md`

#### Scenario: The release pull request's changelog edit is allowed

- **GIVEN** a pull request on `release/0.4.0` that edits `CHANGELOG.md`
- **WHEN** the aggregate check runs
- **THEN** the changelog job succeeds and the aggregate can complete green
