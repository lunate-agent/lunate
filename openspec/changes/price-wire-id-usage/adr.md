# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change. The change adds one internal catalog lookup and routes two
existing price lookups through it. It introduces no architectural pattern, boundary, protocol or
dependency: `ModelCatalog` keeps owning id resolution, `Lunate.Agent` keeps owning spend accounting,
`Lunate.Coding` keeps owning the JSON serialization, and no public API or session format changes.
Nothing here meets the bar for a new durable ADR.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the lookup stays inside `Lunate.Ai` and uses no new
  model types.
- `adr/0003-loop-own-vs-maf-harness.md` — spend accounting stays in the harness, where the loop tests the limit.
- `adr/0015-session-format.md` — the change deliberately does not alter the model id a
  session records.
- `adr/0022-model-catalog-and-provider-auth.md` — the catalog remains the only price source.
- `adr/0027-provider-store-model-profiles-and-usages.md` — provider-scoped resolution of a model id
  is already the accepted rule; this change reuses it for pricing.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
