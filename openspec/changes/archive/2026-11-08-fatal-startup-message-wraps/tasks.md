## 1. Pin the reported behaviour

- [x] 1.1 Add a failing acceptance test in `tests/Lunate.Coding.Tests` that runs the real interactive host at 40 columns in a child process with `LUNATE_HOME` inside the worktree, feeds the console writes through `TerminalScreen`, and asserts the whole guard message is visible across wrapped rows with no row wider than 40 columns.
- [x] 1.2 Add a unit test for the shared fatal-message renderer at 40 columns: the guard sentence wraps, every line is at most 40 cells, and joining the lines reconstructs the full text (no substring-of-source assertion).
- [x] 1.3 Add a child-process test that sets `LUNATE_HOME` unset, `HOME`/`USERPROFILE` to a temporary home, and the session working directory to that home; assert the session composes normally and reports no workspace refusal.

## 2. Implement the fix

- [x] 2.1 Add the internal `TerminalMessage` helper to `src/Lunate.Tui` (`Lines`/`Write`), wrapping with `CellText` and treating a non-positive or `int.MaxValue` width as no wrap.
- [x] 2.2 In `InteractiveSession.RunAsync`'s composition-failure catch, commit `lunate: <reason>` to the live area's scrollback with the helper before teardown; keep the existing exit-code mapping and stderr fallback.
- [x] 2.3 Use the helper for the pre-session fatal paths: the raw-mode failure in `InteractiveSession.RunAsync` and the terminal-support/setup refusals in `Cli.RunInteractive`, at the console's column count.

## 3. Verify and record

- [x] 3.1 Run `dotnet csharpier format .`, then `dotnet build Lunate.slnx -c Release`.
- [x] 3.2 Run only the touched test classes by filter (acceptance, guard/home, TUI renderer, CLI caller classes) and record their names.
- [x] 3.3 Run `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`.
- [x] 3.4 Confirm the default-home behaviour did not require a guard change; record the measurement in the PR and update the proposal if it differs.
