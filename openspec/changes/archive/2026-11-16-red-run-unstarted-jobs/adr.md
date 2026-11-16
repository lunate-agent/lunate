# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16

## Review Summary

ADR review completed for this change: `red-run-unstarted-jobs`, the red-run reporter's rule that a job
no runner ever acquired is not a red job.

## In-Force ADRs Reviewed

The change touches no budget, matrix size, runner label, workflow trigger or permission, so none of
these is revisited:

- `adr/0005-budget-calibration.md` and `adr/0005-addendum-startup-floor.md` — startup budgets are
  re-calibrated from post-merge runs, never guessed; #922 changed no number.
- `adr/0037-ci-gate-sized-by-event.md` — which legs run on which event decides the matrix, not this
  reporting predicate.
- `adr/0041-net11-target-frameworks.md` — the startup and memory budgets are re-measured on .NET 11 as a
  post-merge maintainer step; unaffected here.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced. The change refines one predicate
  inside the existing "Main-run failure reporting" requirement; it establishes no pattern, boundary,
  technology or contract that later changes would cite.
