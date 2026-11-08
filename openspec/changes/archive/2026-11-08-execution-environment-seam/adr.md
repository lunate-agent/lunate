# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change. The change introduces an internal, reversible refactor seam;
it does not establish a new durable architecture commitment, public contract, security boundary or
supersession. No repository-level ADR is created.

## In-Force ADRs Reviewed

- ADR-0003 — the agent loop runs tools itself (unchanged; approvals and cancellation stay in the
  loop).
- ADR-0012 — tool contract (unchanged; no public tool type changes).
- ADR-0013 — agent loop (unchanged; cancellation still ends the tool call's work).
- ADR-0016 — workspace boundary (unchanged; file tools stay on host filesystem access).
- ADR-0017 and its addenda, including the remote-host addendum — extensibility boundary and the
  `IProcessRunner` transport (unchanged; `ProcessRunner` remains the public seam implementation).
- ADR-0021 — per-run workspaces (unchanged; the working-directory mapping is one absolute path).
- ADR-0024 — bash guard (unchanged; the guard keeps wrapping the host half).
- ADR-0025 — verified edits (unchanged; verification stays host-side).
- ADR-0026 — Roslyn built-in boundary (unchanged; no new process path).
- ADR-0027/0029/0030/0031/0032/0033 — provider and decision-layer records reviewed; not affected.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced. The seam is internal to
  `Lunate.Coding`, has local execution as its only implementation, and can be reverted without a
  stored-state, API or protocol migration. Container selection and its security boundary remain
  #627's proposal, not this change.
