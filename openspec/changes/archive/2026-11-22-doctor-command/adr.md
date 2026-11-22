# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The full `adr/` folder was read and its supersession links
were followed. The command reuses existing boundaries and adds no long-term architectural
commitment, so no repository-level ADR is created.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the command stays in `Lunate.Coding`, below the
  project graph and behind the existing Microsoft.Extensions.AI types.
- `adr/0022-model-catalog-and-provider-auth.md` — provider presets and API-key auth are read through
  the existing store, never re-implemented.
- `adr/0027-provider-store-model-profiles-and-usages.md` — provider entries and profiles keep their
  existing resolution order.
- `adr/0029-provider-api-families.md` — the report names a provider entry, not a second family
  model.
- `adr/0035-tool-environment-pass-through.md` and
  `adr/0036-tool-child-credential-shape-strip.md` — the report names a credential's presence and
  source, never its value.
- `adr/0041-net11-target-frameworks.md` — the command targets `net11.0` like every project.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
