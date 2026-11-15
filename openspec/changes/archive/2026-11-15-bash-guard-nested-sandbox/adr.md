# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0012 — Tool contract and declaration adapter: the notice stays in the `bash` tool's own
  result/log channel; no tool-contract shape changes.
- ADR-0016 — Workspace boundary for file tools: unaffected (`bash` is intentionally not
  boundary-checked).
- ADR-0024 — The bash guard: its "detected and reported, never assumed" rule is the one this change
  applies to the nested-sandbox probe failure; the guard profile, mechanisms and refusal/hint design
  are unchanged.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced. The change corrects the report of
  an existing branch and introduces no new dependency, boundary or contract.
