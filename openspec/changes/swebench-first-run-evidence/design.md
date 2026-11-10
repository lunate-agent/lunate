## Context

The `swebench-first-run` change is archived. Its Lunate arm is complete (five instances, two
models, official counts), but the baseline arm stopped after one instance per model, the committed
summary was never written, and the spend total was reconciled by counting an unpriced row as the
cap and then adding the other arm's spend. The archived delta already requires the baseline, the
committed summary, the write-up and the README claim; this change completes that evidence and
makes the spend rule explicit.

The run definition stays frozen: `eval/swebench/instances.txt` and `run.json` are the published
definition. Re-running the Lunate arm would replace measured evidence with a different sample of
the same models, so only the missing baseline is executed and merged under the same run id.

## Goals / Non-Goals

**Goals:**

- Baseline rows for all five pinned instances and both pinned models, scored by the official
  harness.
- A committed `eval/swebench/summary.json` / `summary.md` that reconcile both arms against the cap.
- A spend rule that cannot report more than the cap on paper.
- A pinned-stride test that compares against the official split, not just list length.
- A write-up and README claim that cite the completed run.

**Non-Goals:**

- Changing the pinned subset, models, cap or step limits.
- Re-running the Lunate arm.
- A full Verified number; five instances remain a pilot.

## Decisions

### D1 — Run only the baseline, merge by run id

`--arm baseline --run-id first-run-2026-11-08` reads the committed Lunate summary for the spend
already committed and writes `first-run-2026-11-08.baseline.json`; `--arm merge` then produces
`summary.json` and `summary.md`. This preserves the measured Lunate rows and makes the merged
summary the single record.

### D2 — Unknown spend is unknown, not the cap

`run_arm` tracks a separate `unknown` flag. When a row's `spendUsd` is `None`, the orchestrator
stops starting instances and reports `totalSpendUsd: null`; it never adds a substitute amount.
`summarize_arm` already reports an arm total as `null` when any row is unpriced, so the run total
follows the same rule. `capReached` is true because the cap can no longer be certified. This is
stricter than the old `spent = cap` sentinel and directly removes the US$30.031674 total.

### D3 — The stride is pinned against the official split fixture

`tests/fixtures/swebench/verified-test-ids.txt` records the 500 official Verified test ids in the
order the dataset returns them sorted. The test asserts the pinned list equals the ids at indices
0, 100, 200, 300 and 400. The fixture is data, not a live download, so the suite stays offline; a
header records its provenance.

### D4 — The README cites the pilot, not the saturated suite

The C# bullet keeps the Roslyn sentence and replaces the 6/6 head-to-head sentence with this
run's measured result and a link to the write-up. The write-up states the pilot's size and every
unresolved or failed instance, so the claim cannot be read as a full Verified score.

## Risks / Trade-offs

- [An arm64 host under QEMU makes the baseline slow or flaky] -> Keep every failure row; report it
  as a failure, never drop it, and let the official evaluation decide resolution.
- [Baseline spend could approach the cap] -> The orchestrator passes the remaining budget divided
  by the concurrent models and stops after the instance that reaches the cap; a capped run is
  reported as a cap stop with its actual spend.
- [The five-instance pilot is too small for a reliability claim] -> The write-up and README state
  the sample size and scope explicitly.

## Migration Plan

No product migration. The run artifacts and docs are repository data; the accounting change is
covered by focused Python tests. If the baseline cannot complete, the PR stays blocked with the
committed attempts and the reason.

## Open Questions

None blocking. A larger Verified subset remains a follow-up only if this pilot's failure modes
justify it.
