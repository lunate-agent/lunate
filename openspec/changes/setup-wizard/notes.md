# Setup wizard — implementation ledger

Execution session card (issue #524), branch `wt/issue-524`. Order follows dependency, not file
order: the wizard core (groups 2–3) must exist before the trigger (1.2), `/setup` (5) and the CLI
(4) can wire to it without breaking the current first run.

## Plan

1. **1.1** `SetupState.Check(settings, providers)` + unit tests → first commit, draft PR.
2. **2.x** wizard state machine and step drivers + tests.
3. **3.x** summary, commit through store writers, abort/skip tests, writer pinning, parity test.
4. **1.2–1.4** trigger, welcome header wizard state, fixture tests.
5. **4.x** `lunate setup` CLI: routing, flags, exit codes, CI no-TTY test.
6. **5.1–5.3** `/setup` command, entry menu, width fixture tests.
7. **5.4** docs; **6.x** gate, archive.

## Status

See the PR for the live status; this file records decisions and partial progress if the session
stops early.

- Task 1.1: implemented (`src/Lunate.Coding/SetupState.cs`),
  tests `tests/Lunate.Coding.Tests/SetupStateTests.cs`.
