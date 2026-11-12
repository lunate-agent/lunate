# ADR Review Manifest

- Status: completed
- Review date: 2026-11-12

## Review Summary

No major durable architectural decision is introduced. This change adds release-process tooling —
a per-pull-request fragment, an assembler, and a CI job — and modifies no product boundary, data
format, dependency or public contract. No repository-level ADR file is created.

## In-force Context Reviewed

- `adr/0011-repo-quality-tooling.md` — markdownlint scope and the documentation map the new files
  join; the fragment directory is added to the lint scope rather than exempted.
- `adr/0008-release-publish-flags.md` — the release build and asset contract stays untouched; the
  only release-workflow change is a changelog check before the existing gate.
- `adr/0005-budget-calibration.md` and `adr/0009-memory-budgets.md` — no build, publish or budget
  path is changed.

## Decision

The fragment, the assembler and the guard are process plumbing inside the existing CI and release
workflows. They are reversible by reverting the files, add no dependency, and do not constrain a
future architecture choice, so recording them as a durable ADR would add process weight without a
decision future changes must honour. The one durable rule — a pull request writes a fragment, never
the changelog — belongs in `AGENTS.md`, which is protected; its exact proposed edit is quoted in
`design.md` for maintainer approval.
