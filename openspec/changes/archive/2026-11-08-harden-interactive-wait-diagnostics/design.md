# Design — harden the interactive wait's timeout diagnosis

## What is wrong today

`InteractiveSessionHost.WaitUntilAsync` polls a `Func<bool>` for a wall-clock budget and, on expiry,
throws `TimeoutException("The condition never became true within 60 seconds." + Snapshot())`. Three
things are missing from that message, and #660's two occurrences each cost an investigation because
of them:

1. **Which condition** never became true. There are ~200 call sites; the message named none of them,
   and the stack trace only says which helper was waiting.
2. **How the poll loop behaved.** A hang has one shape (tens of thousands of polls, condition false
   at each) and a starved runner has another (few polls, seconds between them). The message could not
   tell them apart, which is exactly the distinction the issue asks for and the reason it says "a
   60-second wait that never completes is a hang, not a slow runner".
3. **The process tree.** Every family of failing test spawns children, and #621 turned stdin into
   `/dev/null` and put the child in its own session; a child blocked on a read, a child that outlived
   its redirect, or a pipe whose write end another process still holds is only visible in `ps`/`/proc`
   output, which nobody had captured at the moment of failure.

## Decisions

### Capture the condition from the call site, not from the caller

An optional `what`-style parameter would have to be passed by 200 call sites, and the one call site
that matters is the one somebody will forget. `[CallerArgumentExpression(nameof(condition))]` gives
the lambda's own source text with no call-site change and no runtime cost when the wait succeeds. The
expression is captured into a local at entry so a `condition` captured in a closure cannot change it
under the wait.

### Measure the gap between polls, not just the elapsed time

The helper's loop is `await Task.Delay(1)` — one millisecond by intent. Recording the longest gap
between two polls turns the loop's own starvation into a number: `polls=60012 longestPollGapMs=1`
means the runner was healthy and the condition really never held; `polls=41 longestPollGapMs=2400`
means the polling thread itself was starved. `ThreadPool.ThreadCount` and
`ThreadPool.PendingWorkItemCount` are added at failure time for the same reason (a pool with all
workers blocked shows up on the next occurrence even when the poll loop looks healthy).

### Report the session state the waits actually read

`Snapshot()` already reported running, bash state, writes, input, pending approval, the arm window,
scheduled work and the scrollback. It did not report `IsComposed` — the condition in 379 of the call
sites — nor cursor, queued follow-ups, autocomplete, transcript size or the alternate screen. Every
field a wait in this project tests is now in the snapshot, so a composition timeout says
`composed=false` next to everything else instead of leaving the reader to infer it.

### Dump the process table, bounded, and never let the diagnosis hide the failure

The tree is read from `/bin/ps -axo pid=,ppid=,pgid=,stat=,wchan=,comm=` (the shape the issue asks
for), filtered to the test process's own descendants, and rendered as rows. On Linux each process's
`/proc/<pid>/fd` links are appended, because a pipe write end held by a bystander is invisible in
`ps` and decisive for the EOF question. Windows uses a best-effort `Get-CimInstance` walk through
PowerShell and says so if it cannot.

Two properties keep this from becoming a new failure mode:

- the whole collection is bounded by `WaitBudgets.Diagnosis`, a new budget, and every await uses it;
- a diagnostic step that fails or times out contributes a line saying so, and never replaces the
  `TimeoutException`.

No new packages; the process table is read with `System.Diagnostics.Process`, the same mechanism the
existing `ProcessRunner.KillPosixTree` uses for its `ps` enumeration.

### Do not touch the budget, and split the spawn finding out

The card forbids raising the 60 s ceiling, and the root cause found while investigating is in the
POSIX spawn contract (a concurrent spawn inherits another child's pipe write ends, so the finished
child's stdout never reaches EOF and `ProcessRunner.RunAsync` never returns). Fixing that changes the
ABI #621 pinned, which `AGENTS.md` reserves for maintainer sign-off, so it is filed as #699 and this
change lands only the diagnosis that makes the next occurrence legible. The design deliberately does
not hide the remaining gap: an EOF-pinned child shows up in the dump as a live descendant holding a
pipe descriptor, which is what #699 needs to confirm its own fix.

## Risks

- The timeout path now spawns `ps` (and on Linux reads `/proc`). It runs only after a failure, inside
  `WaitBudgets.Diagnosis`, and its result is plain text in the exception message — it cannot mask the
  original failure because the original exception is thrown either way.
- `Snapshot()` gains fields; existing assertions on failure text are `Contains`-style, and the new
  fields are additive.
- The `ps` output format is stable across Darwin and Linux for the requested columns (`comm` last, so
  trailing spaces in a command name are harmless). Windows output is explicitly best-effort.
