# ADR Review Manifest

- Status: completed
- Review date: 2026-11-11

## Review Summary

No durable architectural decision is introduced. The change corrects two path references inside the
`extensions` spec — onto the `docs/dev/` homes the S4 move created — and narrows the documentation
checker's prefix lists to the paths that exist. No dependency, public contract, storage format or
security boundary changes, and no new ADR file is written.

## In-force Context Reviewed

- `adr/0017-extensibility-architecture.md` — the extensibility contract whose spec text carries the
  first path, and the record that keeps the pre-move path it was written with.
- `docs/dev/information-architecture.md` — the signed-off 0.4.0 layout, its old-path table and the
  ruling that maintainer pages get no stubs.

## Decision

The spec names the paths that exist today; the records keep the paths that were true when they were
written. The checker's prefix lists describe today's tree, so the pre-move prefixes leave them.
