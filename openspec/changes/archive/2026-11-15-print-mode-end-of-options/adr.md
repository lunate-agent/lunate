# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change. The change is a launch-parser behaviour: it adds the
conventional `--` end-of-options marker and keeps two pre-dispatch scans from reading past it. No
long-term architectural commitment (pattern, technology, boundary, contract) changes, so no
repository-level ADR is created.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the parser stays in `Lunate.Coding`; no project
  reference and no model type is touched.
- `adr/0003-loop-own-vs-maf-harness.md` — the run loop is untouched; the marker only decides what
  the prompt is.
- `adr/0013-agent-loop.md` — the loop's step ceiling and events are untouched.
- `adr/0015-session-format.md` — the session format is untouched; the prompt is an ordinary user
  message either way.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
