# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change. The change introduces no durable architectural decision of
its own: it applies the lock-file claim already recorded in ADR-0017's extensions addendum and
already implemented by `Lunate.Ai.FileLock` to two more Lunate-owned stores. No supersession is
needed and no prior ADR is contradicted.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the store code stays in its existing projects.
- `adr/0015-session-format.md` — the single-writer claim is a sibling pattern, not changed.
- `adr/0017-addendum-extensions.md` — records the lock-file claim (`FileShare.None` on a sidecar)
  that this change reuses.
- `adr/0022-model-catalog-and-provider-auth.md` — the user-scoped provider/auth stores are not
  redefined.
- `adr/0027-provider-store-model-profiles-and-usages.md` — the provider store's file shape and
  write path are unchanged.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
