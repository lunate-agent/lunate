# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

The change extends an existing wizard flow to collect more than one provider run. It adds no store
schema, no public API, no dependency and no new interaction primitive. The provider entry,
credential, profile and usage shapes are the ones ADR-0027 already governs, and the one-writer rule
is preserved by looping inside the existing commit transaction. No repository-level ADR is needed.

## In-Force ADRs Reviewed

- ADR-0002 (layering and ME.AI model types): unaffected — no new layer or model type.
- ADR-0003 (the agent owns the loop): unaffected — the wizard is not a model turn.
- ADR-0007 (live-area concurrency): unaffected — the loop reuses the existing picker/prompt tasks.
- ADR-0022 (model catalog and provider auth): reused unchanged.
- ADR-0027 (provider store, model profiles and usages): the same writers and shapes, called once
  per collected provider.
- ADR-0029 (provider API families): a renamed preset keeps its family and placement through
  `PresetId`; no family contract changes.

## New Durable ADRs Created

- None - the decision (one active run plus a sealed list, committed in one pass) is an
  implementation detail of the setup-wizard spec.
