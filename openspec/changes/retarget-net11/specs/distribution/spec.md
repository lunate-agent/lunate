## MODIFIED Requirements

### Requirement: Dotnet tool package

`Lunate.Coding` SHALL pack as a framework-dependent .NET tool with package id `lunate` and command
`lunate`, targeting `net11.0`, so installing and running it SHALL require the .NET 11 runtime; it
SHALL add no new dependencies. The release workflow SHALL attach the `.nupkg` to the GitHub release;
the release note SHALL state the .NET 11 runtime requirement; and the verify gate SHALL prove a local
roundtrip — pack, `dotnet tool install` from the local feed, run `lunate --version`.

#### Scenario: The tool roundtrips from a local feed

- **GIVEN** a `dotnet pack` of the CLI and an empty tool path
- **WHEN** the tool is installed from the local feed and run on a machine with the .NET 11 runtime
- **THEN** `lunate --version` reports the current version

#### Scenario: The tool package names its runtime requirement

- **GIVEN** a packed `lunate` tool package
- **WHEN** its target framework group is read
- **THEN** it carries a `net11.0` asset, and the release note for that version states that the
  `dotnet tool` install requires the .NET 11 runtime
