# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0037 — CI gate sized by event (the gate per event; its job set and the `ci-ok` aggregate are unchanged)
- ADR-0001 — self-contained single-file releases with CI budgets (not affected)
- ADR-0005 and its addendum — startup budget calibration and the macOS floor rule (not affected)
- ADR-0009 — memory budget gate (not affected)

ADR-0037 records that "a red nightly reports itself" as a property of the per-event gate; this change
extends the same reporter to a push to `main` and retargets its milestone and area label. That is a
policy detail inside the reporter, not a new architectural commitment, so ADR-0037 stays a record of
what was true when it was written and is not rewritten or superseded.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
