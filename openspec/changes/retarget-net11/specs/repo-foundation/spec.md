## ADDED Requirements

### Requirement: One target framework

Every project in the repository SHALL target `net11.0`, and no project SHALL multi-target: no
project file SHALL declare `TargetFrameworks`, and every `TargetFramework` a project declares SHALL
be `net11.0`. `Directory.Build.props` SHALL declare `net11.0` as the one framework every project
inherits. A published package SHALL carry a single `lib/net11.0` asset, and the `dotnet tool` package
SHALL carry a single `tools/net11.0` asset. Directories holding somebody else's project rather than a
project of this repository — an eval task under `eval/tasks/` and a spike record under
`docs/dev/spikes/` — are data and are exempt; a fixture whose subject is the multi-target shape may
multi-target at `net11.0`.

Reintroducing multi-targeting, or dropping `net11.0` for a published package, SHALL require its own
ADR and a deprecation window, never a silent target-framework bump.

#### Scenario: A clean build targets one framework

- **GIVEN** a clean checkout with the pinned SDK
- **WHEN** the solution builds
- **THEN** every project builds for `net11.0` and the build produces no second target framework

#### Scenario: A multi-targeting project fails the repository test

- **GIVEN** a project that declares `TargetFrameworks` or a `TargetFramework` other than `net11.0`
- **WHEN** the target-framework test runs
- **THEN** it fails and names the project and the framework it declares

#### Scenario: The packed contract carries one asset

- **GIVEN** a freshly packed `Lunate.Contracts` package
- **WHEN** its `lib/` entries are read
- **THEN** it carries `lib/net11.0` and no other framework's asset

## MODIFIED Requirements

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
