# ADR Review Manifest

- Status: review requested — security-boundary sign-off pending
- Review date: 2026-11-08

## Review Summary

This change composes with accepted ADR-0024 (the bash guard) rather than replacing it: the guard
continues to wrap the host process the tool spawns — now the container engine's `exec` client — and
the pre-run redirection scan still runs on the model's command text. The new part is a filesystem
boundary for the inner command: the container mounts only the workspace.

The boundary question the card opens is one-way and is **not** answered here. Whether the container
filesystem boundary replaces BashGuard for inner commands, or an in-container guard is required, is
requested from the maintainer on the pull request, which carries `needs-signoff`. Until that sign-off
lands, the implementation claims only what it does: the host guard protects the host half, and the
inner command is confined by the mount, not by BashGuard.

## In-Force ADRs Reviewed

- ADR-0003 (own loop on `IChatClient`): unchanged — the loop, approvals and file-change bus stay on
  the host; only the `bash` tool's process location changes.
- ADR-0012 (tool contract): unchanged — `ITool`/`ToolContext`/`ToolResult` and the risk/approval
  contract are untouched.
- ADR-0016 (workspace boundary): upheld — container mounts are limited to the workspace root; the
  state root, install prefix and `auth.json` are never mounted.
- ADR-0021 (per-run workspaces): upheld — a child run in another worktree gets its own container
  mounted at that root, never the parent's container with the wrong working directory.
- ADR-0024 (bash guard): unchanged and still in force. The guard wraps the engine client on the host
  and its refusal/hint text is preserved. No claim is made that it constrains the inner command.
- ADR-0013 (agent loop behaviour): unchanged.
- ADR-0019 (xenoatom terminal UI): unchanged; the header gains one environment segment.

## New Durable ADRs Created

- None yet. If the maintainer decides the container boundary is the accepted protection for inner
  commands, that decision needs a new ADR (or an ADR-0024 addendum) and is recorded in the follow-up
  pull request; if an in-container guard is required, the guard design is the follow-up. This change
  deliberately does not pre-empt that decision.
