# ADR Review Manifest

- Status: completed
- Review date: 2026-06-11

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- `adr/0022-model-catalog-and-provider-auth.md` — provider credentials and endpoints; the probe stays
  unauthenticated.
- `adr/0029-provider-api-families.md` — decision 3 already states `User-Agent: lunate/<version>` is
  always sent on every provider path including discovery; this change applies it to the reachability
  probe rather than diverging from it.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced. The change implements an existing
  accepted decision for one remaining path.
