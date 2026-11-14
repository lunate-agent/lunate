# Design: committed run evidence and durable links

## Context

`first-run-2026-11-08` was run on 2026-11-08 (Lunate) and 2026-11-10 (mini-swe-agent). Its committed
records are `summary.json`, `summary.md`, `first-run-2026-11-08.lunate.json` and
`first-run-2026-11-08.baseline.json`. The per-instance working trees lived under the run host's
`$TMPDIR` (`/private/var/.../T/lunate-bench/first-run-work/first-run-2026-11-08/`), and the
orchestrator's default `--work-dir` was `eval/runs/swebench-first-run`, which `.gitignore` excludes.
The Lunate rows of `summary.json` therefore carry an absolute temporary path and the baseline rows
carry an ignored relative one; either way a clean checkout cannot read the evidence, and the
write-up pointed at `eval/runs/` as the place to audit.

Nothing in the repository or on this machine held the first run's raw outputs any more: no
`predictions.jsonl`, no `patch.diff`, no `trajectory.jsonl`, no per-instance `report.json`. The
evidence that still existed was the committed record: for every instance and arm, whether a patch
was produced and whether the official harness resolved it, with the spend, steps and tokens the arm
summary records.

A maintainer then re-ran the benchmark outside the lane (`rerun-624-bosgame-2`, same harness, same
pinned subset, a US$5 cap) and supplied its predictions, its official per-instance `report.json`
files and its summary. That run's artifacts replace the first run's transcribed record, so the
published write-up, summary and README claim are backed by real patch text and real official
reports rather than by a transcription.

## Goals / Non-Goals

**Goals:**

- Every instance row of the committed summary links to committed evidence that a clean checkout and
  a reader of the pull request can open.
- The link is checked by a test, so a dead or machine-local path cannot merge again.
- A run started after this change writes its working tree to a tracked location, so its links are
  durable by construction.
- What the run host no longer holds is stated in the repository, not silently reconstructed.

**Non-Goals:**

- Regenerating artifacts from a machine the worker controls. The rerun was a maintainer step on the
  same definition; a worker does not re-run the benchmark to make the evidence fit a claim.
- Reconstructing patch text or trajectories from anything other than the run host: inventing them
  would put fabricated evidence behind a public benchmark claim.
- Changing what the arms measure, the subset, the cap or the scorer.

## Decisions

### D1 — The summary row is the link, the committed artifacts are the evidence

Each instance row keeps `workDir` and it becomes a repository-relative path into
`eval/swebench/runs/<run-id>/<arm>/<tier>-<instance>/`, the directory holding that instance's
`predictions.jsonl` and, when it submitted a patch, its official `report.json`. A reader follows
write-up → summary row → the prediction the harness scored → the official report, and sees the
`resolved` and `patch_exists` fields that the row's `resolved` and `hasPatch` values came from.
Absolute paths and `eval/runs/` paths are excluded by the new test.

### D2 — Work trees live in the tracked tree; only the bulk is ignored

The orchestrator's `--work-dir` default changes from `eval/runs/swebench-first-run` to
`eval/swebench/runs`, so `run_dir.relative_to(REPOSITORY)` in `read_lunate_row` always succeeds and
records a checkout-relative path. An `eval/swebench/runs/.gitignore` keeps the bulk of a working
tree out of a commit — `trajectory.jsonl`, `*.traj.json`, `*.log`, `dataset.json`, `exit-code`,
`prompt.md`, `run.sh`, `collect_patch.py` — while leaving the predictions, the official results and
the run README to be committed deliberately.

### D3 — The run's own predictions and reports are the evidence; nothing is transcribed

After the rework, each committed instance directory carries the exact `predictions.jsonl` line the
official harness scored and the official `report.json` the harness wrote, including its
`FAIL_TO_PASS`/`PASS_TO_PASS` results. An instance that submitted no non-empty patch has no report,
because the adapter did not ask the harness to evaluate an empty patch; its empty prediction is
committed and its row records `resolved: false`. Nothing is transcribed by hand: the alternative —
writing plausible diffs or verdicts so the directory looks complete — would fabricate the model's
output behind a published claim, which is worse than an honest gap. Trajectories, the adapter's
`run.json`, the resolved dataset and the harness logs stay on the run host and are declared as not
retained in the run README and the write-up.

### D4 — The test pins the chain, not the file count

`test_every_instance_row_links_committed_official_evidence` walks every arm and instance of the
committed `summary.json`, rejects an absolute `workDir` and a path outside `eval/swebench/runs`,
requires a committed prediction whose instance, model and patch presence agree with the row, and
requires the official `report.json` to agree on `resolved` and `patch_exists` whenever it exists —
an instance without a report must be one the row records as unpatched and unresolved. A future run
that forgets to commit its evidence fails it. `test_the_default_work_dir_is_the_committed_runs_tree`
fails if the default `--work-dir` drifts back under the ignored tree.

## Risks / Trade-offs

- [A run's raw trajectory is not committed] -> The run README and the write-up say so; the committed
  prediction and official report still support every number that is published.
- [Committing a working tree grows the repository] -> Only predictions, official reports and the
  summary are committed; trajectories and logs are ignored by the `.gitignore` in
  `eval/swebench/runs/`.
- [A future run writes untracked files into the repository] -> That is the intent: the evidence is
  committed deliberately, and `git status` shows what a run left behind.
- [The rerun is a different sample than the first run] -> It is a maintainer-run rerun of the same
  pinned definition; the write-up states that a five-instance sample moved between the two, which is
  why it makes no reliability claim.

## Migration Plan

No product migration. The rerun's evidence tree, the committed summary and the write-up are
re-pointed in the same change that commits them. An old run whose host is gone keeps its record and
gains the stated gap; a new run needs no manual re-pointing.

## Open Questions

None blocking. Whether a later run's trajectory is committed (it can be large) or left out is an
operator choice per run; the repository records which of the two happened.
