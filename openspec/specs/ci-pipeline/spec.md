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
scratch-path and spec-archive checks, and the packaging-file assertion — SHALL run once in a single
Linux job that the aggregate gate requires; an OS leg SHALL NOT repeat them. Each OS leg SHALL keep
the checks whose result is a property of that machine: build, test, publish, the startup budget and
the artifact proofs.

The tree checks — ADR numbering, the spec-archive rule, the generated documentation regions, the
documentation link, anchor, redirect and navigation check, and markdownlint — SHALL run in one job on
a `merge_group` run, judged against the batch's base commit. The batch is the first tree on which two
pull requests meet, and each pull request proved only its own tree against its own base, so only the
queue can see a rule both of them break together. The same command list SHALL be what the push to
`main` runs, so the two triggers cannot drift. The job SHALL need no build and no test, and it SHALL
take the batch's full history and its release tags, because the spec-archive check diffs the batch's
range and the version region is derived from the tags.

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
- **GIVEN** a pull request that changes `docs/guide.md` and `src/Lunate.Tui/Interaction/KeyBinding.cs`
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

The CI, CodeQL and release workflows SHALL install the .NET SDK version pinned in `global.json`. The pin SHALL be one exact version — `rollForward: disable` and `allowPrerelease: false` — so a patch that Microsoft has staged but not published cannot change what the gate installs; the pinned SDK also decides the runtime bundled into the self-contained binary, so the version is a product decision, not only a build input. A newer patch SHALL arrive as a Dependabot pull request against `global.json` (the `dotnet-sdk` ecosystem, weekly), which runs the full gate before merge; when a newer feature band carries fixes that affect the gate (for example test-runner exit behaviour on Windows), the pin SHALL move in its own change with the rationale recorded.

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

The Release workflow SHALL fail a tag whose commit is not on `main` or whose commit is not associated with a merged pull request carrying one of the two release signals, so a release cannot be cut from a release branch the lane has not merged. The lane's own release and merge execution SHALL be tracked in lunate-lane#21. After attaching the assets, the Release workflow SHALL read the published release back and SHALL fail unless it is not a draft and carries its `SHA256SUMS` asset. A committed script SHALL make both decisions and SHALL carry test cases for an unmerged release pull request, a tagged non-release pull request, a draft release and a release without `SHA256SUMS`. The release runbook SHALL name the same verification.

#### Scenario: The release pull request may edit the changelog

- **GIVEN** a pull request on branch `release/0.4.0` that runs `--write` and commits `CHANGELOG.md`
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

### Requirement: Nightly failure reporting

A red nightly `schedule` run SHALL file its own issue so it cannot sit unnoticed: the CI workflow
SHALL run a reporter that reads the run's jobs and, for every job whose conclusion is red, opens a
`kind/bug` issue in milestone `0.4.0` naming the failing job and the run link, or comments on the
already-open issue for that job instead of filing a duplicate. The reporter SHALL key the duplicate
check on the job's title, so a week of red nights for the same failing job is one open card. A green
nightly SHALL file nothing.

#### Scenario: A red nightly files a card naming the job

- **GIVEN** a nightly run in which `verify (windows-latest, 2/4)` failed
- **WHEN** the reporter runs
- **THEN** it opens a `kind/bug` issue in `0.4.0` whose title names `verify (windows-latest, 2/4)` and whose body carries the run link

#### Scenario: An open card is reused instead of duplicated

- **GIVEN** an open issue whose title already names the failing job
- **WHEN** the next red nightly reporter runs
- **THEN** it comments on that issue and creates no second one

#### Scenario: A green nightly files nothing

- **GIVEN** a nightly run in which every job succeeded
- **WHEN** the reporter runs
- **THEN** it creates and comments on no issue
