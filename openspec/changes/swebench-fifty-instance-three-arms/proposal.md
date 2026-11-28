## Why

The first published SWE-bench run decided nothing: five instances are 1% of Verified, and one
instance moving in or out of the resolved count moved the arm's result by 20 points. The optional
`semantic_search` tool (#981's rule) has no measurement at all that could justify keeping it, and a
five-instance arm cannot produce one: the tool's effect, positive or negative, is smaller than the
noise of a sample that size. The sample has to grow until an arm comparison can carry a verdict, and
the arms have to be named data so the numbers travel with the configuration that produced them.

## What Changes

- The pinned subset becomes 50 Verified instances: every 10th instance id in lexicographic order of
  the 500-instance `SWE-bench/SWE-bench_Verified` test split (indices 0, 10, …, 490). The rule and
  the ids live in `eval/swebench/instances.txt`; `run.json` records the rule.
- `eval/swebench/run.json` names three arms as data instead of a fixed budget/frontier/decision
  matrix: **lunate budget with `semantic_search` off**, **lunate budget with it on**, and the
  **mini-swe-agent budget** arm. There is no frontier arm in this run — that is its own card.
- Each arm entry names its `tool` (`lunate` or `mini-swe-agent`), its `tier`, its model id and its
  credential variable; a lunate arm names `semanticSearch` (`on` or `off`), and the on arm names the
  embeddings model and credential its prepared home binds. The definition loader validates that
  shape and refuses a malformed arm instead of silently skipping it.
- The harness runs every arm entry on the one pinned sample: the on arm gets a generated
  `LUNATE_HOME` whose `settings.json` enables `semantic_search` in `tools.extra` and binds
  `usages.embeddings`, the off arm runs without that home, and the baseline arm is the one named
  mini-swe-agent entry rather than a mirror of every single-model tier.
- The summary records each arm's id beside its tool and tier, and records the `semantic_search`
  call count per instance and per arm (counted from the trajectory's `tool_call_start` events), so a
  reader can attribute the on arm's numbers to the tool that produced them. The rendered summary
  shows the arm id and the call count.
- One run cost cap: US$4.00, the card's cap. Reaching it stops the run and reports the spend and
  the limit stop, as before.
- `eval/README.md` documents the 50-instance selection rule, the three arms, the prepared home and
  the new per-instance measurement. The previous five-instance run's committed summary and write-up
  stay as the record of that run.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `benchmark-harness`: the published run definition no longer requires one budget and one frontier
  model with an optional decision arm; it names an explicit arm list with a tool per arm, the
  pinned run names the three comparison arms, every arm runs the whole pinned subset, and the
  summary carries the arm id and the `semantic_search` call count.

## Impact

- `eval/swebench/instances.txt` and `eval/swebench/run.json` — the pinned sample and the arm matrix.
- `scripts/eval/swebench_run_support.py`, `scripts/eval/swebench_run.py` and
  `scripts/eval/swebench_baseline.py` — arm validation, per-arm execution, the prepared home,
  per-instance and per-arm `semantic_search` counts, summary rendering.
- `tests/scripts/test_swebench_run.py` and a new arm-definition test module.
- `eval/README.md`.
- No product (C#) code, no public API, no new NuGet package, no session-format change. Docker and
  live credentials stay on the run host; the harness change is exercised with a fake adapter.
