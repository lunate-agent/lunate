## 1. Command and report

- [x] 1.1 Add `DoctorCommand` with `[--json]` parsing, the required-path health rule and exit codes 0/1/2.
- [x] 1.2 Add the report model and collectors for version, OS/architecture, install channel, paths, provider/model/credential presence, container engines, terminal capabilities and the cached update-check result.
- [x] 1.3 Add the text and JSON views from one report model, with invariant formatting and no secret values.
- [x] 1.4 Register `lunate doctor` in `Cli` and document it in `CliHelp`.

## 2. Tests

- [x] 2.1 Add `DoctorCommandTests` for the healthy report, missing required paths, unreadable settings, install-channel classification, JSON shape, no-cache update-check answer and usage errors.
- [x] 2.2 Add the seeded-key test asserting the secret never appears in text or JSON output.
- [x] 2.3 Add the `Cli --help` assertion for the new command.

## 3. Documentation and release note

- [x] 3.1 Document `lunate doctor [--json]` in `docs/cli.md` and add the first troubleshooting step in `docs/troubleshooting.md`.
- [x] 3.2 Add `changes/<pull-request-number>.md` with the release-note line.

## 4. Verification

- [x] 4.1 Run CSharpier, the touched build and the named tests.
- [x] 4.2 Run `openspec validate doctor-command --type change --strict`.
- [x] 4.3 Sync specs and archive with `openspec archive doctor-command --yes` as the last commit.
