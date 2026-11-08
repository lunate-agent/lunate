# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- `adr/0005-budget-calibration.md` — accepted 2026-10-04; mentions the current `concurrency`
  setting in passing and constrains the gate's budgets, which this change does not touch.
- `adr/0011-repo-quality-tooling.md` — accepted 2026-10-06; owns the quality-tooling and CI
  conventions the self-test extension follows.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced. The change is a CI configuration
  correction plus a test of that configuration; it establishes no pattern, boundary or contract that
  outlives the concurrency block.
