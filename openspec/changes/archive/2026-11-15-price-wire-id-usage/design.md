# Design — price a usage record named by its provider's wire id

Binding implementation detail. Where this file pins a behaviour, apply follows it verbatim; a
contradiction between this file and the spec delta is a stop-and-report, not a choice.

## Context

The loop accounts a usage record under the model id the provider echoed on the wire
(`ChatResponseUpdate.ModelId`). For `opencode-go/deepseek-v4.1-flash` that echo is the bare
`deepseek-v4.1-flash`, which two presets serve (`opencode/deepseek-v4.1-flash` at 0.3/1.2 and
`opencode-go/deepseek-v4.1-flash` at 0.15/0.6). `ModelCatalog.Find` refuses to guess between two
wire-id matches and throws; every price lookup swallows that and calls the record unpriced, so
`usage_updated` has no `costUsd` and the cost limit counts nothing — while the launch check, which
looks at the selected catalog entry, happily accepts the limit.

`ModelCatalog` already has the scoped resolver this needs: `FindFor(provider, id)` resolves a bare
id in one provider's scope (catalog id `<provider>/<id>` first, then the exact id, then the single
global wire-id match) and reports an ambiguity as candidates instead of guessing. It is internal and
`Lunate.Agent` and `Lunate.Coding` both see it through `InternalsVisibleTo`.

## Goals / Non-Goals

**Goals:**

- `usage_updated.costUsd` on a run whose provider echoes a bare wire id, priced from the preset the
  run selected.
- The cost limit counts the same record, so a limit the launch accepts is a limit that fires.
- One resolution rule, so the JSON stream and the limit cannot drift apart.

**Non-Goals:**

- No change to which id an event or a session entry carries: the session format pins the resolved
  backend id, and this change leaves it alone.
- No change to the launch refusal: a selected model with no catalog price still refuses a cost limit
  without `--allow-unpriced`.
- No change to the TUI turn footer or `/cost`; they are separate surfaces with their own acceptance.
- No guessing: an id that stays ambiguous after the provider scope is unpriced, never a nearest
  entry and never `0`.

## Decisions

**1. The scope is the provider of the model the run selected, not of the id.**
The echo is a bare id, so it cannot name its own provider; the run knows which preset it called
(`AgentHarnessOptions.Model`, or the resolved usage's model). Alternative rejected: change the
recorded id to the catalog id at the source — the session spec pins "the resolved backend's model
id", and rewriting it would change what sessions record for every provider.

**2. One never-throwing lookup on `ModelCatalog`, beside `FindFor`.**
`FindUsageModel(id, provider)` answers "which entry prices this record": the provider scope first,
then the catalog-wide rules, and `null` for "no entry or still ambiguous". It is internal, so no
public API changes. Alternative rejected: a second resolver in `Lunate.Coding` and another in
`Lunate.Agent` — the drift between them is exactly the bug being fixed.

**3. Callers pass the provider they know, and nothing else changes.**

```text
            ┌──────────────────────────┐
            │ Lunate.Coding            │
            │  PrintMode ── model ─────┼──┐
            │  PrintEventJson ─────────┼──┤ provider hint
            └──────────────────────────┘  │
            ┌──────────────────────────┐  │
            │ Lunate.Agent             │  │
            │  AgentHarness (spend,    │  │
            │   resumed seed) ─────────┼──┘
            └───────────┬──────────────┘
                        │ FindUsageModel(id, provider)
            ┌───────────▼──────────────┐
            │ Lunate.Ai                │
            │  ModelCatalog            │
            │   FindFor(provider, id)  │
            │   Find(id)               │
            └──────────────────────────┘
```

- `PrintEventJson.UsageBody` resolves the fallback catalog id (the selected model) and passes its
  provider to `UsageCost.ForRecord`, which keeps owning the negative-lane and ambiguity rules.
- `AgentHarness.FindModel` passes `SelectedModel`'s provider (without recursing through
  `SelectedModel`, so the seeded `_options.ModelId` path stays acyclic).
- `SeedSpend` goes through the same `AddSpend`/`FindModel` pair, so a resumed session's wire-id
  entries keep their spend.

**4. Ambiguity outside the scope stays unpriced.**
`FindFor` reports candidates rather than a match; `FindUsageModel` returns `null` for those, which
`UsageCost` and `AddSpend` already treat as unpriced — absent `costUsd`, never zero, never counted
as spend.

## Risks / Trade-offs

- [The selected provider's entry is priced for a record a *routed* backend produced] -> The router
  reports its backend as a catalog or registry id, which the exact-id and catalog-id steps resolve
  before the provider hint is consulted; a bare wire id is only reachable when the record really is
  the primary model's echo.
- [A caller forgets the provider hint and an ambiguous id stays unpriced] -> The behaviour is the
  status quo (no cost, no guessing); the spec's scenario pins the priced case so a regression is a
  test failure.
- [Provider-scoped resolution hides a genuine ambiguity in the catalog] -> Both entries stay in the
  catalog and remain ambiguous to `Find`; only a caller that knows which provider it ran prices one.

## Migration Plan

No migration: prices are display and limit arithmetic over already-recorded usage, and no session
content changes. Rollback is reverting the change; a session recorded before or after it seeds the
same way.

## Open Questions

None.
