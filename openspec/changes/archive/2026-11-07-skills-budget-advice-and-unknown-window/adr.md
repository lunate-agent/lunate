# ADR Review Manifest

- Status: completed
- Review date: 2026-11-07

## Review Summary

ADR review completed for this change. It records one durable decision revising an in-force ADR: an unknown model context window disables the skills index budgets (and the notices name only `skills.indexBudget`).

## In-Force ADRs Reviewed

- ADR-0031 (addendum, skills index budget) — its fallback clause ("… else the 128,000-token default") is superseded by ADR-0033; its fractions, ceiling semantics and `skills.indexBudget` decisions stay in force.
- ADR-0032 (addendum, description spec limit) — untouched by this change.
- ADR-0003 (loop owns tools) — unchanged, respected.

## New Durable ADRs Created

- `adr/0033-addendum-unknown-window-disables-skills-budgets.md` — unknown window disables both budgets, notices name the setting only.
