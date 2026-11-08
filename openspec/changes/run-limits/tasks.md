# Tasks — run-limits

## 1. Agent loop and contracts

- [x] 1.1 Add `CostLimitReached` and `TimeLimitReached` events and the `cost_limit` / `time_limit`
      stop reasons; update the public API lists.
- [x] 1.2 Add `MaxCostUsd`, `MaxTime`, `AllowUnpricedCost` and `TimeProvider` to
      `AgentHarnessOptions` with validation; refuse a cost limit over an unpriced selected model.
- [x] 1.3 Accumulate per-record cost from provider amount or catalog pricing; seed it from session
      usages; mark unpriced records rather than counting zero.
- [x] 1.4 Enforce both limits at the boundary (after accounting and before a request); the timer
      cancels an in-flight call and emits the time limit rather than cancellation.
- [x] 1.5 Record the reached limit in the session as `ext/lunate/run-limit`.
- [x] 1.6 Tests: cost boundary, below-boundary, time boundary, in-flight timer, unpriced refusal,
      resumed spend, session record, event identity/sequence.

## 2. Coding frontend

- [x] 2.1 Settings `maxCost` and `maxTime` (off by default) with parsing, schema entries and probes.
- [x] 2.2 Launch flags `--max-cost`, `--max-time`, `--allow-unpriced`, with parsing and help text.
- [x] 2.3 Wire the resolved limits and `TimeProvider` into print, interactive and ACP harnesses.
- [x] 2.4 Print mode: unpriced refusal (exit 2), limit notices, distinct exit codes 3 and 4, JSON
      event mappings, diagnostics.
- [x] 2.5 Interactive notices and diagnostic bundle handling.
- [x] 2.6 ACP stop-reason mapping and event tests.
- [x] 2.7 Tests: print exit codes/messages/refusal, flag parsing, settings parsing/defaults,
      JSON golden lines, interactive notices.

## 3. Documentation and lifecycle

- [x] 3.1 Update `docs/usage.md` and `docs/guide.md`; print-mode docs recommend a cost limit;
      regenerate the settings table with `scripts/docs-sync`.
- [x] 3.2 Run formatting, the focused test classes, `openspec validate --all --strict` and
      `bash scripts/check-openspec-skills.sh`.
- [x] 3.3 Sync specs and archive the change as the final commit.
