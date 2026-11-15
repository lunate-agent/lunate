# Tasks — print-mode-session-id

## 1. Test first

- [x] 1.1 Add failing parser tests in `tests/Lunate.Coding.Tests/CliTests.cs`:
      `LaunchOptions.TryParse(["-p", "--session-id", "run-870", "hi"])` yields
      `SessionId == "run-870"` (and the inline `--session-id=run-870` form), a bare or empty value is
      the existing "expects a value" usage error, and an id with `/`, `\`, `..`, a leading `.`, a
      space or more than 128 characters is a usage error naming `--session-id` and the value.
- [x] 1.2 Add failing conflict and ACP tests in `CliTests.cs`:
      `--session-id` with `--session`, `--continue`, `--resume` or `--no-session` exits 2 naming both
      flags, `Cli.Run(["-p", "--session-id", "bad/id", "Hi"])` exits 2 before composition, and
      `--acp --session-id <id>` is the ACP selection usage error.
- [x] 1.3 Add failing print-mode tests in `tests/Lunate.Coding.Tests/PrintModeTests.cs`: a run with
      `--session-id` and no file creates `<id>.jsonl` with that header id and `run_started` /
      `run_finished` carry `sessionId: <id>`; a second run with the same id loads the recorded
      history and leaves exactly one file.
- [x] 1.4 Add failing interactive tests in
      `tests/Lunate.Coding.Tests/InteractiveSessionSessionSelectionTests.cs`: `--session-id` creates
      the named file for the first prompt and continues it for a second run.

## 2. Implementation

- [x] 2.1 Add the shared grammar and path builder to `src/Lunate.Coding/SessionSelection.cs`
      (`ValidateId`, `IdPath`) with the store's `SessionPaths.SessionFileName` as the path-safety
      authority.
- [x] 2.2 Add `SessionId` to `src/Lunate.Coding/LaunchOptions.cs`: parse `--session-id`, validate the
      value in the parser, add it to `HasSelection` and to the selection conflict list.
- [x] 2.3 Add an internal explicit-id creation overload to `src/Lunate.Agent/Session.cs` that keeps
      the repository identity and worktree header fields, and extend
      `HarnessFactory.CreateSession` with an optional id.
- [x] 2.4 Resolve create-or-continue in `PrintMode.ResolveSession` and in
      `InteractiveSession.StartInitialSession` (`SessionFromId`), reusing one load/error helper.
- [x] 2.5 Name `--session-id` in the ACP refusal text (`Cli.TryParseAcpOptions` / `WriteAcpUsage`) so
      the flag is refused like the other selection flags.

## 3. Documentation

- [x] 3.1 Add the flag, its grammar and its conflicts to `src/Lunate.Coding/CliHelp.cs`,
      `docs/cli.md` and `docs/print.md`; list it with the selection flags in `docs/acp.md`,
      `docs/how-lunate-works.md` and the background flag list in `docs/dev/guide.md`.
- [x] 3.2 Write `changes/<pr>.md` under `### Added` and run `scripts/docs-check --check`.

## 4. Verify and archive

- [x] 4.1 Run `dotnet csharpier check .`, a Release build of `Lunate.Coding`, `Lunate.Agent` and the
      `Lunate.Coding.Tests` project, and the `CliTests`, `PrintModeTests` and
      `InteractiveSessionSessionSelectionTests` classes (CI owns the full suite and the
      publish/perf legs).
- [x] 4.2 `openspec validate print-mode-session-id --strict`, sync both delta specs (`print-mode`,
      `agent-sessions`) into `openspec/specs/`, run `openspec validate --all --strict`, and archive
      as the last commit.
