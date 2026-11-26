# ADR Review Manifest

- Status: completed
- Review date: 2026-11-26

## Review Summary

ADR review completed for this change. It corrects the `agent-tools` specification so its cap-marker
sentence matches the already-shipped `grep`, `find`, `ls` and `semantic_search` behaviour. No code,
dependency, boundary, protocol or session-format decision changes, so no new repository-level ADR
is warranted.

## In-Force ADRs Reviewed

- `adr/0003-loop-own-vs-maf-harness.md` — the harness still owns tool execution and its shared
  truncation path; this change only names that path's marker in the spec.
- `adr/0016-workspace-boundary.md` — the search tools' workspace and `.gitignore` boundary is
  unchanged; the delta preserves the existing requirement text around it.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced. The marker contract belongs to
  the `agent-tools` specification and its owning change, not to a new ADR.
