## Context

The harness has two halves: `scripts/eval/swebench.py` runs one instance inside its official image
and hands the patch to the official evaluation; `scripts/eval/swebench_run.py` owns a run — it reads
`eval/swebench/run.json`, spends one cost cap across the arms, and writes the committed summary.
Support code that does no subprocess work lives in `scripts/eval/swebench_run_support.py`, which is
where the tests reach the selection, spend and summary rules without Docker.

The run definition was written for a fixed matrix: exactly one budget and one frontier model, an
optional decision arm, and a baseline that mirrors every single-model tier. The comparison this card
needs — the same budget model with `semantic_search` off and on, plus its mini-swe-agent baseline —
does not fit that matrix: two arms share one tier, and the baseline is one explicit arm rather than
a mirror. The pinned sample also grows from 5 to 50 instances, so a single instance no longer moves
an arm's result by 20 points.

```
+------------------------------ run host (Strix Halo, Docker) ------------------------------+
|                                                                                           |
|  eval/swebench/run.json + instances.txt   (the pinned definition, committed data)         |
|            |                                                                              |
|            v                                                                              |
|  scripts/eval/swebench_run.py  --arm all --lunate-bin ... --mini-extra ...                |
|    |  per arm entry: remaining-budget split, generated LUNATE_HOME, one instance loop     |
|    |                                                                                      |
|    +--> scripts/eval/swebench.py  (lunate arm)  --> docker run official image             |
|    |       LUNATE_HOME=/lunate-run/lunate-home, lunate -p --json --yolo --model ...        |
|    |                                                                                      |
|    +--> mini-extra swebench-single (baseline arm) --> official evaluation                |
|                                                                                           |
|  eval/swebench/runs/<run-id>/<arm>-<instance>/  predictions.jsonl, report.json            |
|  eval/swebench/summary.json + summary.md        (the numbers travel with the arms)        |
+-------------------------------------------------------------------------------------------+
```

## Goals / Non-Goals

**Goals:**

- One pinned 50-instance sample that all three arms run, selected by a rule anyone can re-apply to
  the official 500-instance test split.
- The three arms are configuration data: tool, tier, model, credential, and `semantic_search` on or
  off with the embeddings profile that makes it on.
- The harness executes exactly the arms the definition names, so a two-budget-arm matrix does not
  collide on a tier-named directory or on the adapter's run id.
- The on arm's prepared `LUNATE_HOME` is generated from its definition, committed in the run tree,
  and differs from the off arm only in `tools.extra` (plus the embeddings usage that only the on arm
  needs).
- The summary carries the arm id and a per-instance/per-arm `semantic_search` call count, so the
  keep/remove verdict has a measured input and not only a resolve rate.
- Tests pin the selection rule, the arm definitions, the generated home and the three-arm execution
  without Docker.

**Non-Goals:**

- Running the three arms. That needs Docker, the official images and live credentials; it happens on
  the run host, and this change delivers the configuration, the harness and the tests.
- A frontier arm, a decision arm in this run, repeated-sample statistics or a significance test.
- Any product (C#) change: `semantic_search`, its settings and its events already exist.

## Decisions

### The arm list stays under `models`, each entry gains `tool`, `semanticSearch` and `embeddings`

Every entry already names a model, and `tier` stays the arm's label. A `tool` field
(`lunate`/`mini-swe-agent`, default `lunate` for definitions written before this change) makes the
baseline an explicit arm instead of a mirror of the single-model tiers. `semanticSearch`
(`on`/`off`) is explicit for the two comparison arms; an `on` arm carries the `embeddings` model and
credential its prepared home binds.

*Alternatives considered:* renaming the key to `arms` (a large test churn for no behavioural gain,
and every existing definition still loads); a separate top-level `baseline` list (splits one matrix
into two and makes "every arm runs every instance" harder to read).

### The arm's identity is its explicit `arm` name, with `tier` as the fallback

The summary, the run directories and the adapter's run id are keyed by `arm_id(model)`:
`model["arm"]` when present, otherwise `model["tier"]`. Two budget arms therefore write
`lunate/semantic-search-off-<instance>` and `lunate/semantic-search-on-<instance>` instead of
colliding on `budget-<instance>`, and a definition written before this change keeps its directory
names. Old committed summaries have no `armId`, and their rendered summary is unchanged.

*Alternative:* suffixing the tier with the semantic-search state. Rejected: the name would encode one
axis of a definition that now has several.

### The on arm's home is generated, minimal and committed with the run

`semantic_search_home_files` renders `settings.json` with one `embeddings` profile over the
provider's preset, `usages.embeddings` bound to it, `tools.extra: ["semantic_search"]`, plus a
`providers.json` entry whose `credentialReference` is `env:<variable>`. The chat model stays on
`--model`, so the off arm runs with no home at all and the two arms' container commands differ only
by `--lunate-home`. The orchestrator writes the home under `<run>/lunate-home/<arm>`; the adapter
copies it into every instance run, so a committed run carries the exact settings.

*Alternatives:* a checked-in prepared home under `eval/swebench/` (a second copy of the definition
that can drift from `run.json`); a wrapper that always writes a home for every arm (the off arm
would then also have a home, and the "identical apart from the home" property the spec records for
the previous comparison would be lost).

### Spend is priced per record for any arm that runs more than one model

The on arm's trajectory carries chat records and embeddings records with distinct model ids and
distinct prices. Such an arm is priced like the decision arm: the sum of the per-record `costUsd`
values, and one unpriced record makes the instance unpriced. A single-model arm keeps the lane price
of its one model, as before. A usage record naming the embeddings model is an embeddings call, not
an agent step, so the on arm's `steps` stays comparable with the off arm's.

### `semantic_search` calls are counted from `tool_call_start` events

A call is a `tool_call_start` whose `toolName` is `semantic_search`; one call can sit inside a step
and never becomes one. The count is per instance row and summed per arm. An arm that does not
enable the tool records zero, not unknown.

*Alternatives:* counting `tool_call_result` (a refused call would still be a call, so the start event
is the honest signal); counting embedding requests (that is index building, not tool use).

### The sample is a stride over the recorded lexicographic order

Indices 0, 10, …, 490 of the 500-instance Verified test split: 50 ids, re-derivable from the
committed `tests/fixtures/swebench/verified-test-ids.txt` and from the official dataset, and a
superset of the previous five-instance subset.

*Alternatives:* a seeded random sample (needs the seed committed and a random source; the stride is
the rule the repository already used); all 500 instances (a cost the US$4.00 cap cannot cover).

### The previous five-instance run stays a record

`eval/swebench/summary.json`, `summary.md` and `docs/dev/eval/swebench-first-run.md` are the record of
the run that produced them; they are not rewritten. Its definition is copied into that run's evidence
directory (`runs/rerun-624-bosgame-2/definition/`) so a reader can tell which definition produced the
committed summary after the pinned definition moves on.

## Risks / Trade-offs

- **The cap stops the run before all 50 instances.** The card caps one run at US$4.00; the
  orchestrator already splits the remaining budget across the pending arms and stops starting
  instances at the cap and reports the limit stop. The summary then reports what it got, and the
  write-up names the shortfall rather than a resolved rate for a partial sample -> mitigation is the
  existing limit-stop reporting plus a per-instance pre-flight estimate in the run report.
- **The on arm needs an embeddings credential.** `google/gemini-embedding-001` needs
  `GOOGLE_API_KEY`, which the run host may not have set; the arm cannot run without it -> the run
  definition names the variable beside the model, and the adapter forwards it by name like the chat
  credentials, so a missing value fails the preflight instead of silently running without the tool.
- **The embeddings index cache lands in the run tree.** `LUNATE_HOME` is inside the mounted
  per-instance run directory, so the cache is written under `lunate-home/cache/`; the run tree's
  `.gitignore` excludes it, keeping a committed run to predictions and official reports.
- **A 50-instance run on QEMU is slow.** The run host is expected to run the x86_64 images natively;
  the existing `DOTNET_EnableWriteXorExecute=0` note applies when it emulates -> the timeout and
  worker count stay the definition's job.
- **Two arms share one model, so a provider outage hits both.** Accepted: it is the controlled
  comparison the card wants, and a failed instance is recorded per arm, never dropped.

## Migration Plan

1. Replace the pinned subset and arm list; the loader accepts both the old and the new shapes, so no
   other definition in flight breaks.
2. The committed five-instance summary and write-up stay; a new run writes new evidence under
   `eval/swebench/runs/<run-id>/`.
3. Rollback is the revert of the change: the previous `run.json`/`instances.txt` are in git history
   and the loader still reads them.

## Open Questions

None for the code. The run itself (three arms, 50 instances, the US$4.00 cap, the reported call
counts and the keep/remove verdict) needs the run host, Docker with the official images and live
credentials — the card's own maintainer steps.
