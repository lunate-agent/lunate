## Why

`InteractiveSessionHost.WaitUntilAsync` has now failed in two different tests (a terminal round-trip
test and a setup-wizard pin test) on POSIX legs, always at the same 60-second ceiling, and every
occurrence produced only a bare `TimeoutException` heading a state dump that does not name the
condition it was polling, how often it polled, or what the process tree looked like. The occurrences
block the `v0.3.0` tag (#660). A 60-second wait that never completes is a hang, not a slow runner,
and a hang that reports nothing costs an investigation per occurrence: nobody has yet seen the wait
say what it was waiting for.

## What Changes

- A timed-out wait in the interactive-session test helper reports, in its own failure message:
  - the condition it polled, captured from the call site (no per-call-site edits at the ~200 call
    sites), the budget it was given, the elapsed time and the poll count;
  - the **longest gap between two polls**, which separates "the poll loop ran 60 000 times and the
    condition stayed false" (a hang) from "the poll loop was starved for tens of seconds" (a loaded
    runner) — the distinction every occurrence so far could not make;
  - the thread pool's state at the moment of failure (`ThreadPool.ThreadCount`,
    `ThreadPool.PendingWorkItemCount`);
  - a **process tree snapshot** — `ps -o pid,ppid,pgid,stat,wchan,comm` for the test process and its
    descendants on POSIX, `/proc/<pid>/fd` links on Linux, a best-effort equivalent on Windows —
    bounded by its own ceiling so the diagnosis cannot itself hang;
  - the session state snapshot the wait's condition reads, extended with the fields the waits
    actually test.
- The sibling waits in the same support file (committed-scrollback, scrollback-after-action) carry
  the same diagnosis instead of a partial one.
- The 60-second budget is **not** raised, and the POSIX spawn path (#621) is **not** touched: the
  change is about making the next occurrence diagnosable without a rerun.
- Regression tests pin the message: a wait driven to its own expiry names its condition, its poll
  count and longest gap, the session fields, and the process tree (including its own pid).

## Capabilities

### New Capabilities

- `test-wait-diagnostics`: what a timed-out wait in the interactive-session test helper must report
  about itself — the condition, the observation count and gaps, the thread pool, the process tree —
  and that the report is bounded so the diagnosis cannot replace one silent failure with another.

### Modified Capabilities

<!-- None: no user-facing behaviour, no product spec changes. This capability is test-support
     behaviour and is deliberately kept out of the product specs. -->

## Impact

- `tests/Lunate.Coding.Tests/InteractiveSessionTestSupport.cs` — the wait helpers and `Snapshot()`.
- New test-support file for the process-tree diagnosis, plus a new `WaitBudgets` ceiling for it.
- New regression tests; every existing caller of the wait helpers is unaffected behaviourally (same
  budgets, same polling, same pass/fail), so no production code, no public API, and no new packages.
- Findings that belong to the spawn path (file-descriptor inheritance between concurrent children)
  are reported on #660 and filed as their own issue; they are out of scope here because they change
  the process-isolation contract and need maintainer sign-off.
