# ADR Review Manifest

- Status: completed
- Review date: 2026-11-12

## Review Summary

ADR review completed for this change: a selection fix inside `Lunate.Ai`. The change implements
the provider-family contract already accepted in ADR-0029 (a provider serves one API family per
endpoint, and the resolved family is what the pipeline keys behaviour on) and the provider-store
contract in ADR-0027; it introduces no new boundary, dependency or public contract.

## In-Force ADRs Reviewed

- ADR-0022 (generated model catalog, provider presets and API-key auth): unchanged — the catalog
  keeps declaring a reasoning style as model metadata; the wire shape is now keyed on the family.
- ADR-0027 (provider profiles, usages and capability facts): unchanged — `ModelProfile` fields and
  validation are untouched; the session thinking level remains the parameter that is applied.
- ADR-0029 (a provider is a credential plus one base URL per API family): applied — the resolved
  API family decides the reasoning wire parameter, exactly as it already decides the client,
  decorators and credential placement.
- ADR-0003 (the loop runs tools on `IChatClient`): checked as context — no loop change.

## New Durable ADRs Created

- None — no major durable architectural decision was introduced. `/adr/` gains no file; this
  manifest is the review record.
