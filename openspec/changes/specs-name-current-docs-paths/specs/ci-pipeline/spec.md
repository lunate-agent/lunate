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

A `merge_group` run SHALL run the full test gate on `ubuntu-latest` only and SHALL skip the work the
pull request already proved: the changed-path decision, the changelog fragment guard, the
platform-independent checks, the documentation profile, the Windows PowerShell twin and the dedicated
perf leg SHALL NOT run. A prose-only batch SHALL therefore run the plan only, because its pull
request already ran the documentation profile.

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

Pushes that touch only documentation MAY skip the matrix but SHALL still run the documentation lint
and the documentation link, anchor, redirect and navigation check.

#### Scenario: A pull request runs the fast gate
- **GIVEN** a pull request that changes source code
- **WHEN** the pull request's CI run completes
- **THEN** the plan, the changed-path decision, the changelog guard, the platform checks and the Ubuntu verify shards ran, and no macOS, Windows, PowerShell, perf or CodeQL job ran

#### Scenario: A merge group runs the Ubuntu test gate only
- **GIVEN** a `merge_group` run for a batch that contains code
- **WHEN** all its jobs complete
- **THEN** the plan and the Ubuntu verify shards ran, and the changed-path decision, changelog guard, platform checks, docs profile, PowerShell twin, perf leg and macOS and Windows legs did not run

#### Scenario: A prose-only batch runs the plan only
- **GIVEN** a `merge_group` run whose changed paths are all prose
- **WHEN** its CI run completes
- **THEN** only the plan and the aggregate check ran, because the pull request already ran the documentation profile

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

#### Scenario: A broken documentation link cannot land

- **GIVEN** a documentation page whose relative link or `#anchor` does not resolve
- **WHEN** the full gate or the documentation profile runs, on a pull request or on a documentation-only push to `main`
- **THEN** `scripts/docs-check --check` fails, names the page and the link, and the run is red
