# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16

## Review Summary

ADR review completed for this change. No major durable architectural decision was introduced and no
repository-level ADR file was created: retrying a leg that never acquired a hosted runner is a policy
detail inside the per-event gate and its reporter, not a new commitment about the loop, the tool
contracts, the protocols or the dependencies.

## In-Force ADRs Reviewed

- ADR-0037 — CI gate sized by event (reviewed): it records the per-event job set, the `ci-ok` aggregate
  and the red-main reporter. This change keeps the job set, the matrix, the budgets and the aggregate's
  fail-closed rule, and adds one bounded retry of a run whose legs never reached a machine; the reporter
  stays the same job with one more decision. ADR-0037 stays a record of what was true when it was
  written and is neither rewritten nor superseded.
- ADR-0005 and its addendum — startup budget calibration and the macOS floor rule (reviewed, not
  affected): the dedicated `perf (macos-latest)` leg keeps its budget, its trigger and its
  non-enforcement on a push; this change only lets that leg be retried when it never started.
- ADR-0008 — release publish flags (reviewed, not affected): no publish flag or release shape changes.
- ADR-0009 — memory budget gate (reviewed, not affected): the budget and its per-OS values are unchanged.
- ADR-0041 — net11 target frameworks (reviewed, not affected): no framework or toolchain change.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
