## ADDED Requirements

### Requirement: The doctor report

`lunate doctor [--json]` SHALL print one report with the product version, the operating system and
architecture, the install channel, the config path and the state path with whether each exists, the
resolved provider and model, the resolved provider credential's presence, container-engine
availability, terminal capabilities, and the last update-check result. It SHALL accept only the
optional `--json` flag; any other argument SHALL be a usage error with exit code 2.

#### Scenario: One command prints the facts a bug report needs

- **GIVEN** an installed Lunate with a `settings.json` and an update-check cache
- **WHEN** `lunate doctor` runs
- **THEN** stdout carries the version, OS and architecture, install channel, both paths with their
  existence, the provider and model, credential presence, container-engine availability, terminal
  capabilities and the cached update-check answer, one fact per line
- **AND** the command exits 0

#### Scenario: An unknown argument is a usage error

- **WHEN** `lunate doctor --verbose` runs
- **THEN** the command prints the usage line and exits 2

### Requirement: Doctor is offline and never prints a secret

`lunate doctor` SHALL make no network request. It SHALL NOT print any API key, token, stored
credential value or provider header value: a credential SHALL be reported only as `present` or
`absent`, with an environment variable name or `stored` as the source when present. Reading the
update-check cache and listing `PATH` for container engines SHALL be the only external facts it
reads.

#### Scenario: A seeded key never appears

- **GIVEN** `auth.json` stores a key whose value is `sk-doctor-secret` under the resolved provider
- **WHEN** `lunate doctor` and `lunate doctor --json` run
- **THEN** neither output contains `sk-doctor-secret`
- **AND** the credential reads `present`

#### Scenario: No network is used

- **GIVEN** a machine with no network access and an update-check cache on disk
- **WHEN** `lunate doctor` runs
- **THEN** it prints the cache's last result and succeeds without attempting a release request

### Requirement: Required paths decide the exit code

`lunate doctor` SHALL treat the state root and the config file (`settings.json` in that root) as
required paths. It SHALL exit 0 when both exist and the settings file reads, and exit 1 when either
is missing or the settings file cannot be read. A missing optional fact — a container engine, an
update-check cache, a resolved model — SHALL NOT by itself change the exit code.

#### Scenario: Both required paths exist

- **GIVEN** a state root that exists and a readable `settings.json`
- **WHEN** `lunate doctor` runs
- **THEN** every required path reads present and the command exits 0

#### Scenario: A required path is missing

- **GIVEN** a state root or `settings.json` that does not exist
- **WHEN** `lunate doctor` runs
- **THEN** the report names the missing path and the command exits 1

#### Scenario: An unreadable settings file is unhealthy

- **GIVEN** a `settings.json` that exists but fails to load
- **WHEN** `lunate doctor` runs
- **THEN** the report carries the load error as a problem and the command exits 1

### Requirement: The install channel is named

`lunate doctor` SHALL classify the running binary as `dotnet tool`, `self-contained`, `Homebrew` or
`Scoop` from the executable path, and SHALL fall back to `self-contained` when no channel marker
matches. A framework-dependent tool path under `.dotnet/tools` SHALL read `dotnet tool`; a Homebrew
`Cellar/lunate` path SHALL read `Homebrew`; a Scoop `scoop/apps/lunate` path SHALL read `Scoop`.

#### Scenario: A tool install is named

- **GIVEN** the process path is under `~/.dotnet/tools`
- **WHEN** `lunate doctor` runs
- **THEN** the install channel reads `dotnet tool`

#### Scenario: An unpacked release archive is self-contained

- **GIVEN** the process path is `~/.local/bin/lunate`
- **WHEN** `lunate doctor` runs
- **THEN** the install channel reads `self-contained`

### Requirement: The JSON report is pasteable

`lunate doctor --json` SHALL print one JSON object and nothing else on stdout. It SHALL carry
`version`, `os`, `architecture`, `installChannel`, `config` (`path`, `exists`), `state` (`path`,
`exists`), `provider`, `model`, `credential` (`state`, `source`), `containerEngines` (`docker`,
`podman`, `selected`), `terminal` (`interactive`, `color`, `unicode`, `term`, `columns`),
`updateCheck` (`checkedAt`, `latestVersion`, `changelogUrl`, `fresh`), `healthy` and `problems`.
The exit code SHALL be the same as the human report's.

#### Scenario: The JSON carries the same facts and the health

- **GIVEN** a healthy install
- **WHEN** `lunate doctor --json` runs
- **THEN** the single stdout object contains every documented field
- **AND** `healthy` is true, `problems` is empty and the command exits 0

#### Scenario: No update check has run

- **GIVEN** no update-check cache
- **WHEN** `lunate doctor --json` runs
- **THEN** `updateCheck.checkedAt`, `updateCheck.latestVersion` and `updateCheck.changelogUrl` are
  null and `updateCheck.fresh` is false
