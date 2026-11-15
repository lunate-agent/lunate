## Why

Print mode cannot be given a session id up front: `--session <id|path>` only loads an existing
session, and `--continue` picks the newest session recorded for the working directory. A supervisor
that wants to name a run before it starts — so it can find the process by `--session-id <id>` on the
command line and continue the same name later — has no way to do it. Pi takes `--session-id <id>`
("use exact project session ID, creating it if missing") for exactly this reason (lunate#870).

## What Changes

- **New launch flag `--session-id <id>`**: resolves `<id>.jsonl` in the working directory's session
  directory. It loads that file when it exists (continuing its history) and creates a session with
  the caller's id when it does not, recording the launch prompt directives like a fresh run. It is
  available in print mode and the interactive session, because the launch parser and the session
  selection are shared by both frontends.
- **The id has a grammar**: 1–128 characters from `A-Z`, `a-z`, `0-9`, `.`, `_` and `-`, starting
  with a letter or digit and without `..`. A value outside it is a usage error (exit 2) naming
  `--session-id` and the value, before anything is composed. The grammar is checked only for the
  caller-chosen id, never for an existing session loaded with `--session`.
- **It is a session selection flag**: mutually exclusive with `--session`, `--continue`,
  `--resume` and `--no-session` (usage error naming both flags), and a usage error in ACP mode like
  the rest.
- **Observable identity**: the chosen id is the session's header id, so `run_started`,
  `run_finished` and every other event carry it as `sessionId`; the process command line already
  carries `--session-id <id>` because the caller passed it. No session-format change.
- **Documentation**: `lunate --help`, `docs/cli.md` and `docs/print.md` describe the flag, the
  grammar and the conflicts; the ACP and how-it-works pages list it with the other selection flags.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `print-mode`: print mode accepts `--session-id <id>`, creates or continues that id in the working
  directory's session directory, reports it in the event stream, and treats an id outside the
  grammar or a combination with the other selection flags as a usage error (exit 2).
- `agent-sessions`: the launch-time session selection gains `--session-id <id>` — a deterministic
  create-or-continue in the working directory's session directory rather than the store-wide,
  possibly ambiguous lookup `--session <id>` performs.

## Impact

- `src/Lunate.Coding/LaunchOptions.cs`: the new flag, its grammar check and its place in the
  selection conflict list.
- `src/Lunate.Coding/SessionSelection.cs`: the shared grammar and the id's path in a session
  directory.
- `src/Lunate.Coding/PrintMode.cs`: resolve the id (load when present, create when absent).
- `src/Lunate.Coding/InteractiveSession.Selection.cs`: the same resolution for the interactive
  session.
- `src/Lunate.Coding/HarnessFactory.cs`, `src/Lunate.Agent/Session.cs`: an internal explicit-id
  creation overload that keeps the repository/worktree header fields (no public API change).
- `src/Lunate.Coding/CliHelp.cs`, `docs/cli.md`, `docs/print.md`, `docs/acp.md`,
  `docs/how-lunate-works.md`, `docs/dev/guide.md`.
- Tests in `tests/Lunate.Coding.Tests/CliTests.cs`, `PrintModeTests.cs` and
  `InteractiveSessionSessionSelectionTests.cs`.
- No new package, no public API change, no session-format change, no settings key.
