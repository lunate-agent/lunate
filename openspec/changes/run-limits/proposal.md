# Run limits: cost and wall-clock beside the step limit

## Why

A run can cost an unbounded amount of money and time. The step limit stops a tool loop, but a
single expensive model call or a slow provider is bounded by nothing, and print mode — the mode most
likely to run unattended — has no budget to give it. Issue #622 asks for the same protection a step
limit gives, in the two units an operator actually cares about: dollars and wall-clock.

## What Changes

- Add `maxCost` (US dollars) and `maxTime` (seconds) settings keys, both off by default.
- Add print-mode flags `--max-cost <usd>`, `--max-time <duration>` and `--allow-unpriced`.
- Enforce both limits in the agent loop beside `MaxSteps`:
  - cost is fed by the existing per-usage pricing (`CostCalculator` over the catalog, or the
    provider-reported amount when a record carries one);
  - wall-clock uses one monotonic timer per run.
- Each limit ends the run through a distinct event (`CostLimitReached` / `TimeLimitReached`) and
  `RunFinished(cost_limit)` / `RunFinished(time_limit)`, naming the limit, its value and what was
  reached. The limit is recorded in the session as an extension entry.
- Print mode exits with a distinct code for each limit: `3` for cost, `4` for time.
- A cost limit on a model with no known price refuses to start unless `--allow-unpriced` is passed;
  unknown-priced records never count as `$0`.
- A resumed session keeps its spend: the cost accumulator is seeded from the session's recorded
  usages, so a limit applies to the session's cumulative spend.
- Update print-mode and usage documentation; print-mode documentation recommends setting a cost
  limit.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-loop`: run limits (cost and wall-clock) enforced beside the step limit, with limit events
  and stop reasons.
- `agent-events`: the closed event set gains the two limit events.
- `agent-sessions`: the limit is recorded in the session and resumed spend is seeded from recorded
  usages.
- `print-mode`: new flags, distinct exit codes, limit messages and the unpriced-model refusal.
- `tui`: the interactive session shows the two limits as notices (default off).

## Impact

- `Lunate.Contracts`: two new `AgentEvent` records and two `StopReasons` constants; public API entries.
- `Lunate.Agent`: options, loop enforcement, cost accumulation, session seeding and recording.
- `Lunate.Coding`: launch flags, settings keys, print exit codes/notices/JSON, interactive notices,
  diagnostics.
- `Lunate.Protocols`: ACP stop-reason mapping.
- `Lunate.Cli` help text, `docs/configuration.md` (generated from the settings schema), `docs/usage.md`
  and `docs/guide.md`.
