# ADR Review Manifest

- Status: completed
- Review date: 2026-11-16

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0037 — the CI gate is sized by the event (the change adds steps to the existing `platform` and
  `tree-checks` jobs; the event-to-job table and the `ci-ok` job set are unchanged)
- ADR-0011 — repository quality tooling (the docs lint and formatting tools are untouched; the guard is
  a repository script, not a new tool)
- ADR-0041 — one target framework (the guard's subject, `TargetFrameworkTests`, is not edited)

No in-force ADR constrains the guard's shape or its placement. ADR-0037 already places the
platform/docs checks on a pull request and the tree checks on the merge queue and a push to `main`;
this change only fills those lists with a check that was missing from them.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
