# ADR Review Manifest

- Status: completed
- Review date: 2026-11-10

## Review Summary

ADR review completed for this change. The change makes the existing process-isolation requirement
executable: it distinguishes a proven group exit from a failed or unproven probe. It introduces no
new architectural pattern, contract, dependency or boundary.

## In-Force ADRs Reviewed

- ADR-0003 (own loop on `IChatClient`) — unchanged; cancellation cleanup stays inside the runner.
- ADR-0012 (tool contract) — unchanged; no `ITool`, `ToolContext` or result-shape change.
- ADR-0016 (workspace boundary) — unchanged; mounts are untouched.
- ADR-0021 (per-run workspaces) — unchanged; container identity and removal remain per root.
- ADR-0024 (bash guard) — unchanged; the host guard and its policy boundary are untouched.
- ADR-0034 (POSIX child descriptor isolation) — unchanged; no spawn or descriptor change.

## New Durable ADRs Created

None — no major durable architectural decision was introduced. The process-only container boundary
and its BashGuard policy question remain as recorded by the archived container tool runner change and
its maintainer review; this change does not reopen or decide that boundary.
