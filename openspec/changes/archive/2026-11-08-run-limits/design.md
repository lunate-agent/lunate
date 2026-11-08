## Context

A run currently stops on `MaxSteps`, token budgets (subagents) or cancellation. Cost and
wall-clock are unbounded, and print mode — the unattended frontend — has no budget flag. The
existing per-usage cost tracking prices records from the catalog and already persists usage on the
session.

## Goals / Non-Goals

**Goals:**

- Enforce a cost limit and a wall-clock limit in the loop beside the step limit.
- Feed cost from the existing per-usage pricing, including provider-reported amounts.
- Use one monotonic timer per run, driven by `TimeProvider` so tests can pin the boundary.
- Make each limit end the run with a distinct event, stop reason, session record and print exit
  code, naming the limit, its value and the reached spend/time.
- Refuse a cost limit on an unpriced model unless the host allows it.
- Seed cost from a resumed session's recorded usages.

**Non-Goals:**

- No new pricing source, provider call or telemetry.
- No per-model or per-tool sub-budgets.
- No hard real-time pre-emption guarantee: the timer cancels the in-flight call, and the loop
  observes the limit at the next accounting/request point.
- No schema change: the session record is an extension entry.

## Decisions

1. **Limits live in the loop, not a wrapper.** `AgentHarnessOptions` gains `MaxCostUsd`,
   `MaxTime`, `AllowUnpricedCost` and `TimeProvider`. The loop already owns usage accounting and
   the point between a model call and the next tool batch; a wrapper could not stop at the same
   boundary or record the reached value with the session.
2. **Cost is session-cumulative.** A harness seeds its spend from session entries at construction
   and adds every accounted usage. This makes "a resumed session keeps its spend across restart"
   true without a second store. A no-session run starts at zero.
3. **One price per record.** A provider-reported `SessionUsage.Cost` wins; otherwise
   `CostCalculator.Compute` prices the model and token lanes. A null result marks the record
   unpriced and contributes nothing — never zero.
4. **The exact boundary ends the run.** Checks are `reached >= limit` after usage accounting and
   before each request, so equality triggers. The wall-clock timer additionally cancels an
   in-flight call; the cancellation is distinguished from a user cancel by a flag set when the
   timer fires.
5. **Session record is an extension entry.** `ext/lunate/run-limit` with a small invariant JSON
   payload records the limit and reached value. Extension entries are already part of the session
   format, so no schema bump or golden change is needed.
6. **Print exit codes 3 and 4.** `2` already covers length/step/usage errors; distinct codes keep
   scripts able to branch per limit. The answer already streamed is still written, like the step
   limit.
7. **Unknown-priced refusal is a frontend launch refusal.** Print mode checks the selected model
   before creating a harness and exits 2 with a message naming the model and `--allow-unpriced`;
   the harness also refuses unpriced accounting when a cost limit is set and the option is off,
   so direct API callers get the same guard.

## Risks / Trade-offs

- **A provider call can overshoot the wall-clock limit** -> The timer cancels the call; the
  observed elapsed value may be slightly over the boundary, which the event reports honestly.
- **Catalog prices can be stale** -> The limit uses the same pricing the product already shows;
  provider-reported costs take precedence when present.
- **Unpriced records make a cost limit incomplete** -> The run refuses unless explicitly allowed;
  with the option, the limit is documented to cover priced spend only.
- **Cumulative session spend may surprise a user** -> The limit event and `/session` both show the
  spend; documentation states that the cost limit is session-cumulative.

## Migration Plan

No data migration. Existing sessions load unchanged; the new settings default to off. Removing the
settings or flags restores today's behaviour.

## Open Questions

None.
