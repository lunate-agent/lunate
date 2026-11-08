# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change. It alters how the POSIX child is started, which is the
platform implementation of the process contract already accepted for tools (ADR-0003, ADR-0012,
ADR-0013) and composes with the bash guard (ADR-0024) without changing the guard. Because the spawn
descriptor rule is a durable commitment future changes must not regress, and the issue names the
spawn ABI directly, this change records it as a new repository-level ADR.

## In-Force ADRs Reviewed

- ADR-0003 (own loop on `IChatClient`): unchanged — the loop still owns tool execution and
  cancellation; this change only stops a child from holding another spawn's write ends.
- ADR-0012 (tool contract): unchanged — `ITool`/`ToolContext`/`ToolResult` untouched.
- ADR-0013 (agent loop behaviour): unchanged — no event or recording behaviour changes.
- ADR-0024 (bash guard): unchanged — the guard's mechanism, profile and probe cadence stay exactly
  as accepted; the spawn below it keeps `POSIX_SPAWN_SETSID`, its file actions and its explicit
  pointer contract, and only the pipe handles' inheritability and one Darwin flag change.
- ADR-0034 (new): records the descriptor-inheritance rule.

## New Durable ADRs Created

- `adr/0034-posix-child-descriptor-isolation.md` — a POSIX child inherits only the descriptors its
  own spawn names; pipe ends are close-on-exec and Darwin additionally sets
  `POSIX_SPAWN_CLOEXEC_DEFAULT`.
