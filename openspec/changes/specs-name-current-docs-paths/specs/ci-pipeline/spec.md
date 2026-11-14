## MODIFIED Requirements

### Requirement: Per-commit verification matrix

CI SHALL build and test the solution on ubuntu, macos and windows runners for every push to `main` and every pull request that changes anything outside the prose paths, running the verification gate on each OS. The checks whose result does not depend on the operating system — formatting, the generated documentation regions, the documentation link, anchor, redirect and navigation check (`scripts/docs-check --check`), markdownlint, the public API, `openspec validate --all --strict`, the scratch-path and spec-archive checks, and the packaging-file assertion — SHALL run once in a single Linux job that the aggregate gate requires; an OS leg SHALL NOT repeat them. Each OS leg SHALL keep the checks whose result is a property of that machine: build, test, publish, the startup budget and the artifact proofs. A pull request whose changed paths are all prose SHALL instead run the documentation profile on one runner, because such a change can break neither the build nor a test. The profile SHALL be chosen by a committed script with a self-test, deny-by-default: only `docs/`, `adr/`, `openspec/` and root-level Markdown qualify, and anything else — any change under `.github/workflows/`, either side of a rename, and any path outside those trees — SHALL select the full gate. A profile that cannot be read SHALL select the full gate. Pushes that touch only documentation MAY skip the matrix but SHALL still run the documentation lint and the documentation link, anchor, redirect and navigation check.

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
- **THEN** the full matrix runs on all three runners

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
