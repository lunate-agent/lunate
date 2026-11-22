# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The change introduces no durable architectural decision of
its own: it applies the lock-file claim already recorded in ADR-0017's extensions addendum and
already implemented by `Lunate.Ai.FileLock` — and already applied to `settings.json` and
`providers.json` by the archived `cross-process-store-locks` change — to the third Lunate-owned
store and to the setup wizard's commit. No supersession is needed and no prior ADR is contradicted.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the store code stays in `Lunate.Coding` and only
  uses the internal `Lunate.Ai.FileLock`; no reference is added upward.
- `adr/0015-session-format.md` — the single-writer claim is a sibling pattern, not changed.
- `adr/0017-addendum-extensions.md` — records the lock-file claim (`FileShare.None` on a sidecar)
  that this change reuses.
- `adr/0022-model-catalog-and-provider-auth.md` — the user-scoped auth store keeps its file shape,
  location and owner-only permission; only its write path gains the claim.
- `adr/0027-provider-store-model-profiles-and-usages.md` — the provider store's file shape and write
  path are unchanged.
- `adr/0041-net11-target-frameworks.md` — no target-framework or packaging change.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
