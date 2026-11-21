# ADR Review Manifest

- Status: completed
- Review date: 2026-11-21

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0003 (the loop runs tools) — not touched: the change is a release-tooling script, no agent loop.
- ADR-0011 (repo quality tooling) — the in-force decision on what the repository's own scripts and
  their gates look like; the new `--self-test` mode follows the pattern that ADR's tooling established
  (`scripts/changelog-guard`, `scripts/docs-check`).
- ADR-0037 (CI gate sized by event) — the verify profile gains one `--self-test` invocation beside
  the assembler's suite; no job or profile changes shape.
- ADR-0041 (net11 target frameworks) — untouched: the change is Python stdlib only.

No in-force ADR constrains how a changelog fragment's relative links are resolved, and none is
revisited.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced. The link rewrite is a change-local
  fix to one release script, recorded in this change's `design.md`.
