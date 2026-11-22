## Why

Troubleshooting starts from `docs/troubleshooting.md` and guesswork, even though the individual probes already exist. Nothing prints the install channel, the resolved paths, the provider and model, and the environment in one place, so a bug report begins with a round of questions instead of facts.

## What Changes

- Add `lunate doctor [--json]`, one offline command that prints version, OS and architecture, install channel, config and state paths with existence, the resolved provider and model, credential presence (never values), container-engine availability, terminal capabilities and the last update-check result.
- The human report is one fact per line; `--json` prints one pasteable object with the same facts plus `healthy` and `problems`.
- Exit 0 when the state root and settings file exist and the settings read; exit 1 when a required path is missing or the settings cannot be read; exit 2 on a usage error.
- The command never makes a network request and never prints a secret value: credentials are resolved only to `present` or `absent`.
- Add the command to `--help`, the command reference, the troubleshooting page and the changelog fragment.

## Capabilities

### New Capabilities

- `doctor`: the `lunate doctor` command's report, its offline and secret-free contract, its required-path health rule and its JSON shape.

### Modified Capabilities

- None. The command reuses existing settings, auth-store, provider, catalog, update-check, terminal and execution facts without changing their behaviour.

## Impact

- `src/Lunate.Coding/Commands/DoctorCommand.cs`, a small `DoctorView`/`DoctorReport` area, and the `Cli` dispatch plus `CliHelp` entry.
- `tests/Lunate.Coding.Tests/Commands/DoctorCommandTests.cs` and a `Cli` help assertion.
- `docs/cli.md`, `docs/troubleshooting.md`, `AGENTS.md` does not change.
- No new package, no public API, no network dependency, no protocol change.
