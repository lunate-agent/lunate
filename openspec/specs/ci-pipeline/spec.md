# ci-pipeline Specification

## Purpose
The continuous integration and release automation contract: every push to main and every pull request is verified on ubuntu, macos and windows runners with the full gate and calibrated budgets, and release tags produce the three single-file binaries (osx-arm64, win-x64, linux-x64) attached to the GitHub release.

## Requirements

### Requirement: Per-commit verification matrix

CI SHALL run a verification gate whose job set is decided by the event. A pull request SHALL run the
plan, the changed-path decision, the changelog fragment guard, the platform-independent documentation
and API checks, and build plus tests on `ubuntu-latest` only: a pull request SHALL NOT run macOS or
Windows verify legs, the Windows PowerShell twin, the dedicated perf leg, or CodeQL. A pull request
whose changed paths are all prose SHALL instead run the documentation profile on one runner, because
such a change can break neither the build nor a test. The profile SHALL be chosen by a committed
script with a self-test, deny-by-default: only `docs/`, `adr/`, `openspec/` and root-level Markdown
qualify, and anything else — any change under `.github/workflows/`, either side of a rename, and any
path outside those trees — SHALL select the full gate. A profile that cannot be read SHALL select the
full gate.

A `merge_group` run SHALL run the full test gate on `ubuntu-latest` only and the tree checks, and
SHALL skip the work the pull request already proved: the changed-path decision, the changelog
fragment guard, the platform-independent checks, the documentation profile, the Windows PowerShell
twin and the dedicated perf leg SHALL NOT run. A prose-only batch SHALL therefore run the plan and
the tree checks, because its pull request already ran the documentation profile.

A push to `main` and the nightly `schedule` run SHALL run the full matrix: build and tests on
ubuntu, macos and windows with every shard, the platform-independent checks once on Linux, the
Windows PowerShell twin, and the dedicated perf leg. A manual dispatch SHALL run the same matrix and
the PowerShell twin, with the perf leg left to the nightly. The push to `main` and the nightly SHALL
be the events on which the three-OS coverage is obtained; a pull request and a merge group SHALL NOT
run macOS or Windows verify legs. A push to `main` SHALL run the CodeQL workflow, and the weekly
CodeQL schedule SHALL keep analyzing `main`.

The checks whose result does not depend on the operating system — formatting, the generated
documentation regions, the documentation link, anchor, redirect and navigation check
(`scripts/docs-check --check`), markdownlint, the public API, `openspec validate --all --strict`, the
scratch-path and spec-archive checks, the docs-tree guard (`scripts/no-docs-references.sh`) and the
packaging-file assertion — SHALL run once in a single
Linux job that the aggregate gate requires; an OS leg SHALL NOT repeat them. Each OS leg SHALL keep
the checks whose result is a property of that machine: build, test, publish, the startup budget and
the artifact proofs.

The tree checks — ADR numbering, the spec-archive rule, the generated documentation regions, the
documentation link, anchor, redirect and navigation check, the docs-tree guard, and markdownlint —
SHALL run in one job on
a `merge_group` run, judged against the batch's base commit. The batch is the first tree on which two
pull requests meet, and each pull request proved only its own tree against its own base, so only the
queue can see a rule both of them break together. The same command list SHALL be what the push to
`main` runs, so the two triggers cannot drift. The job SHALL need no build and no test, and it SHALL
take the batch's full history and its release tags, because the spec-archive check diffs the batch's
range and the version region is derived from the tags.

Because a docs-only push skips the matrix and a code pull request never runs the documentation
profile, the docs-tree guard SHALL be part of the platform checks **and** of the shared tree-check
command list: a test that reads the docs tree is red on the pull request that adds it, and the push
that lands it cannot carry the failure unseen.

Pushes that touch only documentation MAY skip the matrix but SHALL still run the documentation lint
and the documentation link, anchor, redirect and navigation check.

#### Scenario: A pull request runs the fast gate
- **GIVEN** a pull request that changes source code
- **WHEN** the pull request's CI run completes
- **THEN** the plan, the changed-path decision, the changelog guard, the platform checks and the Ubuntu verify shards ran, and no macOS, Windows, PowerShell, perf or CodeQL job ran

#### Scenario: A merge group runs the Ubuntu test gate only
- **GIVEN** a `merge_group` run for a batch that contains code
- **WHEN** all its jobs complete
- **THEN** the plan, the tree checks and the Ubuntu verify shards ran, and the changed-path decision, changelog guard, platform checks, docs profile, PowerShell twin, perf leg and macOS and Windows legs did not run

#### Scenario: A prose-only batch runs the plan only
- **GIVEN** a `merge_group` run whose changed paths are all prose
- **WHEN** its CI run completes
- **THEN** only the plan ran a profile job, the tree checks ran beside it and the aggregate check ran, because the pull request already ran the documentation profile

#### Scenario: A merge group runs the tree checks against the batch's base
- **GIVEN** a `merge_group` run for a batch
- **WHEN** all its jobs complete
- **THEN** the tree checks ran the shared command list against the batch's base commit, with the batch's full history and the release tags

#### Scenario: A duplicate ADR number in a batch is rejected
- **GIVEN** two pull requests in the same batch that each took the same free ADR number
- **WHEN** the merge group's tree checks run on the combined head
- **THEN** ADR numbering fails and names both files, the aggregate gate is red, and the queue ejects the batch

#### Scenario: A push to main runs the full matrix
- **GIVEN** a push to `main` that is not prose-only
- **WHEN** its CI run completes
- **THEN** the Ubuntu, macOS and Windows verify legs, the platform checks, the PowerShell twin and the perf leg ran, and the CodeQL workflow ran for the same push

#### Scenario: The nightly runs the full matrix
- **GIVEN** the nightly `schedule` run
- **WHEN** it completes
- **THEN** every OS leg ran, and the PowerShell twin and the perf leg are part of the run's verdict

#### Scenario: Green matrix on a clean push
- **GIVEN** a push that satisfies the gate
- **WHEN** the CI matrix runs
- **THEN** all three runner jobs complete green

#### Scenario: A failing gate turns CI red
- **GIVEN** a push whose tests fail
- **WHEN** the CI matrix runs
- **THEN** the affected runner job fails and the run is red

#### Scenario: Docs-only push still lints
- **GIVEN** a push to `main` that touches only documentation
- **WHEN** the docs-lint workflow runs
- **THEN** the documentation lint completes green, and a lint violation would fail the workflow

#### Scenario: A proposal pull request runs the docs profile
- **GIVEN** a pull request whose changed paths are all under `openspec/changes/<name>/`
- **WHEN** CI runs for the pull request
- **THEN** the docs profile runs `openspec validate --all --strict` and no build, publish or test job runs

#### Scenario: Prose beside code still runs the full matrix
- **GIVEN** a pull request that changes `docs/dev/guide.md` and `src/Lunate.Tui/Interaction/KeyBinding.cs`
- **WHEN** CI decides the profile from the changed paths
- **THEN** the full gate runs, with its build and test legs on `ubuntu-latest`

#### Scenario: A workflow change is never prose
- **GIVEN** a pull request that changes `.github/workflows/ci.yml`
- **WHEN** CI decides the profile from the changed paths
- **THEN** the full gate runs, so a change to the gate cannot reduce the gate that checks it

#### Scenario: Platform checks run once
- **GIVEN** a pull request that selects the full gate
- **WHEN** the run completes
- **THEN** the formatting, docs, public-API and OpenSpec checks ran in exactly one job, and each OS leg ran them zero times

#### Scenario: A test that reads the docs tree is red before review
- **GIVEN** a pull request that adds a test which opens a file under `docs/`
- **WHEN** the pull request's full gate runs
- **THEN** the platform checks fail naming the test's line, so the read is caught before review and
  again by the tree checks on the push to `main`

#### Scenario: A broken documentation link cannot land

- **GIVEN** a documentation page whose relative link or `#anchor` does not resolve
- **WHEN** the full gate or the documentation profile runs, on a pull request or on a documentation-only push to `main`
- **THEN** `scripts/docs-check --check` fails, names the page and the link, and the run is red

### Requirement: Budget enforcement in CI
CI SHALL fail when a performance budget is exceeded, and SHALL run the gate self-tests so a broken budget check is detected.

#### Scenario: Budget breach turns the gate red in CI
- **GIVEN** the CI matrix job runs `scripts/gate-tests.sh`
- **WHEN** the budget check is broken (a breach is not detected)
- **THEN** the job fails

### Requirement: Windows verification twin
The windows runner SHALL run `scripts/verify.ps1` in addition to the bash gate.

#### Scenario: Windows twin green
- **GIVEN** the windows runner
- **WHEN** `scripts/verify.ps1` runs
- **THEN** it completes with `verify: OK`

### Requirement: Release artifacts

On `v*` tags, CI SHALL run the full verification gate before publishing and SHALL fail when the tag does not match the version in `Directory.Build.props`; it SHALL publish self-contained single-file binaries for osx-arm64, win-x64 and linux-x64, attach one archive per target to the GitHub release, attach the framework-dependent `dotnet tool` package and the extension package the same tag publishes, and attach `SHA256SUMS` covering every attached asset. A manual dispatch SHALL produce the same artifacts as workflow artifacts without publishing either package.

#### Scenario: Tag produces three artifacts

- **GIVEN** a `v*` tag
- **WHEN** the release workflow runs
- **THEN** the GitHub release carries one archive for each of the three target RIDs

#### Scenario: Dry run without a release

- **GIVEN** a manual workflow dispatch in dry-run mode
- **WHEN** the release workflow runs
- **THEN** the three target artifacts and both packages are produced and uploaded as workflow artifacts

#### Scenario: Tag and version mismatch fails before publishing

- **GIVEN** a tag whose version does not match `<Version>` in `Directory.Build.props`
- **WHEN** the release workflow runs
- **THEN** the gate job fails and nothing is published

#### Scenario: Archives ship with checksums

- **GIVEN** a `v*` tag that passes the gate
- **WHEN** the release completes
- **THEN** `SHA256SUMS` is attached next to the archives, covers every other attached asset, and matches them

#### Scenario: The tool package ships with the release

- **GIVEN** a `v*` tag that passes the gate
- **WHEN** the release completes
- **THEN** the `lunate` `.nupkg` is attached next to the archives

#### Scenario: The extension package ships with the release

- **GIVEN** a `v*` tag that passes the gate
- **WHEN** the release completes
- **THEN** the `Lunate.Extensions.Subagents` `.nupkg` is attached next to the archives and that version was pushed to nuget.org

### Requirement: Code scanning
A CodeQL workflow SHALL analyze the C# code on pushes to `main` and a weekly schedule, uploading results to GitHub code scanning. It SHALL NOT run on pull requests: the push to `main` and the weekly schedule are the events that own the three-OS and code-scanning coverage, and a pull request does not duplicate them.

#### Scenario: Analysis on a clean change
- **GIVEN** a push to `main` that satisfies the gate
- **WHEN** the CodeQL workflow runs
- **THEN** the analysis completes green and results are uploaded to code scanning

#### Scenario: A pull request does not run code scanning
- **GIVEN** a pull request that changes source code
- **WHEN** the pull request's checks run
- **THEN** no CodeQL analysis job runs, because the push to `main` and the weekly schedule own it

### Requirement: SDK pinning

The CI, CodeQL and release workflows SHALL install the .NET SDK version pinned in `global.json`. The pin SHALL be one exact version — `rollForward: disable` and `allowPrerelease: false` — so a patch that Microsoft has staged but not published cannot change what the gate installs; the pinned SDK also decides the runtime bundled into the self-contained binary, so the version is a product decision, not only a build input. A newer patch SHALL arrive as a Dependabot pull request against `global.json` (the `dotnet-sdk` ecosystem, weekly), which runs the full gate before merge; when a newer feature band carries fixes that affect the gate (for example test-runner exit behaviour on Windows), the pin SHALL move in its own change with the rationale recorded. A bump that changes which targeting packs the SDK bundles for the product's framework SHALL land in one change together with the gate's offline pack resolution, because such a bump is red on the operating systems the merge queue does not run, and the Dependabot configuration SHALL record that coupling next to the pin it bumps.

#### Scenario: Workflows resolve the pinned SDK

- **GIVEN** a workflow run on any runner
- **WHEN** the SDK is set up
- **THEN** the installed version is the one pinned in `global.json`

#### Scenario: A staged-but-unpublished patch cannot change the install

- **GIVEN** setup metadata that advertises a patch the download feed does not serve
- **WHEN** a workflow sets up the SDK
- **THEN** the pinned version is installed and the run continues, instead of resolving the phantom patch and failing

#### Scenario: A published patch arrives as a reviewed pull request

- **GIVEN** a newer patch of the pinned feature band published on the feed
- **WHEN** Dependabot's weekly run completes
- **THEN** one pull request bumps `global.json`, and the full gate runs on it before anyone merges

#### Scenario: A gate-relevant band fix moves the pin

- **GIVEN** an SDK feature band newer than the pin carrying a fix that affects gate behaviour
- **WHEN** the pin is bumped
- **THEN** the change records the measured before/after and the workflows install the new version

#### Scenario: A bump that changes the bundled packs is not merged alone

- **GIVEN** an SDK bump whose SDK no longer bundles the targeting packs of the framework the product
  and its fixtures target
- **WHEN** the bump is proposed
- **THEN** it lands in the same change as the gate's offline pack resolution, and the Dependabot
  configuration next to the pin records that the two belong together

### Requirement: Captured gate steps end with their commands

The gate scripts SHALL capture step output without depending on pipe EOF — writing to a file and printing it when the command ends — so a captured step ends when its command ends, even when build workers that inherited its output outlive it (MSBuild node-reuse workers and the compiler server do, by design). The zero-test guard SHALL parse the same captured output.

#### Scenario: A captured test step ends with its test run

- **GIVEN** the gate runs a captured test step on Windows
- **WHEN** the test run completes
- **THEN** the step ends without waiting for surviving build workers, and the gate's test totals and coverage are unchanged

#### Scenario: The capture still guards discovery

- **GIVEN** a test run whose output lacks a non-zero MTP summary
- **WHEN** the captured step completes
- **THEN** the gate fails with the discovery-regression error

### Requirement: Per-pull-request changelog fragments

A pull request SHALL record each release-note line in its own fragment at `changes/<pull-request-number>.md` and SHALL NOT edit `CHANGELOG.md`; only the release pull request assembles the changelog. A fragment SHALL name exactly one heading the release can file it under — `### Added`, `### Changed` or `### Fixed` — followed by one or more Markdown bullets in the changelog's shape, each ending with a reference such as `(#1234)` (the issue or pull request the line belongs to). A fragment whose file name is not a decimal pull-request number, whose heading is not one of the three accepted headings, whose body carries text outside the heading and its bullets, or whose bullets carry no reference SHALL be reported as malformed. Two fragment files that resolve to the same pull-request number SHALL be reported as a duplicate, naming both files.

`changes/README.md` SHALL state the rule: a pull request writes its fragment and never the changelog.

The gate SHALL be inert while `changes/README.md` does not exist on the pull request's base branch,
because the pull request that introduces the directory is the last direct changelog edit and would
otherwise be refused with nowhere else to write the line. Once the directory is on the base, the
rule is live for every later pull request.

#### Scenario: A fragment-only pull request passes

- **GIVEN** a pull request whose only prose change is `changes/1234.md`
- **WHEN** the gate runs
- **THEN** the changelog guard passes, and the fragment-only change selects the documentation profile

#### Scenario: The pull request that introduces the directory is not blocked

- **GIVEN** a pull request whose base does not yet contain `changes/README.md`, and which edits `CHANGELOG.md` to seed the generated region
- **WHEN** the changelog gate runs
- **THEN** it reports the rule as inert and succeeds, so the change that makes the rule live can merge

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

### Requirement: Aggregate merge gate

The CI workflow SHALL expose a single aggregate status check (`ci-ok`) that succeeds only when every
verification job the event and profile require has completed successfully and every job they do not
require was skipped, so renaming, adding, or removing a matrix leg cannot silently drop required
coverage. The repository ruleset SHALL require the aggregate check for `main` — a one-time maintainer
configuration outside the workflow's control. The aggregate SHALL report a result on every pull
request: the workflow SHALL NOT apply path filters to pull-request events. A required job that is
skipped or cancelled SHALL NOT count as success, a job that ran although the event and profile did
not require it SHALL fail the aggregate, and a run whose profile cannot be read SHALL fail rather
than pass.

The required job set SHALL follow the event. A pull request SHALL require `plan`, `changes`, the
changelog fragment guard, the platform checks (full profile) or the documentation profile (docs
profile), and, on the full profile, the Ubuntu verify legs; it SHALL NOT require macOS, Windows, the
PowerShell twin or the perf leg, and their skip SHALL be a pass. A `merge_group` run SHALL require
`plan`, the tree checks and the Ubuntu verify legs (or `plan` and the tree checks for a docs batch),
and SHALL fail when the tree checks did not succeed, or when `changes`, the changelog guard, the
platform checks, the docs profile, the PowerShell twin, the perf leg or a macOS or Windows verify leg
ran, because the pull request already proved them. A push to `main`, the
nightly and a manual dispatch SHALL require the full matrix, `changes`, the platform checks, the
PowerShell twin and the docs profile skipped; the nightly SHALL also require the perf leg and a
manual dispatch SHALL require it skipped; a missing macOS leg SHALL fail them.

For a pull request, the aggregate SHALL decide from the pull request's live draft state and the live
head commit's check runs read through the GitHub API, not from the triggering run's frozen `draft`
payload or its own `needs` results. It SHALL take, for each required job, the newest attempt across
all runs on that live head. A run whose newest plan check run was skipped is a draft-payload run and
SHALL NOT count; a cancelled attempt SHALL NOT count when any other attempt of that job exists, and
SHALL never be a pass when it is the only evidence. While the live gate's required jobs are still
queued or running, the aggregate SHALL fail with `gate still running` and SHALL name those jobs; it
SHALL NOT report green and SHALL NOT turn this run's payload into a red. The live head SHALL carry a
check run for every leg of the plan's verify matrix: the aggregate SHALL take the matrix's shard
count from the legs that ran, and SHALL fail, naming each missing leg, when a shard or a whole OS leg
the plan requires has no check run at all, so a missing leg cannot count as another shard's pass.
Because the plan puts Ubuntu legs only in a pull-request matrix, the live decision SHALL require the
Ubuntu legs and SHALL NOT require a Windows or macOS leg. Rerunning an older or draft-payload run
SHALL therefore yield the live head's verdict: green when the head's ready gate passed and carried
every required leg, red when it failed or a required leg is absent. `merge_group`, push, `schedule`
and `workflow_dispatch` decisions SHALL keep the payload decision.

Five jobs are conditional. The tree checks SHALL run on a `merge_group` run and SHALL be skipped on
every other event: a skip SHALL be a pass outside a merge group, and a merge group in which the job
did not succeed SHALL fail the aggregate. The Windows PowerShell twin SHALL run on a push to `main`,
the nightly and a manual dispatch, and its skip on a pull request or a merge group SHALL be a pass.
The changelog fragment gate SHALL run on every non-draft pull request regardless of profile, and
SHALL fail when the pull request is not the release pull request and its changed paths include
`CHANGELOG.md`; its skip SHALL count as success outside a pull-request event, and a pull-request run
in which it did not succeed SHALL fail the aggregate. The `changes` job SHALL run on a pull request and on a push,
nightly or manual dispatch, and its skip SHALL be a pass only in a merge group. The dedicated perf
leg SHALL run on the nightly and on a push to `main`; it SHALL feed the nightly's verdict and SHALL
NOT feed a push's verdict, because a shared runner varies by a few percent.

#### Scenario: Aggregate green when every job passes

- **GIVEN** a pull request whose verification jobs all succeed
- **WHEN** the aggregate check runs
- **THEN** it completes green

#### Scenario: Aggregate red when a job fails or is skipped

- **GIVEN** a pull request where any required verification job fails or is skipped
- **WHEN** the aggregate check runs
- **THEN** it fails, so the merge stays blocked

#### Scenario: A pull request does not require the Windows or macOS legs

- **GIVEN** a pull request whose plan put only Ubuntu legs in its verify matrix
- **WHEN** the aggregate check runs
- **THEN** it completes green without a Windows or macOS leg and without the PowerShell twin

#### Scenario: A path-filtered leg skipped legitimately

- **GIVEN** a pull request that touches only Linux-only code
- **WHEN** the PowerShell leg is skipped because a pull request does not run the Windows twin
- **THEN** the aggregate completes green

#### Scenario: A path-filtered leg skipped illegitimately

- **GIVEN** a push to `main` that changes a PowerShell script
- **WHEN** the PowerShell leg is skipped or cancelled although the decision says it is required
- **THEN** the aggregate fails

#### Scenario: A Windows leg on a pull request is not required but is not a failure to skip

- **GIVEN** a manual dispatch run on the same live head that carried the full matrix
- **WHEN** the pull-request aggregate reads the head
- **THEN** the Windows and macOS legs are permitted, because the pull request's own matrix did not require them

#### Scenario: A merge group rejects the work the pull request proved

- **GIVEN** a `merge_group` run in which `changes`, the changelog guard, the platform checks, the docs profile or the PowerShell twin ran
- **WHEN** the aggregate check runs
- **THEN** it fails, naming the job that should have been skipped

#### Scenario: A merge group whose tree checks failed is red

- **GIVEN** a `merge_group` run whose tree checks reported a duplicate ADR number or a hand-edited spec
- **WHEN** the aggregate check runs
- **THEN** it fails and names the tree checks, so the queue rejects the batch

#### Scenario: A merge group whose tree checks were skipped is red
- **GIVEN** a `merge_group` run in which the tree-checks job was skipped
- **WHEN** the aggregate check runs
- **THEN** it fails, because the batch's own tree was never judged

#### Scenario: A merge group with a missing Ubuntu shard is red

- **GIVEN** a `merge_group` run whose Ubuntu verify matrix is missing a shard
- **WHEN** the aggregate check runs
- **THEN** it fails and names the missing shard

#### Scenario: A push without macOS legs is red

- **GIVEN** a push to `main` whose verify matrix contains no macOS leg
- **WHEN** the aggregate check runs
- **THEN** it fails, so the event that owns the three-OS coverage cannot silently drop an OS

#### Scenario: A nightly without its perf leg is red

- **GIVEN** the nightly run in which the dedicated perf leg did not succeed
- **WHEN** the aggregate check runs
- **THEN** it fails

#### Scenario: A push whose perf leg is red is not gated on it

- **GIVEN** a push to `main` whose performance measurement failed
- **WHEN** the aggregate check runs
- **THEN** the perf result does not feed the push's verdict

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

#### Scenario: A rerun of a draft payload does not override a passed ready run (#815)

- **GIVEN** a ready run on the live head whose required jobs passed, and a newer rerun of an earlier draft-payload run whose plan skipped
- **WHEN** the rerun's aggregate check runs
- **THEN** it reads the live head, ignores the draft-payload attempt, and completes green

#### Scenario: A rerun of a draft payload does not override a failed ready run (#815)

- **GIVEN** a ready run on the live head whose required jobs failed, and a newer rerun of an earlier draft-payload run
- **WHEN** the rerun's aggregate check runs
- **THEN** it reads the live head, ignores the draft-payload attempt, and fails naming the failed required job

#### Scenario: Cancelled legs do not override the ready run (#816)

- **GIVEN** a ready run whose required legs were cancelled, a draft run beside it, and a later ready run on the same live head that passed
- **WHEN** the aggregate check runs
- **THEN** it ignores the cancelled and draft-payload attempts and completes green

#### Scenario: A re-executed ready run is judged by its newest attempt

- **GIVEN** a ready run on the live head whose required jobs passed, and a newer rerun of an older ready run that re-executed its jobs and passed as well
- **WHEN** the aggregate check runs
- **THEN** it takes the newest attempt of each required job and completes green

#### Scenario: A failed re-execution of a ready run is red

- **GIVEN** a ready run on the live head whose required jobs failed, and a newer rerun of an older ready run that re-executed its jobs and failed again
- **WHEN** the aggregate check runs
- **THEN** it takes the newest attempt of each required job and fails, naming the failed required job

#### Scenario: A ready gate with running jobs says so

- **GIVEN** a live ready pull request whose required jobs include a check run that is queued or in progress
- **WHEN** the aggregate check runs
- **THEN** it fails with `gate still running`, names those jobs, and neither reports green nor derives a red from its own payload

#### Scenario: A missing required matrix leg is red

- **GIVEN** a live ready pull request whose gate passed every leg it ran, but one shard of the plan's verify matrix has no check run on the head at all
- **WHEN** the aggregate check runs
- **THEN** it fails and names the missing leg instead of completing green on the shards that did run

#### Scenario: A whole missing OS leg is red

- **GIVEN** a live ready pull request whose gate ran no Ubuntu verify leg at all
- **WHEN** the aggregate check runs
- **THEN** it fails and names the missing Ubuntu legs, so a matrix that shrank silently cannot pass

#### Scenario: A live draft is red

- **GIVEN** a pull request that is currently a draft, even though an earlier run on the same head passed
- **WHEN** the aggregate check runs
- **THEN** it fails because the head is a draft, from the pull request's live state

#### Scenario: A merge group keeps its payload decision

- **GIVEN** a `merge_group` run
- **WHEN** the aggregate check runs
- **THEN** it decides from that run's required jobs exactly as before, not through the pull-request API path

### Requirement: Fixture restores in the gate are bounded and honest

The test suite's fixture restores SHALL restore XML solutions (`.slnx`) only and SHALL refuse a
legacy `.sln` by name, because MSBuild's legacy parser restores a solution by generating a metaproject
on disk under the temp directory while an XML solution is expanded in memory — the gate SHALL NOT depend
on a generated file it does not control. A fixture restore SHALL run at most one at a time across every
test process on the machine, on one MSBuild node, with every folder the child can write to inside the
fixture it restores. A restore failure SHALL NOT be reported as silence that did not happen: a child
stream that has not closed within the drain budget SHALL be reported as a stream that had not closed,
and a failed child whose output carries no error SHALL be reported with the last MSBuild target it
entered, so a zero-error solution failure still says where the restore gave up.

#### Scenario: A fixture solution is an XML solution

- **GIVEN** the fixtures under `tests/fixtures/solutions/`
- **WHEN** the gate restores one
- **THEN** the solution file is `.slnx`, and a `.sln` passed to the restore helper is refused with a
  message naming the XML replacement and the generated metaproject it avoids

#### Scenario: One restore at a time, on one node

- **GIVEN** several test assemblies restoring fixtures at once on a loaded machine
- **WHEN** the restores run
- **THEN** at most one fixture restore child is running at a time, it runs with a single MSBuild node
  and no build server, and its temp directory, package folder, CLI home and HTTP cache are inside the
  fixture

#### Scenario: A stalled stream is not a silent child

- **GIVEN** a restore child that exited but left a stream open
- **WHEN** the drain budget expires
- **THEN** the failure report says the stream had not closed and that its output is missing, and never
  says the child printed nothing

#### Scenario: A zero-error failure names the target it was in

- **GIVEN** a solution restore that failed after entering `ValidateSolutionConfiguration` and printed
  `Build FAILED. 0 Warning(s) 0 Error(s)` with no per-project restore line
- **WHEN** the failure is reported
- **THEN** the report carries the child's own output and names `ValidateSolutionConfiguration` as the
  last target it entered

### Requirement: Run concurrency is scoped to one obligation

CI SHALL cancel a run only when a newer run of the same obligation supersedes it: `cancel-in-progress` SHALL be true only for `pull_request` runs. A draft pull-request run and a ready pull-request run SHALL use different concurrency groups, so a draft run SHALL NOT cancel a ready run and a ready run SHALL NOT cancel a draft run. Within a group, a newer run of the same obligation SHALL supersede the older one. A push to `main`, a `v*` tag push, a `merge_group` run, a `workflow_dispatch` run and the nightly `schedule` run SHALL NOT be cancelled by the concurrency setting. The nightly run SHALL use a concurrency group distinct from the push run on the same branch, so neither obligation can cancel the other. Advisory workflows that also run on `main` (`docs-lint`, `pack`) SHALL apply the same rule; a workflow without a `pull_request` trigger therefore cancels nothing. A committed self-test SHALL evaluate the effective group and cancellation decision per event for `ci.yml`, `docs-lint.yml` and `pack.yml`, and SHALL fail when a workflow regresses.

#### Scenario: A push to main and the nightly coexist

- **GIVEN** a `CI [push]` run on `main` and a nightly `schedule` run on the same branch
- **WHEN** the nightly starts while the push run is in flight
- **THEN** the two runs are in different concurrency groups, neither is cancelled, and both reach a completed conclusion

#### Scenario: Only a pull request supersedes its own older run

- **GIVEN** two runs of the same pull request with the same draft state, the second started while the first is in flight
- **WHEN** the second run enters its concurrency group
- **THEN** the first run is cancelled and the second runs the gate

#### Scenario: A draft run never cancels a ready run

- **GIVEN** a ready pull-request run in flight and a newer draft pull-request run for the same pull request
- **WHEN** the draft run enters its concurrency group
- **THEN** its group differs from the ready run's group, so the ready run is not cancelled

#### Scenario: A tag push is never cancelled

- **GIVEN** a `v*` tag push run in flight
- **WHEN** any other CI event starts
- **THEN** the tag run's group does not match the newcomer's and the tag run completes

#### Scenario: A merge group is never cancelled

- **GIVEN** a `merge_group` run in flight
- **WHEN** another run enters CI
- **THEN** the merge group's gate is not cancelled

#### Scenario: A push to main is never cancelled by a newer push

- **GIVEN** two pushes to `main`, the second arriving while the first run is in flight
- **WHEN** the second push run starts
- **THEN** the first push run is not cancelled; the commits it covers keep their verdict

#### Scenario: Advisory workflows on main never cancel their own runs

- **GIVEN** an in-flight `docs-lint` or `pack` run for a push to `main`
- **WHEN** another run of that workflow starts
- **THEN** the in-flight run is not cancelled, because only a pull-request run may supersede its older
  run

#### Scenario: A regression in the expression is caught

- **GIVEN** a workflow whose `cancel-in-progress` becomes true for a `push`, tag, `merge_group` or
  `schedule` event, or whose nightly group equals the push group, or whose draft and ready
  pull-request groups coincide
- **WHEN** `scripts/ci-ok.sh --self-test` runs
- **THEN** it fails and names the workflow and event that broke the rule

### Requirement: Main-run failure reporting

A red `push` run on `main` and a red nightly `schedule` run SHALL each report themselves so a failure
cannot sit unnoticed: the CI workflow SHALL run the same reporter for both events, and it SHALL read the
run's jobs and, for every job whose conclusion is red, open a `kind/bug` issue naming the failing job
and the run link, or comment on the already-open issue for that job instead of filing a duplicate. The
reporter SHALL key the duplicate check on the job's title, so a week of red runs for the same failing
job is one open card. A green run SHALL file nothing.

A job that never started is not a red job of the repository's build or tests: a job whose conclusion is
`cancelled`, whose `runner_id` is `0` and whose `steps` list is present and empty was never acquired by a
runner, so the reporter SHALL treat it as not red, file no card and post no comment for it on the
strength of this requirement, and SHALL name it, with the run link, in the run's own log and job summary,
so the red run is still visible even when another job of the same run files. All three conditions SHALL
hold, because each alone is ambiguous: a job cancelled by a superseding run also has no steps, and a
started job can still report `runner_id: 0`. The rule SHALL NOT cover a job that ran and did not pass: a
`cancelled` job that reached at least one step, a `failure`, a `timed_out` and every other red conclusion
SHALL still file, and a job whose `steps` list or `runner_id` is missing or unreadable SHALL be treated as
red, so the rule fails closed instead of passing on a document it cannot read.

The two jobs whose result is derived from the others' results — the `ci-ok` aggregate and this
reporter — SHALL NOT be opened as a card or commented on when the run also holds a red job that ran and is
neither of those, because that job's card already names the failure the derived job restates; the reporter
SHALL name the derived red it did not file, with the run link, in the run's own log instead. When the run's
only red jobs that ran are the derived ones, the aggregate's card SHALL still be filed, because then
nothing else in the run speaks for the gate. A derived job that never started keeps the never-started rule
above: it is named, not filed, like any other abandoned job.

A run whose red jobs never reached a machine SHALL be a capacity red, not a code red: GitHub cancels a
job when it never acquires a hosted runner for it, so nothing the repository builds or tests has run and
no step exists to triage. The two jobs whose result is derived from the others' — the `ci-ok` aggregate
and this reporter — SHALL be allowed beside the abandoned jobs, because they fail exactly because a leg
did not succeed; they SHALL NOT be named as abandoned jobs themselves, and no card and no comment SHALL
be produced for them as part of a capacity red. One red job that ran and is neither of those SHALL make
the run an ordinary red.

The reporter SHALL NOT open a card for a capacity red, in any attempt: it SHALL record the abandoned jobs
and what it saw in the run's job summary, SHALL fail while the retry budget is unspent, because the retry
reruns only failed jobs and the reporter has to be one of them to observe the retry, and SHALL succeed at
the budget with the summary naming the rerun as the remedy. The pipeline SHALL retry a capacity red's
failed jobs once, from a workflow triggered when the run completes, because a run cannot rerun itself
while it is in progress, and the retry SHALL be limited to a `push` to `main` and the nightly, the events
that run the full matrix and that this requirement reports on. The budget SHALL be one retry: a run at
its second attempt SHALL NOT be retried again, so a runner shortage costs one extra attempt rather than a
loop. A red job that ran SHALL NEVER be retried this way, so no failure that executed is retried away.

The issue SHALL be placed in the lowest-numbered open milestone whose title is a version
(`MAJOR.MINOR.PATCH`), read from the GitHub API so the rule works on a GitHub runner from repository
state alone. A release closes its milestone, so the next lowest open version becomes the target without
a code change, and a `backlog` or any other non-version milestone SHALL never be selected. When no
version milestone is open, the card SHALL still be filed and the reporter SHALL say it filed the card
without a milestone.

The reporter SHALL add `area/tui` or `area/core` from the failing test's project only when the project,
its namespace and the failing test decide it: a test in the `Lunate.Tui` project is `area/tui`, a test
in the `Lunate.Coding` project is `area/tui` when its test source names the `Lunate.Tui` namespace and
`area/core` otherwise, and a card whose red job names no failing test, names a test in another project,
or names failing tests that disagree SHALL carry neither area label instead of a guess. The reporter
SHALL pass an area label only when the repository's label set has it, and SHALL file the card unlabelled
when it does not.

#### Scenario: A red push run files a card naming the job

- **GIVEN** a `push` run on `main` in which `verify (macos-latest, 1/2)` failed
- **WHEN** the reporter runs
- **THEN** it opens a `kind/bug` issue whose title names `verify (macos-latest, 1/2)` and whose body carries the run link

#### Scenario: A red nightly files a card naming the job

- **GIVEN** a nightly run in which `verify (windows-latest, 2/4)` failed
- **WHEN** the reporter runs
- **THEN** it opens a `kind/bug` issue whose title names `verify (windows-latest, 2/4)` and whose body carries the run link

#### Scenario: A job no runner ever acquired files nothing (#922)

- **GIVEN** a `push` run on `main` whose `perf (macos-latest)` job concluded `cancelled` with `runner_id: 0` and an empty `steps` list, because the hosted runner was never acquired after multiple attempts
- **WHEN** the reporter runs
- **THEN** it files no card and posts no comment for that job, and the run's log names the job and the run link

#### Scenario: A run whose only red jobs never started files nothing

- **GIVEN** a push run in which every red job concluded `cancelled` with `runner_id: 0` and an empty `steps` list
- **WHEN** the reporter runs
- **THEN** it creates and comments on no issue, and the run's log says the jobs never got a runner and that a rerun is the remedy

#### Scenario: A cancellation that started stays red

- **GIVEN** a nightly run whose `verify (macos-latest, 2/2)` job concluded `cancelled` after reaching its first step, and a push run whose `perf (macos-latest)` job concluded `cancelled` with no `steps` field at all
- **WHEN** the reporter runs on each
- **THEN** both file a card, because neither is evidence of a job that never started

#### Scenario: A cancellation that held a runner stays red

- **GIVEN** a job concluded `cancelled` with an empty `steps` list and `runner_id: 7`, as a superseding run leaves it, and a job concluded `cancelled` with an empty `steps` list and no `runner_id` at all
- **WHEN** the reporter runs on each
- **THEN** both file a card, because an empty step list alone is not evidence that no runner was acquired

#### Scenario: The aggregate is not filed beside the leg it restates (#1002)

- **GIVEN** a push run on `main` in which `verify-powershell (windows)` failed and `ci-ok` failed because that required leg did not succeed
- **WHEN** the reporter runs
- **THEN** it files exactly one card, naming `verify-powershell (windows)`, and the run's log names `ci-ok` together with the run link

#### Scenario: The aggregate's card is filed when it is the run's only red

- **GIVEN** a push run on `main` in which every leg the plan required succeeded and `ci-ok` concluded `failure` anyway
- **WHEN** the reporter runs
- **THEN** it files a card naming `ci-ok`, because nothing else in the run explains the gate's red

#### Scenario: A real failure beside an unstarted job still files

- **GIVEN** a push run in which `verify (macos-latest, 1/2)` concluded `cancelled` with `runner_id: 0` and an empty `steps` list and `verify (windows-latest, 2/4)` failed
- **WHEN** the reporter runs
- **THEN** it files exactly one card, naming `verify (windows-latest, 2/4)`, and the run's log names `verify (macos-latest, 1/2)` together with the run link

#### Scenario: A leg that never acquired a runner is retried, not reported

- **GIVEN** a `push` run on `main` in which `verify (macos-latest, 1/2)` and `perf (macos-latest)` were cancelled before any step started, and every job that ran succeeded
- **WHEN** the reporter runs at the run's first attempt
- **THEN** it opens no card, names the abandoned legs in the run's job summary, and the run's failed jobs are retried once

#### Scenario: A capacity red that survives the retry names the rerun and files nothing

- **GIVEN** the retry of that run in which the same legs were cancelled before any step started
- **WHEN** the reporter runs at the second attempt
- **THEN** it opens and comments on no card, and the run's job summary says the retry is spent and that a rerun is the remedy

#### Scenario: The derived jobs are never named as abandoned legs

- **GIVEN** a capacity red in which `ci-ok` and `red-main-issue` are red because the abandoned legs did not succeed
- **WHEN** the reporter runs
- **THEN** the run's job summary names neither job, and neither `CI failed: ci-ok` nor `CI failed: red-main-issue` is filed

#### Scenario: A leg that never started is named beside the card a run files

- **GIVEN** a red run in which `verify (macos-latest, 1/2)` was cancelled before any step started and `verify (ubuntu-latest, 2/4)` started a step and failed
- **WHEN** the reporter runs
- **THEN** it files the card for `verify (ubuntu-latest, 2/4)` and the run's job summary names `verify (macos-latest, 1/2)` with the run link

#### Scenario: A job that ran is never retried away

- **GIVEN** a red run in which at least one red job started a step and failed
- **WHEN** the reporter and the retry decision run
- **THEN** the run is not retried and that job's card is filed as before

#### Scenario: The card takes the lowest open version milestone

- **GIVEN** the open milestones `0.6.0`, `0.5.0` and `backlog`
- **WHEN** the reporter files a card
- **THEN** the card is placed in `0.5.0`, and after a release closes `0.5.0` the next card takes `0.6.0`

#### Scenario: A card is filed without a milestone when no version is open

- **GIVEN** only `backlog` is open
- **WHEN** the reporter files a card
- **THEN** the card is filed without a milestone and the run says so, rather than landing in `backlog`

#### Scenario: The area label follows the failing test's project

- **GIVEN** a red verify job whose log names `Lunate.Coding.Tests.InteractiveSessionThemeFileTests.An_unknown_token_warns_and_the_built_in_theme_stands`
- **WHEN** the reporter files the card
- **THEN** the card carries `kind/bug` and `area/tui`, and a card for a coding test whose source names no `Lunate.Tui` namespace carries `area/core`

#### Scenario: No area label when the project does not decide it

- **GIVEN** a red job that names no failing test, names a test in another project, or names failing tests that disagree
- **WHEN** the reporter files the card
- **THEN** the card carries `kind/bug` and no area label

#### Scenario: An open card is reused instead of duplicated

- **GIVEN** an open issue whose title already names the failing job
- **WHEN** the next red run's reporter runs
- **THEN** it comments on that issue and creates no second one

#### Scenario: A green nightly files nothing

- **GIVEN** a run in which every job succeeded
- **WHEN** the reporter runs, on a push or the nightly
- **THEN** it creates and comments on no issue

### Requirement: Fixture restores resolve the SDK's targeting packs offline

A fixture the gate restores SHALL NOT need a package source, and SHALL still restore when the pinned
SDK bundles targeting packs only for its own framework: the offline NuGet configuration the gate
writes into a fixture SHALL name each package source as cleared, and SHALL name the machine's NuGet
global packages folder as a fallback package folder, so a `net10.0` fixture restored by an SDK 11 that
ships only net11.0 packs resolves `Microsoft.NETCore.App.Ref`, `Microsoft.AspNetCore.App.Ref` and
`Microsoft.NETCore.App.Host.<rid>` from the packages the repository's own build already restored there.
The fallback folder SHALL be read-only for the restore child — the child writes its packages inside the
fixture — and SHALL be named only when it exists, because NuGet answers a missing fallback folder with
`NU1301` instead of falling through to the source list. A restore failure SHALL name the fallback folder
the configuration used, so the next reader can tell a missing cache from a broken fixture. Both offline
restores in the suite — the gate's fixture restore and the MSBL binding-order test's workspace restore —
SHALL be produced by one helper, so they cannot drift apart.

#### Scenario: A framework the SDK does not bundle still restores

- **GIVEN** a pinned SDK whose bundled targeting packs cover only its own framework, the repository's
  build has restored the packs for the product's framework into the machine's NuGet global packages
  folder, and a fixture that targets the product's framework
- **WHEN** the gate restores that fixture offline
- **THEN** the restore succeeds with no package source named, and the packs are resolved from the
  machine's global packages folder without being copied into it

#### Scenario: A missing machine cache is not a second confusing failure

- **GIVEN** a machine with no NuGet global packages folder
- **WHEN** the gate writes its offline configuration
- **THEN** the fallback folder is not named at all, so the child fails with the missing package instead
  of `NU1301`, and the failure report says which fallback folder the configuration would have used

#### Scenario: The two offline restores share one configuration

- **GIVEN** the gate's fixture restore and the MSBL binding-order test's workspace restore
- **WHEN** either writes its `NuGet.config`
- **THEN** both texts come from the same helper, name no network source, and name the same fallback
  folder

### Requirement: Docs-tree guard

`scripts/no-docs-references.sh` SHALL fail when the docs tree becomes a build input or a test's
subject: a project, props, targets or solution item that names a docs path; a test that resolves the
repository root and names a docs path; or a `File`/`Directory` call on a docs path. It SHALL accept a
docs reference that a test names without reading it — a value the test compares with or excludes from
a scan — only through a committed allowance that names the file and the exact reference, and it SHALL
print every allowance it applies, so an exception is never silent. Any other docs reference in that
file, and any read call on the allowed line, SHALL still fail. It SHALL also accept an in-memory
fixture path or a rendered value, printing that allowance, and SHALL fail when it scanned no test
source at all, because a scan that finds nothing cannot fail.

#### Scenario: A named but unread docs path is not a read
- **GIVEN** a test that resolves the repository root and names `docs/dev/spikes` only as a directory its scan excludes
- **WHEN** `scripts/no-docs-references.sh` runs
- **THEN** it prints the allowance and passes, because the reference is never opened

#### Scenario: A second docs path in the allowed file still fails
- **GIVEN** the same test file also names another docs path
- **WHEN** the guard runs
- **THEN** it fails naming that line, so the allowance cannot hide a read

#### Scenario: A read of the allowed reference fails
- **GIVEN** a file in which the allowed reference appears inside a `File` or `Directory` call
- **WHEN** the guard runs
- **THEN** it fails with the read message, because a read is a read whatever the allowance names

#### Scenario: The guard runs in the docs profile and the full gate
- **GIVEN** the repository verification gate runs with `--profile docs`, `--profile full` or `--profile platform`
- **WHEN** the gate reaches its docs checks
- **THEN** `scripts/no-docs-references.sh` runs and a read it reports turns the run red

### Requirement: One-command release

`scripts/changelog-assemble` SHALL provide a release mode,
`--release <version> [--date YYYY-MM-DD]`, that performs the whole release pull request edit in one
run against the repository root: it SHALL assemble the fragments into the unreleased section as
`--write` does, SHALL promote that section to `## [<version>] - <date>` with the generated marker
comments removed, SHALL leave a fresh empty `## [Unreleased]` section above the promoted one, SHALL
delete every fragment it consumed, and SHALL set `<Version>` in `Directory.Build.props` to the
released version. `--date` SHALL default to the current date in UTC and SHALL be spelled exactly
`YYYY-MM-DD`; a value in any other form SHALL be refused as a usage error before anything is written.
A successful run SHALL leave `--check` passing and SHALL change exactly three kinds of
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

#### Scenario: A date not spelled YYYY-MM-DD is refused

- **GIVEN** a tree with fragments
- **WHEN** `--release 0.5.0 --date 20261201` or `--date 2026-W49-1` runs
- **THEN** it fails as a usage error naming `YYYY-MM-DD`, and nothing is written

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

### Requirement: The release packs the decision model package

The release and the pack dry run SHALL pack `src/Lunate.Decisions.Model` beside the stable contract
pair. Its package id SHALL be `Lunate.Decisions.Model`, it SHALL carry the contract API version plus
the tag's prerelease suffix, and it SHALL appear in the pack list's expected file names and metadata
checks, so a release cannot publish `Lunate.Extensibility.Abstractions` whose declared dependency the
feed does not carry. The verify gate's contract-package roundtrip SHALL pack it into the feed the
template restores from, so a template built against packages proves the dependency resolves.

#### Scenario: A tag publishes the model package

- **GIVEN** a `v*` tag that passes the gate
- **WHEN** the release packs its packages
- **THEN** `Lunate.Decisions.Model.<contract-version>.nupkg` is built, metadata-checked and pushed
  to nuget.org before `Lunate.Extensibility.Abstractions`

#### Scenario: A pull request proves the pack list

- **GIVEN** a pull request that changes the release package set
- **WHEN** the pack dry run executes
- **THEN** it packs and metadata-checks `Lunate.Decisions.Model` and fails when the expected package
  file is missing

#### Scenario: The template restores the model from the feed

- **GIVEN** the verify gate's contract-package feed
- **WHEN** the packaged-extension template restores against it
- **THEN** the template's own `Lunate.Decisions.Model` reference and the dependent
  `Lunate.Extensibility.Abstractions` both resolve from the freshly packed feed, not from a released
  copy

### Requirement: Weight-aware verify shard partition

The test phase SHALL partition the solution's test projects across the shards by recorded wall time,
not by position in the project list. The unit of partition SHALL be one `lunate.slnx` test project:
both culture passes of a project SHALL run in the same shard, so every test project appears in exactly
one shard. The partition SHALL place projects largest recorded weight first, each into the
currently least-loaded shard, with a deterministic tie-break, so a heavy suite is not parked whole
beside several light projects. The process-bound half of the Coding suite SHALL be its own test
project, `Lunate.Coding.Process.Tests`, compiled from the Bash and Process test files, so one shard
never carries the whole slow suite.

The only input to the partition SHALL be `scripts/verify-shard-weights.tsv`: one recorded wall time per
test project, in seconds, for one culture pass, counted twice because both passes run in that
project's shard. The table SHALL also record the wall time of shard 1's non-test steps, which SHALL
pre-load shard 1's assignment, because that leg pays publish, the startup budget, the install and tool
roundtrips, the formatting, documentation and public-API checks on top of its tests. The partition
SHALL use the same table in every lane, and a shard SHALL compute its own assignment from it alone,
never from another shard, so the shards of a lane agree without coordination.

A test project with no row in the table SHALL be assigned the default weight, so adding a test project
never fails the gate; a row naming a project that is not in `lunate.slnx` SHALL fail
`scripts/gate-tests.sh`, so the table cannot drift away from the solution. Weight errors SHALL affect
the balance only: every test project SHALL be assigned to exactly one shard whatever the weights say.

The CI matrix SHALL run the Ubuntu and Windows lanes at the manual dispatch's shard count -- four
unless it picks another -- and SHALL keep the macOS lane at its cap of two shards, so macOS never gains
a third. At the Ubuntu and Windows count, no shard SHALL carry more than one third of its lane's summed
recorded weight, counting shard 1's non-test pre-load in both the shard and the lane. A dispatch that
picks fewer shards is a baseline measurement: below four the third cannot be met by any partition,
because two shards always leave one carrying at least half the lane. The macOS lane's acceptance is the
best wall time within two shards instead: both the position and the weight partition SHALL be measured
at two shards, and the measured busiest-shard wall times SHALL be recorded before and after, so the
acceptance is a measurement rather than a prediction. Its two-shard partition SHALL also be no heavier
-- in its busiest shard's summed recorded weight, and in the wall time its heaviest project predicts --
than the position partition it replaced, which is the gate's own comparison. At every shard count `n`
the busiest shard SHALL stay within the
longest-processing-time bound, `(4n-1)/(3n)` of the larger of the lane's fair share and its heaviest
project (shard 1's non-test pre-load included), because no partition can beat a single project heavier
than the fair share; at four shards that is 31.25 % when the fair share is the larger term, which is
stricter than the third. Every shard SHALL own at least one test project at every shard count the CI
matrix runs, because a shard that runs nothing is not a shard.

`scripts/gate-tests.sh` SHALL enforce the partition: for each shard count in use it SHALL check that
every test project, with both culture passes, is in exactly one shard, that no shard is empty, that
the partition is byte-identical across calls, and that both the third and the longest-processing-time
bound hold; it SHALL fail naming the shard and the weight when any of them does not. It SHALL require
the table's `#primary` row, so shard 1's non-test work is never planned as free. It SHALL read every
lane's shard count from the workflow and fail when the Ubuntu or Windows lane is below four, where the
third cannot hold, or when the macOS lane is not at its two-shard cap; it SHALL report the Ubuntu and
Windows pole against the third and the macOS two-shard pole against the position partition it replaced.

Every gate run SHALL record the wall time of each test project it runs, next to the per-shard record
`scripts/shard-timing.sh` already writes, one record per shard and culture pass so that a shard which
ran both passes keeps both. That recording SHALL also cover a pass that fails after its retry budget
and each pass of the opt-in pooled runner, because those runs executed their projects before the leg
exited. `scripts/shard-weights.sh weights --from <dir>` SHALL fold such records back into
`scripts/verify-shard-weights.tsv`, taking a project's slower recorded pass, so a real run refreshes
the weights instead of anyone estimating them.

#### Scenario: A heavy suite is split into projects instead of one shard carrying it whole

- **GIVEN** the Coding suite whose process-bound tests dominate the Windows lane
- **WHEN** `lunate.slnx` test projects are enumerated
- **THEN** the Bash and Process test files belong to `Lunate.Coding.Process.Tests`, a separate test project from `Lunate.Coding.Tests`, and each is planned as one project in one shard

#### Scenario: No shard exceeds a third on Ubuntu and Windows

- **GIVEN** the recorded weight table
- **WHEN** the four-shard partition the Ubuntu and Windows lanes run is computed
- **THEN** every shard's weight, including shard 1's non-test pre-load, is at most one third of the summed weight of the lane's shards

#### Scenario: macOS keeps its two-shard cap

- **GIVEN** the CI matrix's verify legs
- **WHEN** the plan decides the legs for a push, the nightly or a manual dispatch
- **THEN** `ubuntu-latest` and `windows-latest` each have the dispatch's shard count - four unless the dispatch picks another - and `macos-latest` has two shards, never a third

#### Scenario: macOS's weight partition is no worse than the position partition within two shards

- **GIVEN** the recorded weight table and the two-shard partition the macOS lane runs
- **WHEN** `scripts/gate-tests.sh` compares it with the position partition it replaced
- **THEN** the weight partition's busiest shard carries no more recorded weight, and its heaviest project predicts no more wall time, and the gate reports both numbers

#### Scenario: A lane given the wrong shard count fails the gate

- **GIVEN** a workflow whose macOS lane runs three shards, or whose Ubuntu or Windows lane runs two
- **WHEN** `scripts/gate-tests.sh` reads the lanes' shard counts
- **THEN** it fails and names the lane, because macOS's cap is two and the third leaves no room below four shards

#### Scenario: The partition is complete and non-overlapping for every shard count

- **GIVEN** a shard count of 1, 2, 3 or 4
- **WHEN** the shards' assignments are collected
- **THEN** every `lunate.slnx` test project appears in exactly one shard, its default-culture and de-AT passes run in that shard, and every shard owns at least one project

#### Scenario: The partition is deterministic

- **GIVEN** the same weight table and shard count
- **WHEN** the assignment is computed twice
- **THEN** both runs print the same shards in the same order, on every platform

#### Scenario: A new test project gets the default weight instead of failing the gate

- **GIVEN** a test project in `lunate.slnx` with no row in the weight table
- **WHEN** the partition is computed and the gate self-tests run
- **THEN** the project is assigned the default weight, it appears in exactly one shard, and the gate does not fail

#### Scenario: A weight row for a project the solution no longer has fails the gate

- **GIVEN** a weight table row naming a test project that is not in `lunate.slnx`
- **WHEN** `scripts/gate-tests.sh` runs the partition checks
- **THEN** it fails and names the stale row, so the table is refreshed with the project list

#### Scenario: Shard 1's non-test work is not planned as free

- **GIVEN** the recorded weight table
- **WHEN** the partition checks run
- **THEN** the table has a `#primary` row and shard 1's load includes that recorded non-test time

#### Scenario: A real run records per-project wall times

- **GIVEN** a gate run that executed test projects
- **WHEN** the run finishes
- **THEN** `artifacts/verify-timing/` holds a per-project record naming each project's wall time beside the shard's own record, and `scripts/shard-weights.sh weights --from` folds it into a weight table

#### Scenario: A failed pass still records its projects

- **GIVEN** a gate run whose default or de-AT pass fails after its retry budget, or an opt-in pooled run
- **WHEN** the pass finishes
- **THEN** its per-project records are written for every project it ran before the leg exits, so a red run's wall times are not lost

#### Scenario: A shard's two culture passes are both kept

- **GIVEN** a shard that ran its default-culture pass and its de-AT pass
- **WHEN** the run records the per-project wall times
- **THEN** it writes one record for each pass, neither pass replacing the other, and the fold takes the project's slower recorded pass
