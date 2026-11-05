# ADR Review Manifest

- Status: completed
- Review date: 2026-11-03

## Review Summary

ADR review completed for this change. The design's one durable architectural decision — a fifth
provider family `systemone` served by a native decision transport behind the decision layer's seam,
with provider quirks as preset data, the box + image contract at 1.16.0, exact-version pinning and
provider-reported cost — is already recorded as the repository-level ADR this change created:
ADR-0030, accepted 2026-11-03 (its acceptance, the TypeSafe-direct deferral and the preset scoping
were folded in from the sign-off in #492). The manifest records the review outcome; the full context,
decision, consequences and rejected alternatives live in the ADR file.

## In-Force ADRs Reviewed

- ADR-0002 (layering, ME.AI model types): the decision transport is deliberately **not** an
  `IChatClient` — a decision exchange is not a chat exchange — yet the client surface is shaped like
  M.E.AI clients so a future M.E.AI decision abstraction is a mechanical swap (the ADR's addendum).
- ADR-0022 (model catalog and provider auth): the family comes from a model's resolved `apiFamily`
  (profile → discovery → catalog) and credentials ride the store's existing sources.
- ADR-0027 (provider store, model profiles and usages, accepted): decision profiles and usages are
  these shapes; the decision usage is where the provider-reported `usage.cost` is recorded.
- ADR-0028 (decision layer primitive): the native seam this change finally fills; the validator stays,
  and the contract's decision types are the 1.16.0 surface.
- ADR-0029 (provider API families): `systemone` joins the family list as the fifth value; the
  per-family base URL, the family guard and the preset `baseUrls` map are its contract.
- ADR-0030 (native decisions, accepted 2026-11-03): the change's own decision, listed under
  "completes" ADR-0027/0028 and building on ADR-0029.

## New Durable ADRs Created

- `adr/0030-native-decisions.md` — "Native decisions are a provider family, not a chat model",
  status accepted — 2026-11-03, with an addendum (2026-11-03) pinning the M.E.AI swap path:
  `IDecisionClient` (delegating client, builder, OpenTelemetry), the adapter in `Lunate.Ai.Decisions`
  and the vendor transport in `Lunate.Ai.SystemOne`, neither packed.
