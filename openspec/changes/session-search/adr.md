# ADR Review Manifest

- Status: completed
- Review date: 2026-11-23

## Review Summary

ADR review completed for this change. Session search is a read-only query over the session store the
repository already owns: it adds no pattern, boundary, technology or contract that a future change
would have to honour. The scan reuses `SessionFormat.Parse` and the store's own enumeration, stays
internal to `Lunate.Agent`, and leaves the session file format and the picker's existing lifecycle
alone. No supersession is needed and no prior ADR is contradicted.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the scan lives in `Lunate.Agent`; `Lunate.Coding`
  consumes it through its existing friend-assembly edge, never the other way.
- `adr/0015-session-format.md` — the JSONL format, its schema and its append-only rule are unchanged;
  search only reads, and opening a hit reuses the existing `activeLeaf` record.
- `adr/0016-workspace-boundary.md` — the scan reads only the sessions root under the state root, and
  searching writes nothing into a workspace.
- `adr/0007-live-area-concurrency.md` — the picker search stays inside the one-owner picker loop:
  the scan runs on that consumer, never on the live area's key pump.
- `adr/0013-agent-loop.md` and `adr/0012-tool-contract.md` — no loop step and no tool is added.
- `adr/0041-net11-target-frameworks.md` — no target-framework or packaging change; no new NuGet
  package.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
