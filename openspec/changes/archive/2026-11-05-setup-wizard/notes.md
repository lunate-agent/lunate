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

## Final slice (2026-11-05, branch `wt/issue-524-final`)

Arrives after #531 (`c90b706f` = main) had left 2.6, 4.2, 5.1–5.4, 6.1–6.2 unchecked. Audit at that
head: 2.6 and 4.2 were already implemented and pinned by the merged slices (`WizardModelCheck.cs` +
`SetupWizardModelCheckTests.cs`; `SetupCommand.cs`/`SetupCommand.Walk.cs` + `SetupCommandTests.cs`),
so the remaining real work is 5.1 (`/setup` dispatch), 5.2 (the entry menu and the scoped entry
walks, with carried context so an entry writes only what changed), 5.3 (width fixtures), 5.4 (docs)
and the 6.x gate. Ticks follow verification, in that order.

