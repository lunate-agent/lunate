# repo-foundation Specification

## Purpose
The repository's build-and-verification contract: every Lunate project builds on .NET 10 with warnings as errors, the project graph stays downward-only, the core libraries' public surface is tracked, and one command (`scripts/verify.sh`) decides whether the repository is done. Later capabilities extend this contract rather than rebuilding it.

## Requirements

### Requirement: Clean build contract

The repository SHALL build all projects on .NET 11 with compiler warnings treated as errors, with C#
language version `15.0` fixed once for the repository and the target framework declared once for the
projects that build a single target.

#### Scenario: Clean checkout builds

- **GIVEN** a clean checkout with the pinned .NET SDK installed
- **WHEN** `dotnet build` runs
- **THEN** every project compiles with zero warnings and the command exits zero

#### Scenario: A warning fails the build

- **GIVEN** a source change that introduces a compiler warning
- **WHEN** `dotnet build` runs
- **THEN** the build fails and reports the warning as an error

#### Scenario: A C# 15 feature that needs the runtime compiles

- **GIVEN** a first-party project that uses a C# 15 feature requiring the .NET 11 base class library
- **WHEN** `dotnet build` runs
- **THEN** it compiles for `net11.0` and the build neither warns nor errors

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

### Requirement: Which projects may target more than one framework

A project SHALL build more than one target framework only when it is extension-facing: one of the
published packages — `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`,
`Lunate.Ai` and `Lunate.Ai.Decisions` (the two assemblies the `Lunate.Agent` package carries) and
`Mermaid.Terminal` — or the extension template, `templates/extension/TemplateExtension`, which
publishes nothing and builds both frameworks to prove the `net10.0` floor. Every other project
SHALL build the single `net11.0` target. A multi-targeted project SHALL clear the inherited
`TargetFramework` before declaring its `TargetFrameworks`, because MSBuild only takes the
cross-targeting path when `TargetFramework` is empty.

#### Scenario: A multi-targeted project builds every declared asset

- **GIVEN** a project that declares `net10.0;net11.0`
- **WHEN** it is built or packed
- **THEN** the output carries one compiled asset per declared framework and `pack` succeeds

#### Scenario: The extension template is the one multi-targeted project that publishes nothing

- **GIVEN** `templates/extension/TemplateExtension`, which the solution lists
- **WHEN** the solution builds
- **THEN** the template compiles for `net10.0` and for `net11.0`, and the build publishes no
  package for it

#### Scenario: A project that forgets to clear the inherited framework fails

- **GIVEN** a project that declares `TargetFrameworks` but inherits `TargetFramework`
- **WHEN** it is packed
- **THEN** the missing asset fails `pack` (NU5026) rather than shipping a package with one framework
