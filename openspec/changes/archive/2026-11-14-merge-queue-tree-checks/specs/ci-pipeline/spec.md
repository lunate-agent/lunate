## MODIFIED Requirements

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
