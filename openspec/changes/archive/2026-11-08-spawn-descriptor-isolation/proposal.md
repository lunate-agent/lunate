## Why

A POSIX spawn creates its pipe ends inheritable and closes the parent's copies only after
`posix_spawn` returns. A second spawn that begins inside that window inherits the first spawn's
stdout/stderr write ends; when the first child exits the pipe still has a writer, so its reader never
sees EOF and `ProcessRunner.RunAsync` waits forever. This is the product-side cause of the `#660`
hang — the bash tool, ACP/MCP host teardown, the editor round-trip and any test waiting on the
resulting frame all burn their budget on it — and it blocks the 0.3.0 release (`#699`).

## What Changes

- POSIX pipe ends are created close-on-exec from the start (the child-side handles no longer request
  inheritance), and the child receives its descriptors explicitly through the spawn file actions
  (`adddup2`/`addopen`), so no pipe end is reachable through a plain `exec`.
- On Darwin the spawn also sets `POSIX_SPAWN_CLOEXEC_DEFAULT`, so the child inherits only the
  descriptors its own file actions name. Linux keeps the close-on-exec handles (`pipe2`/`FD_CLOEXEC`
  route), since glibc/musl have no equivalent spawn flag.
- The spawn code states the invariant it now guarantees: no descriptor another spawn created may be
  visible to this child.
- New regression tests: an overlapping-spawn repro for stdout and for stderr (the victim reaches EOF
  while the long-lived bystander is still alive), and a descriptor-list assertion that a child holds
  no pipe end it was not given. The existing spawn, redirect, exit-code, `setsid` and stdin tests
  stay green.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `process-isolation`: a POSIX tool child inherits only the descriptors its own spawn names; another
  spawn's pipe ends are never visible to it, so a finished child's captured stream reaches EOF even
  while a concurrent child is alive.

## Impact

- Code: `src/Lunate.Coding/ChildProcess.Unix.cs` (pipe creation and spawn flags only). Windows uses
  redirected handles and job objects and is unchanged.
- Tests: `UnixChildProcessTests` (new leak and descriptor tests), with `ProcessRunnerTests`
  re-run because it consumes the same spawn.
- No public API change, no new package, no session-format change, no timeout change.

Refs #699. Fixes the product-side hang found while working #660.
