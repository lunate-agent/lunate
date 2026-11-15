## ADDED Requirements

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
