# repo-foundation Specification

## Purpose
The repository's build-and-verification contract: every Lunate project builds on .NET 10 with warnings as errors, the project graph stays downward-only, the core libraries' public surface is tracked, and one command (`scripts/verify.sh`) decides whether the repository is done. Later capabilities extend this contract rather than rebuilding it.

## Requirements

### Requirement: Clean build contract
The repository SHALL build all projects on .NET 11 with compiler warnings treated as errors.

#### Scenario: Clean checkout builds
- **GIVEN** a clean checkout with the pinned .NET SDK installed
- **WHEN** `dotnet build` runs
- **THEN** every project compiles with zero warnings and the command exits zero

#### Scenario: A warning fails the build
- **GIVEN** a source change that introduces a compiler warning
- **WHEN** `dotnet build` runs
- **THEN** the build fails and reports the warning as an error

### Requirement: Enforced layering
Project references SHALL point only downward in the architecture: `Lunate.Ai` ← `Lunate.Agent` ← `Lunate.Protocols` / `Lunate.Coding`, and `Lunate.Tui` SHALL reference no other Lunate project. `Lunate.Agent` SHALL reference, at runtime, only `Microsoft.Extensions.AI.Abstractions` and the BCL in addition to `Lunate.Ai`; build-only packages with `PrivateAssets="all"` are exempt. The architecture test SHALL enforce both the project-reference and the package rules.

#### Scenario: Upward reference is rejected
- **GIVEN** a project graph containing an upward reference, e.g. `Lunate.Ai` → `Lunate.Agent`
- **WHEN** the architecture test runs
- **THEN** it fails and names the offending reference

#### Scenario: The real graph passes
- **GIVEN** the repository's actual project graph
- **WHEN** the architecture test runs
- **THEN** it passes

#### Scenario: A runtime package outside the allowed set fails the architecture test
- **GIVEN** a runtime package reference added to `Lunate.Agent` (no `PrivateAssets="all"`) outside the allowed set
- **WHEN** the architecture test runs
- **THEN** it fails and names the offending package

### Requirement: Version entry point
`Lunate.Coding` SHALL provide `lunate --version`, printing the product version and exiting zero within the startup budget.

#### Scenario: Version flag on the published binary
- **GIVEN** the published single-file `lunate` binary
- **WHEN** it is invoked with `--version`
- **THEN** it prints the version, exits 0, and startup stays within the configured budget

### Requirement: Verification gate
`scripts/verify.sh` SHALL run build, tests, single-file publish, startup budget, formatting (CSharpier), documentation lint and public API checks, and SHALL exit non-zero on any failure.

#### Scenario: Green on the skeleton
- **GIVEN** the skeleton repository
- **WHEN** `scripts/verify.sh` runs
- **THEN** it completes with `verify: OK`

#### Scenario: Budget breach turns the gate red
- **GIVEN** an artificially low startup budget (`BUDGET_MS=1`)
- **WHEN** `scripts/verify.sh` runs
- **THEN** it exits non-zero and reports the startup budget exceeded

### Requirement: Public API visibility
Library projects SHALL track their public surface in `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`, and verification SHALL fail when an existing `Shipped` file is edited or deleted relative to the merge base with `main` (or the branch base where a remote is unavailable). Adding a new tracking file for a project is the bootstrap path and SHALL NOT trip the gate.

#### Scenario: Shipped API change is caught
- **GIVEN** a committed or uncommitted edit, or a deletion, of an existing `PublicAPI.Shipped.txt` on a branch
- **WHEN** `scripts/verify.sh` runs
- **THEN** it exits non-zero and shows the diff relative to the merge base with `main`

#### Scenario: A new tracking file passes
- **GIVEN** a branch that adds a `PublicAPI.Shipped.txt` for a project that had none
- **WHEN** `scripts/verify.sh` runs
- **THEN** the gate passes, and the new file's surface entries still go through review as a new file

### Requirement: Calibrated performance budgets
The startup budget SHALL be calibrated per environment from measured baselines (dev machines and each CI runner) and re-calibrated when a baseline shifts; budgets SHALL keep documented headroom above the measured median so they catch regressions rather than machine noise.

#### Scenario: Budgets reflect measured baselines
- **GIVEN** measured startup medians for the dev machine and each CI runner (spike S-3)
- **WHEN** the budgets are set in `scripts/perf.sh` and the CI matrix
- **THEN** each budget sits above its measured median with documented headroom

#### Scenario: A regression trips the budget
- **GIVEN** a change that materially regresses startup (for example, doubling the median)
- **WHEN** the verification gate runs
- **THEN** the budget is exceeded and the gate fails

#### Scenario: Re-calibration is documented
- **GIVEN** a baseline shift (new hardware or a runner image change)
- **WHEN** the budgets are updated
- **THEN** the new measurements and the reason for the change are recorded in the spike report

### Requirement: Target-framework split

The self-contained application and the `dotnet tool` package SHALL target `net11.0`. Every
first-party project that is not one of the published extension-facing packages SHALL target
`net11.0`. The published extension-facing packages — `Lunate.Contracts`,
`Lunate.Extensibility.Abstractions`, `Lunate.Agent`, and the `Lunate.Ai` / `Lunate.Ai.Decisions`
assemblies `Lunate.Agent` carries — SHALL multi-target `net10.0;net11.0` and both assets SHALL build
in the gate and be packed into the same package. Their public API SHALL be identical on both assets:
no union, closed or otherwise `net11.0`-only type SHALL appear on the contract surface while
`net10.0` remains a target. Dropping `net10.0` from a published contract package SHALL require an ADR
and a deprecation window, never a silent target-framework bump.

#### Scenario: The application and tool target net11

- **GIVEN** a clean checkout with the pinned SDK
- **WHEN** the solution builds
- **THEN** `Lunate.Coding` and every non-contract first-party project build for `net11.0`

#### Scenario: The multi-target packs build both assets

- **GIVEN** the published extension-facing projects
- **WHEN** `dotnet pack` runs for each of them
- **THEN** the package carries both a `net10.0` and a `net11.0` asset

#### Scenario: A net10 extension still compiles

- **GIVEN** an extension project targeting `net10.0`
- **WHEN** it restores and builds against the published contract packages
- **THEN** it compiles against the `net10.0` asset

#### Scenario: The public surface is the same on both assets

- **GIVEN** a public type in a multi-target contract package
- **WHEN** the public API analyzer runs for both target frameworks
- **THEN** neither target introduces a public member the other lacks

### Requirement: Budgets are re-measured on a runtime change

When the runtime the product ships on changes, the startup and memory budgets SHALL be re-measured
on the new runtime before the existing numbers are treated as evidence. The measurements SHALL be
recorded with the change. A measurement that exceeds the budget's documented headroom SHALL
re-calibrate the budget per ADR-0005 and ADR-0009; a measurement inside the headroom keeps the
budget and records why.

#### Scenario: A runtime change is measured

- **GIVEN** a published binary on a new runtime
- **WHEN** the startup and memory gates run
- **THEN** the measured startup floor and peak RSS are recorded before the change is called done

#### Scenario: A measurement inside the headroom keeps the budget

- **GIVEN** a re-measured startup floor or peak RSS below the existing budget
- **WHEN** the change is reviewed
- **THEN** the budget is unchanged and the measurement is recorded as its evidence

#### Scenario: A measurement beyond the headroom re-calibrates

- **GIVEN** a re-measured startup floor or peak RSS above the existing budget
- **WHEN** the change is reviewed
- **THEN** the budget is raised per ADR-0005/ADR-0009 and the new number and reason are recorded
