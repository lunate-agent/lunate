# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

No major durable architectural decisions were introduced and no new repository-level ADR files were
created. This change refines the existing setup-wizard interaction inside the accepted setup and
provider decisions; it adds no dependency, public contract, storage format or security boundary.

## In-force Context Reviewed

- `adr/0014-csharp-backend-interface.md` — the interaction components the wizard uses.
- `adr/0027-provider-store-model-profiles-and-usages.md` — provider entries, profiles and usages.
- `adr/0028-decision-layer-primitive.md` — the decisions layer the quick mode leaves off.
- `adr/0029-provider-api-families.md` — custom provider family and placement defaults.
- `adr/0030-native-decisions.md` — native/adapter decision profiles.

## Decision

Quick and detailed are two depths of one walk, not a new architecture. The mode is wizard state, the
commit path and stores are unchanged, and the left-at-default block only reports defaults the
existing settings already define.
