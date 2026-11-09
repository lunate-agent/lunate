# ADR Review Manifest

- Status: completed
- Review date: 2026-11-10

## Review Summary

ADR review completed for this change.

The change corrects one check in profile resolution: a profile on a custom provider entry resolves
its model through the entry and is validated by the entry's API family instead of its adapter kind.
This completes the family authority ADR-0029 already established — the kind equality was the
leftover of the pre-family model — and adds no new boundary, dependency, contract or data model. It
does not meet the bar for a durable architectural decision.

## In-Force ADRs Reviewed

- ADR-0027 (user-scoped provider profiles, usages, and capability facts) — a profile still names one
  provider entry and one model; provider kinds remain templates, and the entry keeps its endpoint,
  credential reference and headers.
- ADR-0029 (a provider is a credential plus one base URL per API family) — decision 2, "the model
  carries the family", is what this change makes the check honour; family precedence and adapter
  selection are unchanged.
- ADR-0022 (model catalog and provider auth) — the catalogue stays the single source of model
  entries and wire ids; the change only decides which entry may supply them for a custom endpoint.
- ADR-0002 (layering and MEAI model types) — the work stays inside `Lunate.Ai`; no model type is
  added or replaced.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
