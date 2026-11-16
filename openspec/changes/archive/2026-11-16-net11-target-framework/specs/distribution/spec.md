## MODIFIED Requirements

### Requirement: Dotnet tool package

`Lunate.Coding` SHALL pack as a framework-dependent .NET tool with package id `lunate` and command
`lunate`, adding no new dependencies. The package SHALL ship the `net11.0` tools asset and no
`net10.0` tools asset, so installing it needs the .NET 11 runtime; the self-contained release archives
SHALL be the install path for a machine without that runtime, and the release note SHALL say both. The
release workflow SHALL attach the `.nupkg` to the GitHub release, and the verify gate SHALL prove a
local roundtrip — pack, `dotnet tool install` from the local feed, run `lunate --version`.

#### Scenario: The tool roundtrips from a local feed

- **GIVEN** a `dotnet pack` of the CLI and an empty tool path on a .NET 11 runtime
- **WHEN** the tool is installed from the local feed and run
- **THEN** `lunate --version` reports the current version

#### Scenario: The package carries the current framework's tools asset

- **GIVEN** the packed `lunate` package
- **WHEN** its contents are listed
- **THEN** the tools asset sits under the `net11.0` framework folder and no `net10.0` tools asset is
  present

#### Scenario: The release note names the runtime

- **GIVEN** a release whose artifacts include the tool package
- **WHEN** the release notes are read
- **THEN** they say the `dotnet tool` needs the .NET 11 runtime and that the archives do not
