# ADR Review Manifest

- Status: completed
- Review date: 2026-11-21

## Review Summary

ADR review completed for this change. The tag-time check is a release-process script mode: it reads
the tree the release already ships and establishes no pattern, boundary, protocol or dependency a
future change has to honour. Nothing here meets the bar for a durable architectural decision, so no
repository-level ADR was created.

## In-Force ADRs Reviewed

- ADR-0002 — layering and Microsoft.Extensions.AI model types, including its addendum that
  `ContractApiVersion` and the preview packages derive from `<Version>`. The check compares `<Version>`
  literally, so the derived versions are not revisited and a prerelease tag is still spelled the same
  on both sides.
- ADR-0011 — CSharpier, markdownlint and the documentation map. The runbook stays the one home that
  describes the release, and the touched Markdown has to pass the documentation lint.
- ADR-0037 — the CI gate sized by event. No job, leg or profile changes: the existing tag-time gate
  step gains one argument.
- ADR-0041 — net11.0 target frameworks. Untouched: no C# is involved.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
