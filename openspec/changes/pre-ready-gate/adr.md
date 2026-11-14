# ADR Review Manifest

- Status: completed
- Review date: 2026-11-14

## Review Summary

ADR review completed for this change. No major durable architectural decision is introduced: the
change adds an advisory pull-request job and a stdlib decision script beside the existing CI, follows
the repository's committed-script-with-tests form, and changes no product boundary, public contract,
dependency, stored format or release path. No repository-level ADR is created.

## In-Force ADRs Reviewed

- `adr/0011-repo-quality-tooling.md` — the committed decision script with a self-test and a
  `verify.sh` entry is the form this change follows; the job is a process check, not a new tool.
- `adr/0005-budget-calibration.md`, `adr/0005-addendum-startup-floor.md` and
  `adr/0009-memory-budgets.md` — the verification matrix, budgets and profile selection are
  unchanged; the new job runs no build and measures nothing.
- `adr/0008-release-publish-flags.md` — no build or release workflow change.
- `adr/0015-session-format.md` and `adr/0002-layering-and-meai-model-types.md` — no session, model
  or project-layer change.

No supersession is needed; no in-force ADR is contradicted.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
