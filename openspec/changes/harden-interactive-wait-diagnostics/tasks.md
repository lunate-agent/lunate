# Tasks — harden the interactive wait's timeout diagnosis

## 1. Diagnosis in the wait helper

- [x] 1.1 `WaitBudgets`: add `Diagnosis` (the ceiling for collecting evidence after a wait has
  already failed) and `WaitExpiry` (how long a test that drives a wait to its own expiry gives it),
  each with the summary the ceiling check requires.
- [x] 1.2 New test-support file: the process-tree and descriptor diagnosis — `/bin/ps` rows
  (`pid,ppid,pgid,stat,wchan,comm`) for the test process's descendants on POSIX, `/proc/<pid>/fd`
  links on Linux, a best-effort equivalent on Windows, everything bounded by `WaitBudgets.Diagnosis`
  and reporting a collection failure instead of throwing.
- [x] 1.3 `Snapshot()` reports every session field the waits in this project test (composition,
  cursor, queued follow-ups, autocomplete, transcript size, alternate screen, on top of what it
  already carried).
- [x] 1.4 `WaitUntilAsync` captures the condition expression from the call site, counts polls, records
  the longest poll gap, and throws a message naming the condition, the budget, the elapsed time, the
  poll count and gap, the thread-pool state, the snapshot and the process tree.
- [x] 1.5 The sibling waits that carry their own budget (`WaitForScrollbackAsync`,
  `ScrollbackAfterAsync`) use the same diagnosis instead of a partial one.

## 2. Regression tests

- [x] 2.1 `WaitDiagnosisTests`: a wait driven to its own expiry names its condition, its poll count
  and longest gap, the session fields, and the process tree with its own pid (fails before 1.2–1.4,
  passes after).
- [x] 2.2 The same tests assert the budget is unchanged (the unnamed wait still uses the cross-turn
  ceiling) and that a diagnosis step that cannot run is reported rather than thrown.

## 3. Verification

- [x] 3.1 Test-first proof recorded: the new tests fail on the pre-change helper, pass after.
- [x] 3.2 `dotnet csharpier check .`, a Release build, `openspec validate --all --strict` and
  `bash scripts/check-openspec-skills.sh` pass.
- [x] 3.3 Run by filter and list them: `WaitDiagnosisTests`, plus every class that references a type
  touched (`WaitCeilingTests`, `ChildProcessProbeTests`, and the two affected classes
  `SetupWizardStepPins` / `InteractiveSessionEditorTests` as the issue's namesakes).
- [x] 3.4 Docs: the finding (spawn fd inheritance → #699) and the no-reproduction measurement are on
  #660; the PR lists what was run. No product docs change, because no product behaviour changes.

## 4. Close out

- [ ] 4.1 Answer every acceptance check from the card by number in a PR comment, with `file:line` or
  test names.
- [ ] 4.2 `openspec sync-specs` then `openspec archive harden-interactive-wait-diagnostics --yes` as
  the final commit.
