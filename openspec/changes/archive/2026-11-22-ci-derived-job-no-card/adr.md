# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change: `ci-derived-job-no-card`, the red-run reporter's rule that the two
jobs whose result is derived from the others' (`ci-ok`, `red-main-issue`) are not filed as their own cards
beside a job that ran.

## In-Force ADRs Reviewed

The change touches no budget, matrix size, runner label, workflow trigger or permission, so none of these
is revisited:

- `adr/0037-ci-gate-sized-by-event.md` — which legs run on which event decides the matrix; the job set and
  the `ci-ok` aggregate are unchanged, and so is its verdict.
- `adr/0005-budget-calibration.md` and `adr/0005-addendum-startup-floor.md` — startup budgets are
  re-calibrated from post-merge runs, never guessed; this change measures nothing.
- `adr/0041-net11-target-frameworks.md` — no target framework, publish shape or budget is affected.

## New Durable ADRs Created

- None - no major durable architectural decision was introduced. The change refines one predicate inside
  the existing "Main-run failure reporting" requirement; it establishes no pattern, boundary, technology
  or contract that later changes would cite.
