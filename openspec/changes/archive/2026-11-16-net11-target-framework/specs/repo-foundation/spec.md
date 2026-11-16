## MODIFIED Requirements

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

## ADDED Requirements

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
