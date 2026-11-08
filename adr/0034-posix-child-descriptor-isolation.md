# ADR-0034: A POSIX child inherits only the descriptors its own spawn names

- Status: accepted — 2026-11-08 (maintainer guidance on #699; fixes the product-side hang found in #660)
- Context: `UnixChildProcess.Spawn` (`src/Lunate.Coding/ChildProcess.Unix.cs`) — the POSIX half of the process contract pinned by ADR-0024's guard and the `process-isolation` spec
- Supersedes: nothing; amends the spawn mechanics of ADR-0024 and the `process-isolation` implementation without changing the guard or the tool contract

## Context

`UnixChildProcess.Spawn` created its captured-pipe ends with `HandleInheritability.Inheritable`,
which clears `FD_CLOEXEC`, and closed the parent's copies only after `posix_spawn` returned.
`posix_spawn` copies every descriptor that is not close-on-exec into the new child, so two spawns
that overlapped in that window handed each other their write ends. The first child exited, the
second still held its stdout/stderr write end, the reader never saw EOF, and `ProcessRunner` — which
awaits the readers after `WaitForExitAsync` — never returned. In the product that is a hung bash
tool, ACP/MCP teardown or editor round-trip; in the test suite it is a wait that burns its whole
budget (#660). A concurrent spawn is normal: more than one tool call can be in flight, and a host
child can outlive a bash run.

## Considered Options

- **Keep inheritable handles and close the parent's copies before `posix_spawn`.** The file actions
  duplicate the ends for the child, but the window still exists between pipe creation and that close,
  and a concurrent `fork`/`posix_spawn` can still observe the handles. Rejected: it narrows the race
  instead of removing it.
- **A process-wide spawn lock.** Serialising every spawn would trade the hang for a throughput
  problem and hide the descriptor defect. Rejected explicitly by the maintainer's guidance.
- **Close every descriptor the spawn does not own in the child.** Portable POSIX has no such file
  action; glibc's `addclosefrom_np` and Darwin's `POSIX_SPAWN_CLOEXEC_DEFAULT` are not available on
  both platforms. Rejected as the sole mechanism: it would be two disjoint implementations with a
  gap on musl.
- **Create the ends close-on-exec and hand the child its descriptors through the file actions.**
  Chosen: one mechanism works on Linux (`pipe2`/`FD_CLOEXEC`) and macOS (`FD_CLOEXEC`), with
  Darwin's `POSIX_SPAWN_CLOEXEC_DEFAULT` as defence in depth.

## Decision

**A POSIX child Lunate spawns inherits only the descriptors its own spawn names; no descriptor
another spawn created may be visible to it.**

- The three captured pipes are created with `HandleInheritability.None`, which leaves close-on-exec
  set on the child end from creation (the .NET Unix implementation's `pipe2(..., O_CLOEXEC)` route).
  The child still receives its ends because `posix_spawn` file actions run before `exec` and
  `dup2` clears close-on-exec on the target 0/1/2.
- On Darwin the spawn flags also set `POSIX_SPAWN_CLOEXEC_DEFAULT` (0x4000, `<sys/spawn.h>`), so the
  child keeps only the descriptors named in its file actions. Linux has no equivalent flag; there
  the close-on-exec handles are the guarantee.
- `POSIX_SPAWN_SETSID`, the `/dev/null` or line-pump stdin, the captured stdout/stderr, `addchdir`,
  the explicit `argv`/`envp` pointers and the error messages stay as they were. The spawn ABI's
  observable behaviour does not change; only the descriptor set it leaves behind does.
- No global spawn lock, no timeout change, no `ProcessRunner` reordering.

## Consequences

- A finished child's captured stream reaches EOF even while an unrelated child spawned at the same
  instant is alive; the bash tool, host teardown and editor round-trip stop hanging on the leftover
  writer.
- Future edits to the spawn path must keep the invariant: a new handle must be close-on-exec, and
  the child must receive it through a file action, never by inheritance.
- The regression is pinned by overlapping-spawn tests for stdout and stderr and by a child
  descriptor-list test, plus the existing spawn/redirect/exit-code/`setsid`/stdin suites.
- Costs nothing when only one spawn runs; no serialisation and no extra syscall on the steady path.
