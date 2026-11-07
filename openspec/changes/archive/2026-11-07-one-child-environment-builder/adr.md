# ADR Review Manifest

- Status: completed
- Review date: 2026-11-07

## Review Summary

ADR review completed for this change. The change is a consolidation of an implementation that already
sat inside accepted architecture: ADR-0012 (the tool contract, where `ProcessRunRequest` and the
`core/process` capability live), ADR-0024 (the bash guard, whose state-root deny this change
complements by keeping Lunate's provider credentials out of a tool child's environment), and
ADR-0017's extension architecture (the remote host's scrubbed environment, unchanged). The
maintainer's ruling on one builder with two policies is recorded here and in the change's design;
no new durable architectural commitment is introduced, so no repository-level ADR is created.

## In-Force ADRs Reviewed

- ADR-0012 (tool contract): unchanged — `ITool`, `ToolContext` and `ToolResult` are untouched; only
  where the tool child's environment comes from changes.
- ADR-0024 (bash guard): unchanged and complemented — the guard already denies reading `auth.json`;
  stripping Lunate's resolved credential variables extends the same "a model-chosen command cannot
  read the user's keys" boundary to the environment.
- ADR-0017 / addendum extensions: unchanged — the remote extension host keeps `Scrubbed` with
  `InheritEnvironmentVariables = false`, now naming the policy at its call site.
- ADR-0003 (own loop) / ADR-0013 (agent loop): unchanged.

## New Durable ADRs Created

- None — the single-builder rule already exists in the extensions capability's requirement ("the
  shared builder … one implementation"); this change extends that rule to the second policy and the
  tool child rather than deciding a new architectural pattern.
