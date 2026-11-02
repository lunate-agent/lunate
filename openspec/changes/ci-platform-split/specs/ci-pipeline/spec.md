## MODIFIED Requirements

### Requirement: Per-commit verification matrix

CI SHALL build and test the solution on ubuntu, macos and windows runners for every push to `main` and every pull request that changes anything outside the prose paths, running the verification gate on each OS. The checks whose result does not depend on the operating system — formatting, the generated documentation regions, markdownlint, the public API, `openspec validate --all --strict`, the scratch-path and spec-archive checks, and the packaging-file assertion — SHALL run once in a single Linux job that the aggregate gate requires; an OS leg SHALL NOT repeat them. Each OS leg SHALL keep the checks whose result is a property of that machine: build, test, publish, the startup budget and the artifact proofs. A pull request whose changed paths are all prose SHALL instead run the documentation profile on one runner, because such a change can break neither the build nor a test. The profile SHALL be chosen by a committed script with a self-test, deny-by-default: only `docs/`, `adr/`, `openspec/` and root-level Markdown qualify, and anything else — any change under `.github/workflows/`, either side of a rename, and any path outside those trees — SHALL select the full gate. A profile that cannot be read SHALL select the full gate. Pushes that touch only documentation MAY skip the matrix but SHALL still run the documentation lint.

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
- **THEN** the full matrix runs on all three runners

#### Scenario: A workflow change is never prose
- **GIVEN** a pull request that changes `.github/workflows/ci.yml`
- **WHEN** CI decides the profile from the changed paths
- **THEN** the full gate runs, so a change to the gate cannot reduce the gate that checks it

#### Scenario: Platform checks run once
- **GIVEN** a pull request that selects the full gate
- **WHEN** the run completes
- **THEN** the formatting, docs, public-API and OpenSpec checks ran in exactly one job, and each OS leg ran them zero times

### Requirement: Aggregate merge gate

The CI workflow SHALL expose a single aggregate status check (`ci-ok`) that succeeds only when every verification job the selected profile requires has completed successfully and every job that profile does not select was skipped, so renaming, adding, or removing a matrix leg cannot silently drop required coverage. The repository ruleset SHALL require the aggregate check for `main` — a one-time maintainer configuration outside the workflow's control. The aggregate SHALL report a result on every pull request: the workflow SHALL NOT apply path filters to pull-request events. A required job that is skipped or cancelled SHALL NOT count as success, a job that ran although the profile did not select it SHALL fail the aggregate, and a run whose profile cannot be read SHALL fail rather than pass.

One job MAY be conditional on the changed paths: the Windows PowerShell leg. Its skip SHALL count as success only when the path decision output says the leg was not required; a skip or cancellation while the decision says it was required SHALL fail the aggregate, and an unreadable decision SHALL fail it. The decision SHALL be produced by a committed script with a self-test, and the leg SHALL also run on every push to `main`, every tag, a nightly schedule and manual dispatch, so the filter cannot hide a regression indefinitely.

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
