# SWE-bench Verified — first published run

**Date:** 2026-11-08 · **Phase:** 0.4.0 · **Change:** `swebench-first-run` ·
**Adapter:** `scripts/eval/swebench.py` (#623)

This is the first run of the SWE-bench harness adapter on real data. It fixes the adapter where the
fake-Docker tests could not see the official images' behaviour, then publishes the numbers, the
spend and the failures from a fixed subset. The official `swebench.harness.run_evaluation` is the
only scorer.

## Run definition

| Item | Value |
| --- | --- |
| Dataset | `SWE-bench/SWE-bench_Verified`, split `test` |
| Subset | 5 instances: every 100th id in lexicographic order (indices 0, 100, 200, 300, 400) |
| Budget model | `opencode-go/deepseek-v4.1-flash` (OpenCode Go plan) |
| Frontier model | `openrouter/anthropic/claude-opus-5.5` (OpenRouter) |
| Baseline | `mini-swe-agent` 2.4.6, same subset and models, 50-step limit |
| Cost cap | one cap for the whole run, US$30.00; the orchestrator passes the remaining budget to each invocation |
| Step limit | Lunate's default 50; mini-swe-agent capped at 50 for parity |
| Timeout | 1800 s per instance/evaluation |
| Environment | macOS arm64 host; Docker via podman, official `linux/amd64` images under QEMU; .NET needs `DOTNET_EnableWriteXorExecute=0` and `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` |

The pinned ids are in [`eval/swebench/instances.txt`](../../eval/swebench/instances.txt); the full
definition is [`eval/swebench/run.json`](../../eval/swebench/run.json).

The host's Podman VM disk filled after the first three instances and the daemon dropped. The VM was
rebuilt (same arm64 host, official `linux/amd64` images under QEMU) and the Lunate arm resumed for
the last two instances; the three completed results were preserved. This is recorded here rather
than hidden: the run is one logical five-instance run with an infrastructure interruption, not two
different measurements.

## Results

The Lunate arm was measured on 2026-11-08; the mini-swe-agent arm on 2026-11-10, with the same
pinned definition and the same dataset split. Both are one logical run: the pinned subset and
models did not change between the arms.

| Arm | Model | Tier | Resolved | Spend |
| --- | --- | --- | --- | --- |
| Lunate | `opencode-go/deepseek-v4.1-flash` | budget | **2/5** | $0.1637 |
| Lunate | `openrouter/anthropic/claude-opus-5.5` | frontier | **3/5** | $1.1343 |
| mini-swe-agent | `openai/deepseek-v4.1-flash` (OpenCode Go) | budget | **4/5** | $0.0423 |
| mini-swe-agent | `openrouter/anthropic/claude-opus-5.5` | frontier | **3/5** | $0.3378 |

Total: $1.6781 of the $30.00 cap. The cap was not reached and no instance was unpriced.

Per instance (Lunate):

| Instance | Budget | Frontier |
| --- | --- | --- |
| `astropy__astropy-12907` | step limit, no patch | resolved (3 steps) |
| `django__django-13128` | step limit, no patch | empty patch |
| `django__django-15572` | resolved (step limit) | resolved (7 steps) |
| `pydata__xarray-3151` | step limit, no patch | patch, unresolved |
| `sphinx-doc__sphinx-8120` | resolved (step limit) | resolved (10 steps) |

Per instance (mini-swe-agent):

| Instance | Budget | Frontier |
| --- | --- | --- |
| `astropy__astropy-12907` | resolved (16 steps) | resolved (4 steps) |
| `django__django-13128` | resolved (22 steps) | resolved (8 steps) |
| `django__django-15572` | resolved (12 steps) | resolved (4 steps) |
| `pydata__xarray-3151` | submitted, unresolved (21 steps) | submitted, unresolved (7 steps) |
| `sphinx-doc__sphinx-8120` | resolved (21 steps) | API error, no patch (6 steps) |

## Cost

The run had one US$30 cap; the orchestrator passed the remaining budget to each invocation and
the baseline had its own US$3 limit per instance. The Lunate rows are priced from the pinned
catalog entry, and the baseline rows from the LiteLLM registry. No instance was unpriced, so the
total is a measured number rather than a substitute.

| Arm | Model | Spend |
| --- | --- | --- |
| Lunate | `opencode-go/deepseek-v4.1-flash` | $0.1637 |
| Lunate | `openrouter/anthropic/claude-opus-5.5` | $1.1343 |
| mini-swe-agent | `openai/deepseek-v4.1-flash` (OpenCode Go) | $0.0423 |
| mini-swe-agent | `openrouter/anthropic/claude-opus-5.5` | $0.3378 |
| **Total** | | **$1.6781 of $30.00** |

The cap was not reached and no cost-limit stop occurred, so every intended instance ran.

## Failures

Every failure is kept; none is dropped from the resolved counts.

- **Lunate, budget model:** all five instances reached the 50-step limit (exit 2).
  `django__django-15572` and `sphinx-doc__sphinx-8120` still resolved; `astropy__astropy-12907`,
  `django__django-13128` and `pydata__xarray-3151` produced no patch.
- **Lunate, frontier model:** `django__django-13128` produced an empty patch after 13 steps;
  `pydata__xarray-3151` produced a patch that the official harness did not resolve after 9 steps.
- **mini-swe-agent, budget model:** `pydata__xarray-3151` submitted a patch that the official
  harness did not resolve (21 steps).
- **mini-swe-agent, frontier model:** `pydata__xarray-3151` submitted a patch that the official
  harness did not resolve (7 steps); `sphinx-doc__sphinx-8120` stopped with an `APIError` after
  6 calls and produced no patch. The failed call's spend is still counted.
- No cost-limit stop and no unpriced instance occurred.

## What this run does and does not show

**It shows:** the adapter runs real Verified instances end to end, the official harness is the only
scorer, both arms use the same subset and models, and a five-instance run costs well under the
cap. It also shows the failure modes: a 50-step budget is tight for the budget model, an empty
patch is not a resolved instance, and a provider error is recorded as a failure rather than hidden.

**It does not show:** a reliability or quality ranking. Five instances are 1% of Verified, there
is one sample per model, and the mini-swe-agent baseline resolved 4/5 with the budget model and
3/5 with the frontier model — the pilot does not show Lunate ahead of that baseline. The C#
head-to-head suite is a different measurement and does not support a SWE-bench claim. The host is
arm64 running the official `linux/amd64` images under QEMU, so wall-clock times are not comparable
to a native x86_64 host. The run is a pilot, not a full Verified number.

## Adapter findings from the first real instance

The fake-Docker tests in #623 could not expose these; the live instance did. Each fix has a test in
`tests/scripts/`.

1. **No ICU in the official images.** The self-contained .NET binary aborted before its first event
   (`Couldn't find a valid ICU package`). The adapter now sets
   `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` in the container.
2. **Stale default dataset.** `princeton-nlp/SWE-bench_Verified` has no `image` or `eval_script`,
   which `swebench` 5.0.2 requires. The default is now `SWE-bench/SWE-bench_Verified`.
3. **Missing image in the local dataset.** The official harness raised `KeyError: 'image'` on the
   adapter's `dataset.json`; the adapter now writes the resolved image into every record.
4. **Wrong summary filename.** `swebench` 5.0.2 writes `<model>.<run_id>.json`, not `results.json`;
   the adapter now finds that name as well.
5. **A crash core dump became the patch.** The QEMU core dump was collected as a 154 MB prediction;
   the collector now excludes `core`, `core.*`, `*.core`, `qemu_*` and `vgcore.*`.

## Reproduce

```bash
dotnet publish src/Lunate.Coding -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o artifacts/linux-x64
export OPENCODE_GO_API_KEY=... OPENROUTER_API_KEY=...
python3 scripts/eval/swebench_run.py --arm all --lunate-bin artifacts/linux-x64/lunate \
  --mini-extra mini-extra --run-id <run-id>
```

The orchestrator writes `eval/swebench/summary.json` and `summary.md`; the per-instance patches are
kept under `eval/runs/` (ignored by git) for audit.
