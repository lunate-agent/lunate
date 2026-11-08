## ADDED Requirements

### Requirement: A timed-out wait names the condition it polled

A wait in the interactive-session test helper that expires SHALL name, in its own failure message,
the condition it was polling, the budget it was given, the time it spent, and the number of polls it
made. The condition SHALL be captured at the call site, so naming it costs a caller nothing and a new
call site cannot forget it.

#### Scenario: The unnamed wait reports its own condition

- **WHEN** a test awaits `WaitUntilAsync` with a condition that never holds, and no budget
- **THEN** the `TimeoutException` message contains that call site's condition text, the applied
  budget and the elapsed time

#### Scenario: A caller-named budget is the budget reported

- **WHEN** a test passes a tighter budget to a wait that expires
- **THEN** the message reports that budget, not the helper's default

### Requirement: A timed-out wait reports how its own poll loop behaved

The failure message SHALL report the poll count and the longest interval between two consecutive
polls, together with the thread pool's state at the moment of failure, so that a genuine hang (many
polls, condition never true) is distinguishable from a starved runner (few polls, long gaps) without
a rerun.

#### Scenario: A starved poll loop is visible as a gap

- **WHEN** a wait expires after its polling was starved rather than after the condition was
  observed false many times
- **THEN** the message's poll count and longest gap show the starvation, and the thread pool counts
  are present

#### Scenario: A hang is visible as many polls

- **WHEN** a wait expires with a condition that stayed false while the poll loop ran normally
- **THEN** the message shows a high poll count with no long gap

### Requirement: A timed-out wait reports the session state its condition reads

The failure message SHALL carry the session state the waits in this helper test — at minimum
composition, running, bash-mode state, the input line and cursor, pending approval, queued
follow-ups, autocomplete, transcript size, alternate-screen state, scheduled work and the committed
scrollback — so an `IsComposed`-style timeout names the field that never turned true instead of
dumping unrelated frames.

#### Scenario: A composition timeout shows composition

- **WHEN** a wait on the session's composition state expires
- **THEN** the message contains the composition field and its value

#### Scenario: The snapshot covers every condition the helper waits on

- **WHEN** the helper's snapshot is rendered for a failure
- **THEN** every session property used by a wait condition in this test project is represented in it

### Requirement: A timed-out wait reports the process tree and its descriptors

The failure message SHALL include the process table for the test process and its descendants — pid,
parent pid, process group, state, wait channel and command — and, where the platform exposes them
(`/proc`), each process's open descriptors, so a child blocked on a read, a child that outlived its
redirect, or a pipe whose write end an unrelated process still holds is visible in the failure
itself.

#### Scenario: The tree is dumped with the state and wait channel

- **WHEN** a wait expires
- **THEN** the message lists the test process's descendants with pid, parent pid, process group,
  state, wait channel and command, and names the process the dump was taken from

#### Scenario: Descriptors are listed where the platform exposes them

- **WHEN** a wait expires on a platform that exposes per-process descriptors
- **THEN** the message lists the open descriptors of the processes in the tree

#### Scenario: A platform without a process table says so

- **WHEN** a wait expires on a platform where the process table cannot be read
- **THEN** the message states that the tree is unavailable instead of omitting the section silently

### Requirement: The diagnosis is bounded and never replaces one silent failure with another

Collecting the diagnosis SHALL be bounded by its own ceiling and SHALL be best-effort: a diagnostic
command that does not answer within that ceiling is reported as such, and no failure in collecting
evidence may hide the original timeout. The change SHALL NOT widen the wait budget to hide a hang.

#### Scenario: A diagnostic read that hangs is reported

- **WHEN** the process-table read does not answer within the diagnosis ceiling
- **THEN** the message says the diagnosis could not be collected, and the timeout is still thrown
  with everything else it knows

#### Scenario: The wait budget is unchanged

- **WHEN** the helper's unnamed budget is read
- **THEN** it is the cross-turn ceiling it was before this change, not a value raised to mask a hang
