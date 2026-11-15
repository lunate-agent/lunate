# Price a usage record named by its provider's wire id

## Why

A run on `opencode-go/deepseek-v4.1-flash` reports every `usage_updated` line with
`"modelId":"deepseek-v4.1-flash"` and no `costUsd`, although the catalog prices that model (input
0.15, output 0.6 per million). The id on the event is the id the provider echoes on the wire, and
that bare id is served by more than one preset (`opencode/…` and `opencode-go/…`), so the
catalog-wide price lookup refuses to guess which entry was meant and drops the cost. The same
unpriced lookup feeds the cost limit: `--max-cost 0.2` is accepted on that run and then never counts
its spend, so the reported cost and the enforced limit disagree.

## What Changes

- A price lookup resolves a usage record's model id in the scope of the provider the run selected:
  the selected provider's own entry for that id wins over a bare wire id that several presets serve.
  Nothing is guessed — an id that still names no entry or stays ambiguous stays unpriced.
- `usage_updated.costUsd` and the cost limit use that one resolution, so a run the JSON prices is a
  run the limit counts. An unpriced record still omits `costUsd` and is never counted as `0`.
- The launch refusal stays as it is: a cost limit over a model the catalog cannot price refuses to
  start unless `--allow-unpriced` is set.
- The event's `modelId` and the session's recorded model id are unchanged: they keep naming the
  backend the provider reported, which is what the session format already pins.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `print-mode`: `usage_updated.costUsd` resolves the record's model id in the provider the run
  selected before pricing it.
- `agent-loop`: the cost limit prices a usage record whose model id is the provider's wire id through
  the same provider-scoped resolution, so the limit and the reported cost agree.

## Impact

- `src/Lunate.Ai/ModelCatalog.cs`: one internal, never-throwing price lookup beside the existing
  provider-scoped `FindFor`.
- `src/Lunate.Agent/AgentHarness.cs`: spend accounting and the resumed-spend seed resolve their model
  through it.
- `src/Lunate.Coding/SessionCost.cs` and `src/Lunate.Coding/PrintEventJson.cs`: the JSON cost lookup
  takes the selected model's provider.
- `docs/json-events.md`: one sentence saying which provider the price lookup resolves against.
- No new packages, no public API change, no session-format change, no change to which model id an
  event or a session entry carries.
- The terminal's turn cost footer and `/cost` price per record the same way; they are not part of
  this card and keep today's behaviour.
