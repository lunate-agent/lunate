## Why

A long turn can finish while the user is looking at another window, and nothing tells them the answer
is ready. Lunate has no OS notification path, so the user either waits at an idle terminal or returns
to discover that the work finished minutes ago.

## What Changes

- Add opt-in desktop notifications for a long interactive turn: off by default, enabled by the
  `notify` setting or the `--notify` launch flag.
- Notify only when the root turn ends, the terminal reports that its window is not focused, and the
  turn lasted at least `notifyMinSeconds` (default 10 seconds, configurable, `0` removes the
  threshold).
- The notification carries the session title (falling back to the session id) and the outcome:
  finished, cancelled, failed, or stopped by a length, step, token, cost or time limit.
- Deliver through the platform's own tool: `osascript` on macOS, `notify-send` on Linux and a
  PowerShell notification through the Windows shell notification area on Windows, because the
  unpackaged app has no registered AppUserModelID for a Windows Runtime toast. Delivery is best
  effort; a missing tool, a failing command or an unsupported platform is silent and never changes
  the turn's result.
- Track terminal focus with the standard xterm focus-reporting sequence (`CSI I` / `CSI O`, enabled
  by `DECSET 1004` in raw mode). A terminal that never reports focus leaves the state unknown, so
  Lunate sends no notification rather than guessing.
- Document the platform tools, the focus-reporting limit and the settings in the usage page, the
  settings reference and the command reference.

## Capabilities

### New Capabilities

- `notifications`: the opt-in desktop notification of a long interactive turn, its focus and
  duration gates, the platform transports, and the failure-isolation rule.

### Modified Capabilities

- `agent-config`: the settings file accepts `notify` and `notifyMinSeconds`, with their defaults and
  validation, beside the existing user-scope keys.

## Impact

`src/Lunate.Coding/Settings/SettingsStore.cs`, `src/Lunate.Coding/settings-schema.json`,
`src/Lunate.Coding/Cli/LaunchOptions.cs`, `CliHelp.cs`, the interactive session's turn completion,
a small notification sender and turn gate, and `Lunate.Tui`'s key reader and console focus state.
Tests under `tests/Lunate.Coding.Tests` and `tests/Lunate.Tui.Tests`; docs under `docs/`. No public
API change, no new package, no session-format change, no protocol change.
