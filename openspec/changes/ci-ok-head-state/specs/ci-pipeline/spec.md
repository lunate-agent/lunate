# ci-pipeline — ci-ok judges the live head

## MODIFIED Requirements

### Requirement: Aggregate merge gate

The CI workflow SHALL expose a single aggregate status check (`ci-ok`) that succeeds only when every verification job the selected profile requires has completed successfully and every job that profile does not select was skipped, so renaming, adding, or removing a matrix leg cannot silently drop required coverage. The repository ruleset SHALL require the aggregate check for `main` — a one-time maintainer configuration outside the workflow's control. The aggregate SHALL report a result on every pull request: the workflow SHALL NOT apply path filters to pull-request events. A required job that is skipped or cancelled SHALL NOT count as success, a job that ran although the profile did not select it SHALL fail the aggregate, and a run whose profile cannot be read SHALL fail rather than pass.

For a pull request, the aggregate SHALL decide from the pull request's live draft state and the live head commit's check runs read through the GitHub API, not from the triggering run's frozen `draft` payload or its own `needs` results. It SHALL take, for each required job, the newest attempt across all runs on that live head. A run whose newest plan check run was skipped is a draft-payload run and SHALL NOT count; a cancelled attempt SHALL NOT count when any other attempt of that job exists, and SHALL never be a pass when it is the only evidence. While the live gate's required jobs are still queued or running, the aggregate SHALL fail with `gate still running` and SHALL name those jobs; it SHALL NOT report green and SHALL NOT turn this run's payload into a red. Rerunning an older or draft-payload run SHALL therefore yield the live head's verdict: green when the head's ready gate passed, red when it failed. `merge_group`, push, `schedule` and `workflow_dispatch` decisions SHALL keep the existing payload decision.

Two jobs MAY be conditional: the Windows PowerShell leg on the changed paths and the changelog
fragment gate on the pull-request event. The PowerShell skip SHALL count as success only when the
path decision output says the leg was not required; a skip or cancellation while the decision says it
was required SHALL fail the aggregate, and an unreadable decision SHALL fail it. The decision SHALL
be produced by a committed script with a self-test, and the leg SHALL also run on every push to
`main`, every tag, a nightly schedule and manual dispatch, so the filter cannot hide a regression
indefinitely. The changelog gate SHALL run on every non-draft pull request regardless of profile, and
SHALL fail when the pull request is not the release pull request and its changed paths include
`CHANGELOG.md`; its skip SHALL count as success only outside a pull-request event, and a pull-request
run in which it did not succeed SHALL fail the aggregate.

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

#### Scenario: A ready gate with running jobs says so

- **GIVEN** a live ready pull request whose required jobs include a check run that is queued or in progress
- **WHEN** the aggregate check runs
- **THEN** it fails with `gate still running`, names those jobs, and neither reports green nor derives a red from its own payload

#### Scenario: A live draft is red

- **GIVEN** a pull request that is currently a draft, even though an earlier run on the same head passed
- **WHEN** the aggregate check runs
- **THEN** it fails because the head is a draft, from the pull request's live state

#### Scenario: A merge group keeps its payload decision

- **GIVEN** a `merge_group` run
- **WHEN** the aggregate check runs
- **THEN** it decides from that run's required jobs exactly as before, not through the pull-request API path

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
