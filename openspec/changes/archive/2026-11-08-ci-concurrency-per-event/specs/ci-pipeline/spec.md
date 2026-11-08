## ADDED Requirements

### Requirement: Run concurrency is scoped to one obligation

CI SHALL cancel a run only when a newer run of the same obligation supersedes it: `cancel-in-progress`
SHALL be true only for `pull_request` runs. A push to `main`, a `v*` tag push, a `merge_group` run, a
`workflow_dispatch` run and the nightly `schedule` run SHALL NOT be cancelled by the concurrency
setting. The nightly run SHALL use a concurrency group distinct from the push run on the same branch,
so neither obligation can cancel the other. Advisory workflows that also run on `main` (`docs-lint`,
`pack`) SHALL apply the same rule; a workflow without a `pull_request` trigger therefore cancels
nothing. A committed self-test SHALL evaluate the effective group and cancellation decision per event
for `ci.yml`, `docs-lint.yml` and `pack.yml`, and SHALL fail when a workflow regresses.

#### Scenario: A push to main and the nightly coexist

- **GIVEN** a `CI [push]` run on `main` and a nightly `schedule` run on the same branch
- **WHEN** the nightly starts while the push run is in flight
- **THEN** the two runs are in different concurrency groups, neither is cancelled, and both reach a
  completed conclusion

#### Scenario: Only a pull request supersedes its own older run

- **GIVEN** two runs of the same pull request, the second started while the first is in flight
- **WHEN** the second run enters its concurrency group
- **THEN** the first run is cancelled and the second runs the gate

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
  `schedule` event, or whose nightly group equals the push group
- **WHEN** `scripts/ci-ok.sh --self-test` runs
- **THEN** it fails and names the workflow and event that broke the rule
