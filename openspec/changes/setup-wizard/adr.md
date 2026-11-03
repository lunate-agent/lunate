# ADR Review Manifest

- Status: completed
- Review date: 2026-11-02

## Review Summary

The setup wizard is a consumer of shipped schemas and approved store contracts: it writes provider
entries, credentials, profiles and usages through the existing `AuthStore`, provider-store and
`SettingsStore` writers, and its one new interaction surface (the wizard flow and `lunate setup`
/ `/setup` entry points) is behaviour specified in the change's spec deltas, not a durable
architectural decision. The card scopes an ADR to a settings-shape change; the profile schema
(`profiles.*.reasoningLevel` per #476) and the provider entry shape (#474 / ADR-0029, itself
pending sign-off) are already approved or tracked there, so this change creates no repository-level
ADR.

## In-Force ADRs Reviewed

- ADR-0002 (layering, ME.AI model types): the wizard lives in `Lunate.Coding`/`Lunate.Tui` and adds
  no model type and no new layer crossing.
- ADR-0003 (the agent owns the loop): untouched — the wizard runs in the shell interaction layer,
  never inside a model turn.
- ADR-0007 (live-area concurrency): the wizard reuses the picker/prompt interaction pattern the auth
  picker already runs there; the card confirms the wizard inherits its idle/busy rules.
- ADR-0022 (model catalog and provider auth, accepted for `provider-presets`): credential sources
  and preset templates are reused unchanged.
- ADR-0027 (provider store, model profiles and usages, accepted): the store and usage shapes the
  wizard writes are exactly these.
- ADR-0029 (provider API families, proposed): the wizard surfaces its family and discovery contract
  but owns none of it; ordering and follow-through are recorded in the design's risk section.

## Conclusion

The wizard's only architecturally significant commitment — one writer, never a second one — is
already the accepted rule ADR-0027 and the store code enforce; the change inherits it in a spec
requirement instead of a new ADR. This card records the outcome and creates no repository-level ADR.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
