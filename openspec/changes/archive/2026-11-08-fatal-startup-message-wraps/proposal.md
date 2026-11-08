## Why

A fatal startup message is written straight to stderr while the live area owns the
terminal. The live area's final clear then erases the message's wrapped rows, so at 40
columns the user sees only the first line — for example
`LUNATE_HOME points inside the workspace; tr` — and cannot read why the session refused
to start. The reason for a failure has to survive the frontend that reported it.

## What Changes

- Fatal startup messages render through one width-aware path: the text wraps at the
  terminal width and is never clipped or elided, so every line of the reason is visible
  at 40 columns and at any wider terminal.
- The interactive session commits a fatal composition message to scrollback through the
  live area's writer before teardown, so the live area's clear cannot erase its wrapped
  tail. The pre-session renderer (the raw-mode failure and the terminal/setup refusal
  before the session starts) uses the same wrapping rule.
- The workspace-trust guard keeps refusing a configured `LUNATE_HOME` inside the
  workspace, and explicitly never refuses the default `~/.lunate` when `LUNATE_HOME` is
  unset — including when the session starts in the user's home directory. This is a
  regression pin: the current guard does not fire there, and the spec now says so.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `tui`: add the fatal-startup rendering contract — wrap to the terminal width, never
  truncate, and commit the message where the live area's teardown cannot erase it.
- `extensions`: state that an unset `LUNATE_HOME` is never refused, so the default home
  starts normally from the user's home directory, and pin it with a scenario.

## Impact

- `src/Lunate.Tui` gains the one wrapping helper used by both renderers.
- `src/Lunate.Coding/Cli.cs` and `src/Lunate.Coding/InteractiveSession.cs` use it for
  fatal startup diagnostics.
- No public API, dependency, session-format or golden-file change.
- Tests: a 40-column terminal-screen acceptance test for the exact guard message, a
  default-home session test in a child process, and renderer unit coverage.
