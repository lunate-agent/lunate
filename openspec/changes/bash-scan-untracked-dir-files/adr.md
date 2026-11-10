# ADR Review Manifest

- Status: completed
- Review date: 2026-11-10

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0017 — Extensibility architecture: the file change bus and its limits are an existing extension contract; this change corrects the bash scan's untracked handling without moving the contract's shape.
- ADR-0024 — The bash guard: unchanged; this change only reads git status, it does not touch process isolation or approvals.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced. Choosing `-uall` over a hand-rolled directory walk is a tactical implementation decision inside an existing scan, recorded in this change's design.md.
