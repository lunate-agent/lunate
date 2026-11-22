## 1. Settings and launch flag

- [x] 1.1 Add `notify` and `notifyMinSeconds` to `AgentSettings`, parse and validate them in `SettingsStore`, and add both entries to `settings-schema.json`.
- [x] 1.2 Add `--notify` to `LaunchOptions`, `ApplyLaunch` and `CliHelp`, with boolean-flag validation and the one-run override.
- [x] 1.3 Show both keys in the `/settings` block and single-key read, and recognize them as launch-time keys.
- [x] 1.4 Add settings and launch-flag tests for defaults, validation, the flag override and help text.

## 2. Terminal focus reporting

- [x] 2.1 Add the internal focus seam to the TUI console, implement it on `SystemConsoleIO`, and script it on the test consoles.
- [x] 2.2 Consume `CSI I` / `CSI O` in `KeyReader` without emitting key events, and enable/disable `DECSET 1004` around raw mode and child handover.
- [x] 2.3 Add TUI tests for focus reports, unknown initial state and the handover reset.

## 3. Notification gate and platform transports

- [x] 3.1 Add the platform-command builder and detached sender for macOS, Linux and Windows, with a pure command shape and an injectable start seam.
- [x] 3.2 Add the turn gate that checks enabled, known-unfocused focus and the duration threshold, maps the turn outcome and swallows every sender failure.
- [x] 3.3 Wire the gate into the root turn completion with the session title, the elapsed time and the stop reason/failure message, and inject the sender through `InteractiveSessionOptions`.
- [x] 3.4 Add tests for the eligible path, flag and setting toggles, focused/unknown/short suppression, child runs, outcome mapping and a throwing sender leaving the turn successful.

## 4. Documentation and release note

- [x] 4.1 Document the setting, flag, threshold, focus-reporting limit and platform tools in `docs/usage.md`, `docs/cli.md` and the generated settings reference.
- [x] 4.2 Add `changes/<pull-request-number>.md` with the release-note line.

## 5. Verification and archive

- [x] 5.1 Run CSharpier, build every touched project, and run the named settings, TUI and notification tests.
- [x] 5.2 Run `openspec validate desktop-turn-notifications --type change --strict` and `openspec validate --all --strict`.
- [x] 5.3 Sync specs and archive with `openspec archive desktop-turn-notifications --yes` as the last commit.
