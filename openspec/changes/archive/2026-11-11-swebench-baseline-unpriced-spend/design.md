## Context

`swebench_baseline.run_baseline_instance` reads mini-swe-agent's own trajectory and takes
`info.model_stats.instance_cost` as the instance spend. It currently coerces any missing or
non-positive value to `0.0`. `swebench_run.run_arm` then sees a number, adds it to the running
spend and may start the next instance, so a run whose baseline price is unknown can be reported as
if it were priced and under the cap. The Lunate arm already returns `None` for an unpriced row and
stops; the baseline path is the remaining hole. The committed `first-run-2026-11-08` baseline has
positive costs for every row, so its evidence does not change.

## Goals / Non-Goals

**Goals:**

- A baseline instance without a positive `instance_cost` is recorded as unpriced and leaves the
  arm and run totals unknown.
- The baseline arm stops before the next instance once a row is unpriced.
- Tests pin zero, missing, negative and positive baseline costs, plus the baseline-arm stop.
- The published-run spec and `eval/README.md` say the rule applies to both arms.

**Non-Goals:**

- Rerunning the committed baseline or changing its recorded numbers.
- Re-pricing a baseline instance from token counts; LiteLLM owns that cost and the run does not
  duplicate it.
- Changing the Lunate arm's spend accounting, the cap, the subset or the models.

## Decisions

### D1 — Missing or non-positive baseline cost maps to `None`

`run_baseline_instance` passes the raw `info` to a small pure helper, `baseline_spend(info)`, which
returns a positive float or `None`. Alternatives considered: leaving `0.0` and flagging it later in
`run_arm` (rejected: the row would still carry a price it does not have), or re-pricing from token
lanes (rejected: mini-swe-agent already reports cost, and a second price table would drift). A zero
from mini-swe-agent means it could not price the call, so it is unknown, not free.

### D2 — The orchestrator treats the row like any other unpriced row

No new stop logic is needed: `run_arm` already sets `unknown_spend` for a `None` row, stops after
the current instance and writes `totalSpendUsd: null` / `capReached: true`. Keeping the rule in one
place is why the extraction returns `None` instead of a sentinel.

### D3 — Test at the extraction and the arm

The regression test builds a real baseline trajectory with a mocked process runner and asserts the
returned row. A second test mocks `run_baseline_instance` and checks the baseline arm stops after
the first instance. This covers both the extraction and the orchestrator without Docker or a live
model.

## Risks / Trade-offs

- [A provider genuinely charges nothing and mini-swe-agent reports `0.0`] -> The run reports
  unknown instead of zero; this is conservative and never understates spend against the cap.
- [The helper is only used by one caller] -> It keeps the spending rule in a pure, testable place
  and mirrors `instance_spend` for the Lunate arm.
- [Future baseline tooling changes the field name] -> The helper returns `None`, which stops the run
  honestly instead of silently reporting zero.

## Migration Plan

No product migration and no data rewrite. The fix ships with tests and a spec/README clarification;
the committed summary is untouched. Rollback is the previous commit, which restores the zero
default.

## Open Questions

None. No in-force ADR is affected; this is a tactical accounting rule, not an architectural
commitment.
