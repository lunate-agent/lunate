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

Nothing in the repository or on this machine holds the run's raw outputs any more: no
`predictions.jsonl`, no `patch.diff`, no `trajectory.jsonl`, no per-instance `official-report.json`.
The evidence that still exists is the committed record: for every instance and arm, whether a patch
was produced and whether the official harness resolved it, with the spend, steps and tokens the arm
summary records.

## Goals / Non-Goals

**Goals:**

- Every instance row of the committed summary links to committed evidence that a clean checkout and
  a reader of the pull request can open.
- The link is checked by a test, so a dead or machine-local path cannot merge again.
- A run started after this change writes its working tree to a tracked location, so its links are
  durable by construction.
- What the run host no longer holds is stated in the repository, not silently reconstructed.

**Non-Goals:**

- Re-running the benchmark to regenerate artifacts. That would replace measured evidence with a
  different sample and spend money; the record is the run.
- Reconstructing patch text or trajectories from anything other than the run host: inventing them
  would put fabricated evidence behind a public benchmark claim.
- Changing what the arms measure, the subset, the cap or the scorer.

## Decisions

### D1 — The summary row is the link, the committed result is the evidence

Each instance row keeps `workDir` and it becomes a repository-relative path into
`eval/swebench/runs/<run-id>/<arm>/<tier>-<instance>/`, the directory holding that instance's
`official-results.json`. A reader follows write-up → summary row → official record and sees the
`resolved_ids` entry that the row's `resolved` value came from. Absolute paths and `eval/runs/`
paths are excluded by the new test.

### D2 — Work trees live in the tracked tree; only the bulk is ignored

The orchestrator's `--work-dir` default changes from `eval/runs/swebench-first-run` to
`eval/swebench/runs`, so `run_dir.relative_to(REPOSITORY)` in `read_lunate_row` always succeeds and
records a checkout-relative path. An `eval/swebench/runs/.gitignore` keeps the bulk of a working
tree out of a commit — `trajectory.jsonl`, `*.traj.json`, `*.log`, `dataset.json`, `exit-code`,
`prompt.md`, `run.sh`, `collect_patch.py` — while leaving the predictions, the official results and
the run README to be committed deliberately.

### D3 — An official result is a transcription; a patch would be an invention

The committed `official-results.json` per instance carries exactly the fields the run record
supports: one instance, whether a submission existed, whether the official harness resolved it,
and the matching id lists. It reproduces no test names: the per-test `report.json` payload is also
gone. The patch text and the trajectories are declared as not retained in the run README and in
the write-up. The alternative — writing plausible diffs so the directory looks complete — would
fabricate the model's output behind a published claim, which is worse than an honest gap.

### D4 — The test pins the chain, not the file count

`test_every_instance_row_links_committed_official_evidence` walks every arm and instance of the
committed `summary.json`, rejects an absolute `workDir` and a path outside `eval/swebench/runs`,
asserts the record exists, and asserts its verdict agrees with the row (`resolved` against
`resolved_ids`, `hasPatch` against `submitted_instances`). A future run that forgets to commit its
results fails it. `test_the_default_work_dir_is_the_committed_runs_tree` fails if the default
`--work-dir` drifts back under the ignored tree.

## Risks / Trade-offs

- [A run's raw patch text is gone] -> The run README and the write-up say so; the committed
  official result and the row still support every number that is published.
- [Committing a working tree grows the repository] -> Only predictions and official results are
  committed; trajectories and logs are ignored by the `.gitignore` in `eval/swebench/runs/`.
- [A future run writes untracked files into the repository] -> That is the intent: the evidence is
  committed deliberately, and `git status` shows what a run left behind.

## Migration Plan

No product migration. The committed summary, the two arm records and the write-up are re-pointed at
the committed evidence in the same change that commits it. An old run whose host is gone keeps its
record and gains the stated gap; a new run needs no manual re-pointing.

## Open Questions

None blocking. Whether a later run's raw patch text is committed (it is small) or left out (it can
be large) is an operator choice per run; the repository records which of the two happened.
