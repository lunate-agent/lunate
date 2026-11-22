# Budget tier: where Lunate's 50 steps go

**Date:** 2026-11-22 · **Run:** `rerun-624-bosgame-2` · **Model:** `opencode-go/deepseek-v4.1-flash` ·
**Evidence:** the run's committed summary and official reports under
[`eval/swebench/runs/rerun-624-bosgame-2/`](../../../eval/swebench/runs/rerun-624-bosgame-2/), and the
Lunate trajectories in the lane inbox under `624/rerun-624-bosgame-2/lunate/`.

The budget arm resolved 2/5 and four of its five runs ended at the 50-step limit. This write-up reads
each Lunate trajectory and says where those steps went. The short answer: **the steps did not go to
tool errors or to hard work. Three of the four failures never made a source edit at all; they spent
26–32 steps downloading and comparing upstream releases. The fourth fixed the bug at step 12 and
spent the next 38 steps debugging a regression test it had written itself.** The one success spent
26 of its 32 steps reading, then five editing and testing. The repeated-read and early-stop problems
are the ones worth fixing; the 50-step ceiling is only where they become visible.

## The run, in steps

The official score is unchanged from [the first-run write-up](swebench-first-run.md): Lunate 2/5,
mini-swe-agent 5/5 on the same subset and model. These are the per-instance budget rows from the
committed [`summary.json`](../../../eval/swebench/summary.json):

| Instance | Lunate steps | Lunate result | mini-swe-agent steps | mini-swe-agent result |
| --- | ---: | --- | ---: | --- |
| `astropy__astropy-12907` | 50 | resolved, step limit | 14 | resolved |
| `django__django-13128` | 50 | no patch, step limit | 20 | resolved |
| `django__django-15572` | 50 | patch, unresolved, step limit | 10 | resolved |
| `pydata__xarray-3151` | 32 | resolved, stop | 11 | resolved |
| `sphinx-doc__sphinx-8120` | 50 | no patch, step limit | 35 | resolved |

The card quotes the baseline as 9/21/10/12/29; the committed summary for this same run records
14/20/10/11/35. This write-up uses the committed values. The shape is the same either way: the
baseline finishes in 10–35 steps and Lunate reaches 50.

The card also notes the frontier tier's shape (4/14/6/7/9). That is the *Lunate frontier* arm; it
shows that a stronger model with the same harness can finish quickly, but it does not explain the
budget model's failure mode. The frontier arm's one failure, `django__django-13128`, produced an
empty patch after 14 steps of local reads and edits; its trajectory does not show the budget
model's upstream detour.

## How the steps were counted

A **step** is one model call, the `usage_updated` event in `lunate -p --json`; it is the number the
run limit counts and the number in `summary.json`. A step can contain more than one tool call: the
budget arm made **276 tool calls in 232 steps** (1.0–1.4 calls per step, at most two per step). The
baseline's `steps` is its `api_calls`, so the comparison is model calls against model calls.

For each trajectory I grouped every `tool_call_start` by the `usage_updated` event before it, then
classified the call from its `bash` command or file path: read-like (`cat`, `sed`, `grep`, `wc`,
`awk`, `git log`, `git diff`, `read`), test (`pytest`/`runtests.py`), edit (`edit` tool or a command
that writes), download/extract (`pip download`, `unzip`, `tar`, `zipfile`), and run (a reproduction
command). Error calls are the results with `isError: true`. Repeated reads count the distinct steps
that read the same file; a `grep` and a `sed` of the same file in different steps both count.

The baseline's `.traj.json` files are not in the lane inbox (only its predictions and official
reports are), so the comparison to mini-swe-agent below uses its recorded step counts and its
documented design, not a replay of its commands. The Lunate trajectories are the primary evidence.

## Where the steps went

| Instance | Steps | Calls | First source edit | Last test run | Non-zero tool results | Patch | Official |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| `astropy__astropy-12907` | 50 | 50 | 12 | 41 | 1 | source + test | resolved |
| `django__django-13128` | 50 | 56 | none | none | 3 | empty | unresolved |
| `django__django-15572` | 50 | 70 | none | none | 4 | scratch copies | unresolved |
| `pydata__xarray-3151` | 32 | 33 | 27 | 31 | 1 | source + test | resolved |
| `sphinx-doc__sphinx-8120` | 50 | 67 | none | none | 2 | empty | unresolved |

“Non-zero tool results” includes search commands that exit non-zero after printing a match and one
`diff` that exits 1 because the files differ; it is not all error-and-retry. The real retries are
counted in the sections below.

### `astropy__astropy-12907` — fixed at step 12, then 38 steps of self-authored test

The source fix is one line in `_cstack`: copy the right-hand matrix instead of filling its block with
ones. The model made it at **step 12**, after 11 read steps. The patch the official harness scored
contains that fix and a regression test, and it resolved. The run still ended at 50 because of what
came after the fix:

- steps 13–24: reproduce the fix, run the existing model tests, read the test file and add two
  regression cases; the model's own test then fails with `NotImplementedError` from a
  `CompoundModel` path;
- steps 25–41: re-read `separable.py` and the test file, try to understand the failure, hit a
  stale-edit refusal at step 34 (re-read at 35), then add more test edits and run three more tests;
- steps 42–50: re-read the source and test file, compare with `git diff`, hit the limit.

Of the 50 steps, 11 were exploration before the fix, 1 was the fix, and **38 were verification and
test debugging**. It ran the tests nine times. The model had already solved the task at step 12; the
remaining steps bought no official result. `astropy/modeling/separable.py` was read in **18**
separate steps and `astropy/modeling/tests/test_separable.py` in **10**.

### `django__django-13128` — 31 steps of upstream source archaeology, no edit

The model spent steps 1–19 reading `django/db/models/expressions.py` (21 read steps for that file
across the run) and the expressions tests. At step 20 it ran `pip download django==3.2` inside
`/testbed`, which wrote wheels into the repository. Steps 21–27 extracted the downloaded package
(`unzip` was missing, so it used Python's `zipfile`), then read the upstream `expressions.py`.
Steps 28–47 alternated between the local file and the upstream copy, tried `django==4.0` (not
available from the configured index), and read the upstream file again. It removed the wheels at
step 23 but never edited a source file. The final prediction is empty.

The step budget divides cleanly: **19 local read steps and 31 download/upstream-read steps**. The
upstream detour is not a tool error and not a retry; it is a strategy the harness and prompt permit.

### `django__django-15572` — 26 steps downloading Django 4.1, then a scratch-file “patch”

Steps 1–13 read the local template engine, autoreload and test files. Step 14 downloaded the Django
4.1 wheel to `/tmp/dj41`. Step 15 failed because `unzip` is not installed; steps 16–21 extracted the
wheel with Python, then two `read` calls to `/tmp/dj41/...` were refused because the paths are
outside the workspace (the model switched to `python`/`cat` through `bash`). Steps 22–39 compared
local files with the 4.1 wheel and source tarball; step 40 copied three upstream files into
`/testbed/tmp_41_*`; steps 41–50 read and compared again.

The run's non-empty “patch” is exactly those scratch copies (`tmp_41_autoreload.py`,
`tmp_41_autoreload_test.py`, `tmp_41_base.py`), not a change to `django/template/autoreload.py`. The
official harness evaluated it and did not resolve it. **No source edit was ever made.**

### `sphinx-doc__sphinx-8120` — 32 steps comparing Sphinx releases, no edit

Steps 1–16 read `sphinx/locale/__init__.py` (14 read steps across the run), `sphinx/util/i18n.py`
(9) and `sphinx/application.py` (8). Steps 17–18 downloaded Sphinx 3.0.0/3.4.0/3.5.0; `unzip` was
missing, so steps 19–24 extracted with Python and read the upstream files. Steps 25–40 downloaded
3.3.0/3.3.1, compared them, read local and upstream `locale/__init__.py` and `application.py`, and
checked release notes. Steps 41–48 downloaded 4.0.0 and 4.5.0 and compared again; steps 49–50 ran
commands. No source edit, empty patch.

The pattern is the same as the two Django failures: the model looked for the upstream fix instead
of making one, and the environment allowed it to spend the whole budget doing so.

### `pydata__xarray-3151` — solved, but 26 steps to locate a five-line bug

The fix is at step 27: check monotonicity only on the dimensions actually being concatenated. The
model then added a regression test at step 30, ran the test at 31, and finished at 32. The cost is
the 26 steps before the fix:

- 10 steps read `xarray/core/combine.py` in overlapping slices (`1–120`, `380–540`, `540–620`,
  `200–380`, `393–530`, `500–530`, `393–520`, `330–410`, …);
- 9 more steps printed line ranges with Python snippets from the same file;
- 3 steps read the tests, and one `grep` returned non-zero.

It never re-read the file after the fix except for one check at step 28. This is the cheapest
failure shape to improve: the answer was in a 716-line file, and the model took 22 steps to find
it.

## Repeated reads

In every instance the most-read file was read in 10 or more separate steps. The table counts
distinct steps that read the file with `read`, `cat`, `sed`, `grep`, `awk` or a Python line-range
script; a file opened twice in one step counts once.

| Instance | File | Steps that read it |
| --- | --- | ---: |
| `astropy__astropy-12907` | `astropy/modeling/separable.py` | 18 |
| `astropy__astropy-12907` | `astropy/modeling/tests/test_separable.py` | 10 |
| `django__django-13128` | `django/db/models/expressions.py` | 21 |
| `django__django-13128` | upstream `pkg/django/db/models/expressions.py` | 11 |
| `django__django-15572` | `django/template/engine.py` | 10 |
| `django__django-15572` | `django/template/autoreload.py` | 10 |
| `django__django-15572` | `tests/template_tests/test_autoreloader.py` | 8 |
| `pydata__xarray-3151` | `xarray/core/combine.py` | 10 |
| `sphinx-doc__sphinx-8120` | `sphinx/locale/__init__.py` | 14 |
| `sphinx-doc__sphinx-8120` | `sphinx/util/i18n.py` | 9 |
| `sphinx-doc__sphinx-8120` | `sphinx/application.py` | 8 |

The model often re-read overlapping windows instead of reading the file once or jumping to the
error with `grep -n`. The read ledger already replaces superseded read results in the request with a
one-line stale notice, but it does not tell the model that it has already seen those lines in this
run. Nothing in the prompt says “read once”; the prompt's only file rule is “read a file before you
edit it”.

## Tool errors are not the sink

There were 11 non-zero tool results across the five runs, in nine steps. Excluding the search/diff
commands that exit non-zero while doing their job, the real errors are:

| Instance | Error | Cost |
| --- | --- | --- |
| `astropy__astropy-12907` | `edit` refused because the test file changed after the last read | one re-read and one retry (steps 34–35, 38) |
| `django__django-13128` | `ls *.whl` in the wrong directory; `unzip` unavailable; `django==4.0` unavailable | three retries inside the upstream detour |
| `django__django-15572` | `unzip` unavailable; two `read` calls outside the workspace refused | one extraction retry and a switch to `bash` |
| `pydata__xarray-3151` | a `grep` over a missing path returned 2 | no retry |
| `sphinx-doc__sphinx-8120` | `unzip` unavailable twice | one extraction retry |

The errors cost single-digit steps. The failures cost tens. The tool messages themselves are
actionable (`unzip: command not found`; the outside-workspace refusal names the allowed root), and
the model recovered from each. Removing the 50-step limit would not have fixed the three no-edit
runs; the model would have kept reading upstream releases.

## What mini-swe-agent does differently

The card names five choices: bash-only, one command per step, a short prompt, linear history and a
fresh subprocess per command. Against this evidence:

| Choice | Lunate today | Would it cut steps? | Why |
| --- | --- | --- | --- |
| Bash-only | `bash`, `read`, `edit`, `write` (and optional search tools) | **A little** (2–5 steps) | Removes the stale-edit refusal and the outside-workspace read refusal; it does not stop repeated reads or upstream downloads. |
| One command per step | Already 1.0–1.4 calls per step, max two | **No** | Serializing 276 calls into 276 model calls would add up to 44 steps (~19%) and remove the batching that currently saves steps. |
| Short prompt | A short composed prompt plus `AGENTS.md` and tool conventions | **Yes, if it carries task rules** | The current prompt is short but says nothing about upstream downloads, test edits, reading once or stopping. The content, not the length, is what the failures need. |
| Linear history | Already linear; no compaction in these runs | **No** | The session and trajectory are a parent chain. There is no summarisation step in these 50-step runs to remove. |
| Fresh subprocess per command | Already true | **No** | `BashTool` runs one non-interactive command in the resolved shell in the workspace root per call. Freshness is not the difference; the workspace restriction is. |

Two further mini-swe-agent properties are worth separating from the five: it ends with an explicit
submission, and its SWE-bench prompt keeps the model in the repository instead of fetching a newer
release. Those are the two biggest levers in this evidence, so they appear as proposals below.

## Proposals

Each proposal is self-contained: a follow-up card can take one without the others. They are ordered
by expected step saving divided by cost, not by implementation order in one pull request.

### Proposal 1 — `swebench-task-profile`: a short, task-specific prompt for benchmark runs

**Change.** Add an opt-in prompt profile for unattended benchmark runs (or have the SWE-bench
adapter pass the same text through the existing `--append-system-prompt`). The profile says: work
in the repository, do not `pip download`/fetch/diff upstream releases; read a file once and then use
`grep -n` and targeted ranges; change the source, not the tests, unless the task asks for a test
change; run the existing relevant tests once; when the reproduction and tests pass, stop and submit.

**Evidence.** Three of the four failures made no source edit and spent 26–32 steps on upstream
archaeology. `astropy__astropy-12907` spent 38 steps after its fix, largely on a test it wrote
itself. The current [`system-prompt.md`](../../../src/Lunate.Coding/system-prompt.md) is already
short, but it does not contain any of these rules; `--system-prompt` and `--append-system-prompt`
already exist in [`cli.md`](../../cli.md), so the adapter can test the profile without a product
change.

**Expected saving.** 20–35 steps on the three upstream cases, 10–30 on the astropy trailing-test
case, 5–10 on xarray.

**Risk.** A prompt tuned on five instances can overfit. Keep it opt-in, record the exact prompt in
the run, and rerun the same five instances before claiming a gain.

### Proposal 2 — `submit-tool`: make “done” an action, not an absence of tool calls

**Change.** Add a `submit` tool (or a `--finish-tool` profile) that the model calls when it considers
the task complete. The call records the final answer and ends the run with
`run_finished(stopReason: completed)`; the loop stops immediately instead of waiting for a model
message with no tool call. The benchmark prompt says to call it after the existing tests pass.

**Evidence.** `astropy__astropy-12907` fixed the bug at step 12 and then spent 38 steps on its own
test; it ended at the step limit, not by choice. `pydata__xarray-3151` stopped at 32 because it
returned a final message, so the loop can stop, but the run has no explicit, checkable completion
signal. A submit action also gives the harness a clean place to record “the model said it was
done” separate from “the step limit fired”.

**Expected saving.** Up to 37 steps on the astropy shape; 5–10 on any run that keeps re-verifying
after the tests pass.

**Risk.** A model can submit prematurely. Make the tool opt-in for benchmark/unattended runs, and
have the prompt require one passing test command before the submission. A new tool changes the
agent/tool contract and needs its own OpenSpec change. Proposal 1's “stop after the existing tests
pass” rule gets part of the saving without a new tool; try that first.

### Proposal 3 — `bash-only-benchmark-arm`: test the existing `--tools bash` selection

**Change.** Run the same five instances with `--tools bash` (and no `read`/`edit`/`write`) to match
mini-swe-agent's single-tool surface. If the arm helps, add a named `bash` tool profile; if not,
record the result and keep the default tools. `--tools` already exists and takes an exact
comma-separated selection.

**Evidence.** The tool-surface errors cost steps: astropy's stale-edit refusal and re-read (steps
34–35, 38), and `django__django-15572`'s two outside-workspace read refusals followed by a switch to
`bash` (steps 19–21). A bash-only surface can read `/tmp` and edit with `python`/`sed` without those
contracts, but it does not fix repeated reads or upstream downloads; those are prompt and
tool-guidance problems. **Expected saving:** 2–5 steps on the affected runs; a controlled
experiment, not the main fix. **Risk:** losing the read ledger and edit verification makes wrong
edits more likely, so keep it opt-in and compare the resolved count, not only the steps.

### Proposal 4 — `read-once-ledger`: tell the model it has already read those lines

**Change.** Extend the existing read ledger (which already records the newest read of each path for
edit safety) to remember the line ranges a run has read. A second `read` of an unchanged range
returns a compact “already read lines X–Y at step N; the file has not changed” pointer instead of
the same text, or at least adds that sentence to the result. Start with the pointer, not a refusal;
a hard refusal would block a legitimate re-read after a change.

**Evidence.** The most-read file was read in 10–21 steps in every instance, and astropy read
`separable.py` in 18 steps. The model often fetched overlapping windows of the same file (xarray's
`combine.py` in 10 steps), which the ledger can see and the model cannot. **Expected saving:**
5–15 steps per instance; the xarray success alone has 10 steps of `combine.py` reads. **Risk:**
range bookkeeping is easy to get wrong across edits and bash changes; use the ledger's existing
digest/invalidations, only claim a range after a successful text read, and clear it when the file
changes.

### Proposal 5 — `offline-benchmark-runs`: remove the upstream-source detour

**Change.** Run the benchmark offline, or deny network-fetching commands (`pip download`, `curl`,
`wget`) in the benchmark profile. The repository and the prebuilt test environment are enough to
solve the task; a package a test genuinely needs belongs in the image. The adapter already controls
the container environment, so this can be an environment/guard setting on the benchmark arm rather
than a product default.

**Evidence.** `django__django-13128` spent 31 steps, `django__django-15572` 26 steps and
`sphinx-doc__sphinx-8120` 32 steps downloading, extracting and comparing upstream releases. The
baseline solved those three in 20, 10 and 35 steps (committed summary) without that detour.
**Expected saving:** 20–32 steps on the three failures, the largest single sink in the run. **Risk:**
a task whose test needs a package not in the image would now fail; the official images are built
for the task's tests, so make this opt-in per benchmark run and record it in the run definition. A
network guard must not break the harness's own evaluation, which runs outside the
agent container.

### Not proposed

- **One command per step.** It would increase the step count here. Lunate already issues one command
  in most steps; the 44 extra tool calls in 232 steps are batched *inside* steps. Serializing them
  would turn them into model calls. If the goal is cleaner observations, that is a different
  problem from this card.
- **Linear history.** Already true. The session and trajectory are a linear parent chain, and
  these runs had no compaction.
- **A fresh subprocess per command.** Already true. `BashTool` starts a new non-interactive shell
  per call. The difference that matters in this run is that `read` is restricted to the workspace,
  which Proposal 3 addresses for the benchmark arm.

## What to measure next

Take Proposal 1 and Proposal 5 first: both can be tested by the SWE-bench adapter without a product
change (append the prompt and block fetches in the container), and together they target the 26–32
step upstream detour and the astropy test detour. Then add Proposal 4 for the remaining repeated
reads, and Proposal 2 if the runs still trail the baseline's step count after the tests pass.

Re-run the same five instances, same model, same cap, same official harness. Record per instance:
steps, resolved, patch presence, spend, and whether the run stopped by `completed`/`submit` or by
`step_limit`. The target is not “more than 50 steps”; it is the budget baseline's range (10–35
steps here) with the same or better resolved count. A run that hits the limit after a correct patch
still fails the card's premise.

## Related

- [SWE-bench Verified — first published run](swebench-first-run.md) — the run's numbers, spend,
  failures and scope.
- [`eval/swebench/run.json`](../../../eval/swebench/run.json) and [`eval/README.md`](../../../eval/README.md)
  — the pinned subset, models, limits, adapter, step definition and run commands.
- [`eval/swebench/runs/rerun-624-bosgame-2/`](../../../eval/swebench/runs/rerun-624-bosgame-2/) —
  predictions and official per-instance reports.
