# SWE-bench Verified — first published run

**Date:** 2026-11-14 · **Phase:** 0.4.0 · **Change:** `swebench-first-run`, `swebench-first-run-artifacts` ·
**Adapter:** `scripts/eval/swebench.py` (#623)

This is the first run of the SWE-bench harness adapter on real data: `rerun-624-bosgame-2`, run by a
maintainer outside the agent lane on a Linux x86_64 host with the same pinned subset and definition
as the pilot that preceded it. The official `swebench.harness.run_evaluation` is the only scorer.
This evidence replaces the earlier `first-run-2026-11-08` record, whose patch text and trajectories
the run host no longer held; the earlier numbers are history, not the published claim.

## Run definition

| Item | Value |
| --- | --- |
| Dataset | `SWE-bench/SWE-bench_Verified`, split `test` |
| Subset | 5 instances: every 100th id in lexicographic order (indices 0, 100, 200, 300, 400) |
| Budget model | `opencode-go/deepseek-v4.1-flash` (OpenCode Go plan) |
| Frontier model | `openrouter/anthropic/claude-opus-5.5` (OpenRouter) |
| Baseline | `mini-swe-agent` 2.4.6, same subset and models, 50-step limit |
| Cost cap | one cap for the whole run, US$5.00; the orchestrator passes the remaining budget to each invocation |
| Step limit | Lunate's default 50; mini-swe-agent capped at 50 for parity |
| Timeout | 1800 s per instance/evaluation |
| Environment | Linux x86_64 host, Docker running the official `linux/amd64` images without emulation (the orchestrator records `hostArchitecture: x86_64`) |

The pinned ids are in [`eval/swebench/instances.txt`](../../../eval/swebench/instances.txt); the full
definition is [`eval/swebench/run.json`](../../../eval/swebench/run.json).

## Results

Both arms ran on 2026-11-14, from 11:25 to 12:01 UTC, on the pinned subset and models.

| Arm | Model | Tier | Resolved | Spend |
| --- | --- | --- | --- | --- |
| Lunate | `opencode-go/deepseek-v4.1-flash` | budget | **2/5** | $0.0913 |
| Lunate | `openrouter/anthropic/claude-opus-5.5` | frontier | **4/5** | $1.0810 |
| mini-swe-agent | `opencode-go/deepseek-v4.1-flash` (OpenCode Go) | budget | **5/5** | $0.0393 |
| mini-swe-agent | `openrouter/anthropic/claude-opus-5.5` | frontier | **5/5** | $0.4040 |

Total: $1.6157 of the $5.00 cap. The cap was not reached and no instance was unpriced.

Per instance (Lunate):

| Instance | Budget | Frontier |
| --- | --- | --- |
| `astropy__astropy-12907` | resolved, step limit (50 steps, $0.0152) | resolved (4 steps, $0.0701) |
| `django__django-13128` | step limit, no patch (50 steps, $0.0226) | empty patch (14 steps, $0.5063) |
| `django__django-15572` | patch, unresolved (50 steps, $0.0245) | resolved (6 steps, $0.1549) |
| `pydata__xarray-3151` | resolved (32 steps, $0.0074) | resolved (7 steps, $0.1561) |
| `sphinx-doc__sphinx-8120` | step limit, no patch (50 steps, $0.0216) | resolved (9 steps, $0.1937) |

Per instance (mini-swe-agent):

| Instance | Budget | Frontier |
| --- | --- | --- |
| `astropy__astropy-12907` | resolved (14 steps, $0.0049) | resolved (4 steps, $0.0314) |
| `django__django-13128` | resolved (20 steps, $0.0107) | resolved (13 steps, $0.2271) |
| `django__django-15572` | resolved (10 steps, $0.0049) | resolved (4 steps, $0.0340) |
| `pydata__xarray-3151` | resolved (11 steps, $0.0036) | resolved (4 steps, $0.0433) |
| `sphinx-doc__sphinx-8120` | resolved (35 steps, $0.0152) | resolved (8 steps, $0.0683) |

## Cost

The run had one US$5.00 cap; the orchestrator passed the remaining budget to each invocation and the
baseline had its own per-instance limit. The Lunate rows are priced from the pinned catalog entry,
and the baseline rows from the LiteLLM registry. No instance was unpriced, so the total is a
measured number rather than a substitute.

| Arm | Model | Spend |
| --- | --- | --- |
| Lunate | `opencode-go/deepseek-v4.1-flash` | $0.0913 |
| Lunate | `openrouter/anthropic/claude-opus-5.5` | $1.0810 |
| mini-swe-agent | `opencode-go/deepseek-v4.1-flash` (OpenCode Go) | $0.0393 |
| mini-swe-agent | `openrouter/anthropic/claude-opus-5.5` | $0.4040 |
| **Total** | | **$1.6157 of $5.00** |

The cap was not reached and no cost-limit stop occurred, so every intended instance ran.

## Failures

Every failure is kept; none is dropped from the resolved counts.

- **Lunate, budget model:** four instances reached the 50-step limit (exit 2).
  `astropy__astropy-12907` had already produced a patch the official harness resolved;
  `django__django-15572` produced a patch the official harness did not resolve;
  `django__django-13128` and `sphinx-doc__sphinx-8120` produced no patch. Only
  `pydata__xarray-3151` finished inside the budget (32 steps, resolved).
- **Lunate, frontier model:** `django__django-13128` produced an empty patch after 14 steps and
  cost $0.5063 — a third of the Lunate arm's spend for no submission.
- **mini-swe-agent:** no failure. Both models submitted for all five instances and the official
  harness resolved every one.
- No cost-limit stop and no unpriced instance occurred.

## What this run does and does not show

**It shows:** the adapter runs real Verified instances end to end on both arms, the official harness
is the only scorer, both arms use the same subset and models, and a five-instance run costs well
under the cap. It also shows the failure modes: a 50-step budget is tight for the budget model, an
empty patch is not a resolved instance, and a provider-level step budget is spent whether or not a
patch appears.

**It does not show:** a quality or reliability ranking in Lunate's favour. Five instances are 1% of
Verified, there is one sample per model, and mini-swe-agent resolved 5/5 with both models while
Lunate resolved 2/5 and 4/5 — on this sample the baseline is ahead, and the pilot does not support
any claim that Lunate is better at SWE-bench. It also says nothing about reliability across repeated
runs: the earlier pilot of the same definition produced different per-instance outcomes (Lunate
resolved 2/5 and 3/5 there), which is what a five-instance sample does. The C# eval suite in
`eval/` is a different measurement and does not support a SWE-bench claim.

## Adapter findings from the first real instances

The fake-Docker tests in #623 could not expose these; the live instances did. Each fix has a test in
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

The orchestrator writes `eval/swebench/summary.json` and `summary.md`. Its default `--work-dir` is
`eval/swebench/runs`, so a run's working tree — and therefore every `workDir` the summary records —
resolves inside the checkout instead of a machine-local temporary directory.

## Committed evidence

The per-instance evidence behind every number above is committed under
[`eval/swebench/runs/rerun-624-bosgame-2/`](../../../eval/swebench/runs/rerun-624-bosgame-2/), linked
from each instance's `workDir` in [`summary.json`](../../../eval/swebench/summary.json):

- one `predictions.jsonl` per instance and arm, holding the exact `model_patch` the official harness
  scored, and
- one `report.json` per instance and arm with a non-empty patch, the official per-instance record of
  patch presence, resolution and the `FAIL_TO_PASS`/`PASS_TO_PASS` results.

Seventeen of the twenty instance rows have a `report.json`; the three with no non-empty patch
(`django__django-13128` for both Lunate models, `sphinx-doc__sphinx-8120` for the Lunate budget
model) have their empty prediction committed and no report, because the adapter did not ask the
official harness to evaluate an empty patch. A reader can follow a resolved count from the write-up
to the summary row to the official verdict without the run host.

The trajectories stayed in the run host's working tree
(`/home/daniel/swebench-624/runs/rerun-624-bosgame-2/`) and are not part of the repository; the
per-instance spend and step counts they produced are in `summary.json`. That directory's README
records what is and is not committed.
