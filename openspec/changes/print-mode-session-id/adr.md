# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change. It adds one launch flag and one session-selection path: the
id becomes a file name in the working directory's session directory, and creation reuses the
existing provisioning and session-header machinery. No long-term architectural commitment (pattern,
technology, boundary, contract) changes, so no repository-level ADR is created.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the parser and selection stay in `Lunate.Coding`;
  the only new session seam is internal to `Lunate.Agent`, and no model type or project reference
  changes.
- `adr/0003-loop-own-vs-maf-harness.md` — the run loop is untouched; the session id is already
  stamped on the events it emits.
- `adr/0015-session-format.md` — the session format is untouched; the caller's id is the header id
  the format already carries, and no golden file changes.
- `adr/0016-workspace-boundary.md` — the id resolves inside the working directory's own session
  directory, so a same-named session in another worktree is neither read nor overwritten; the
  explicit-id creation overload stays internal, so `PublicAPI.Unshipped.txt` is unchanged.

## New Durable ADRs Created

- None — no major durable architectural decision was introduced.
