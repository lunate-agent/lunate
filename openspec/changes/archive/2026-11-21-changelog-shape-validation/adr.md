# ADR Review Manifest

- Status: completed
- Review date: 2026-11-21

## Review Summary

ADR review completed for this change. The shape check is a release-process script's reading of one
file: it adds named refusals and a write-time post-condition to `scripts/changelog-assemble` and
establishes no pattern, boundary, protocol or dependency a future change has to honour. Nothing here
meets the bar for a durable architectural decision, so no repository-level ADR was created.

## In-Force ADRs Reviewed

- ADR-0011 — CSharpier, markdownlint and the documentation map. The touched Markdown is this change's
  own artifacts; the repository's documentation lint still decides their shape.
- ADR-0037 — the CI gate sized by event. No job, leg or profile changes: the same commands run the
  same script, which now refuses one more kind of file.
- ADR-0041 — net11.0 target frameworks. Untouched: no C# is involved.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
