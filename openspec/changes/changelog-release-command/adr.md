# ADR Review Manifest

- Status: completed
- Review date: 2026-11-14

## Review Summary

ADR review completed for this change. `--release` is a release-process script mode: it performs the
edits `docs/dev/distribution.md` step 1 already described and establishes no pattern, boundary,
protocol or dependency that a future change has to honour. Nothing here meets the bar for a durable
architectural decision, so no repository-level ADR was created.

## In-Force ADRs Reviewed

- ADR-0002 — layering and Microsoft.Extensions.AI model types, including its addendum that
  `ContractApiVersion` and the preview packages derive from `<Version>`. The mode bumps `<Version>`
  only; the derived versions follow, so the ADR is honoured rather than revisited.
- ADR-0011 — CSharpier, markdownlint and the documentation map. The promoted section stays valid
  Markdown and `docs/dev/distribution.md` remains its one home.
- ADR-0037 — the CI gate sized by event. The tag-time `scripts/changelog-assemble --check` step is
  unchanged; `--release` makes it pass rather than replacing it.
- ADR-0041 — net11.0 target frameworks. Untouched: no C# is involved.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
