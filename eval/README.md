# Eval suite

The task suite is the scoreboard for real quality (T-17): small coding tasks with a check script each, driven by `lunate -p --json --yolo`. Results land in `results.csv`, one row per suite run; the T-35 head-to-head uses the same tasks and rows.

## Task format

Each task is a directory under `tasks/`:

| File | Purpose |
| --- | --- |
| `task.md` | The prompt handed to the agent verbatim (`"$(cat task.md)"`). |
| `check.sh` | Runs inside the task's working copy with `bash`; exit 0 = pass, anything else = fail. |
| `repo/` | The fixture files copied into a fresh temp workdir before the run. |

The fixtures are intentionally flawed: the agent's job is to make `check.sh` pass. Fixture projects set their own `TargetFramework`; `tasks/Directory.Build.props` is an empty stub that stops MSBuild's walk-up, so the repository's warnings-as-errors settings do not apply. `eval/` is excluded from CSharpier, is not part of `lunate.slnx`, and is out of every repository gate.

Check scripts use no live APIs and need no network, except `csharp-failing-test`: its `dotnet test` restores `xunit.v3` from NuGet on a cold cache (warm caches run offline). C# tasks use `dotnet test`, `dotnet run` or `dotnet build`; the Python task uses `python3 -m unittest` (stdlib only, no pytest). A fixture must fail its check before the fix and pass after it.

## Running the suite

```bash
dotnet build src/Lunate.Coding -c Release
scripts/eval.sh --model <exact-model-id> --phase 3
```

The same runner drives the T-35 head-to-head with `--tool`:

```bash
scripts/eval.sh --tool claude --phase 5
scripts/eval.sh --tool opencode --model opencode-go/deepseek-v4.1-flash --phase 5
scripts/eval.sh --tool pi --model deepseek/deepseek-v4.1-flash --phase 5
scripts/eval.sh --tool tau --model deepseek/deepseek-v4.1-flash --phase 5
```

Head-to-head setup: `claude` needs the **C# LSP plugin** (`claude plugin install csharp-lsp`, official directory) plus the `csharp-ls` language server on `PATH` (`dotnet tool install --global csharp-ls`); `opencode` uses its built-in C# LSP (csharp-ls) as shipped. Both are invoked headlessly (`claude -p … --output-format json --dangerously-skip-permissions`, `opencode run --format json --auto`). `pi` (npm `@earendil-works/pi-coding-agent` — *not* the unrelated `@badlogic/pi` vLLM deploy tool) and `tau` (the Python port of pi, `uv tool install tau-ai` — *not* the unrelated `dpc/tau` Unix-process agent) both run via OpenRouter with `OPENROUTER_API_KEY`: `pi -p --mode json --no-session --provider openrouter --model <id>` and `tau -p --mode json --provider openrouter --model <id>`. tau ships a model catalog that may lack newer ids — a minimal user overlay (`~/.tau/catalog.toml`) adds them (models lists union on merge). The claude model is whatever the CLI is configured with — it is read from the run's own JSON, not from `--model`.

| Option | Meaning |
| --- | --- |
| `--model <id>` | Required for `lunate` (exported as `LUNATE_MODEL`), `opencode` (passed as `-m`), `pi` and `tau` (passed as `--model`); ignored for `claude` (the row records the model the CLI reported). |
| `--tool <name>` | Which tool to run: `lunate` (default), `claude`, `opencode`, `pi` or `tau`. |
| `--filter <task>` | Run one task by directory name. |
| `--phase <n>` | The `phase` column value (default `3`). |
| `--lunate-bin <path>` | The build to run; a `.dll` is invoked via `dotnet` (default `src/Lunate.Coding/bin/Release/net10.0/lunate.dll`). |
| `--keep` | Keep every task working copy, also on pass. |

Per task the runner makes a fresh `mktemp -d`, copies `repo/`, runs the selected tool there with stdout in `events.jsonl` and stderr in `run.log`, then runs `check.sh` in the same copy. Working copies are kept on failure (the path is printed) and removed on pass. `LUNATE_EVAL_RESULTS=<path>` overrides the results file (used by the offline stub proof).

Metrics come from the tool's own output: `steps` = model calls (`usage_updated` events for lunate, `num_turns` for claude, `step_finish` events for opencode, `turn_end` events for pi/tau), `total_tokens` = the summed token traffic (claude: input + cache creation + cache read + output; pi/tau: the per-turn `totalTokens`, falling back to input + output + cache read + cache write), `seconds` = wall clock of the agent run, `edit_tiers` = the distinct `matchTier` values of `edit` tool results (lunate only; `n/a` for the other tools, which have no tier concept).

## SWE-bench Verified adapter

`scripts/eval/swebench.py` runs Lunate's print mode inside the official SWE-bench Verified
instance images and hands the resulting patch to the official evaluation. It is the entry point
for the published SWE-bench number; it does not score anything itself.

Prerequisites, once per machine:

```bash
docker version                                  # a reachable daemon
python3 -m pip install swebench                 # the official evaluation package
dotnet publish src/Lunate.Coding -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o artifacts/linux-x64
export OPENROUTER_API_KEY=...                   # or the provider variable for your model
```

A one-instance dry run, which is the quickest way to debug the adapter:

```bash
python3 scripts/eval/swebench.py --dry-run \
  --instance astropy__astropy-12907 \
  --model deepseek/deepseek-v4.1-flash \
  --lunate-bin artifacts/linux-x64/lunate
```

The dry run defaults to `--evaluate never`. Add `--evaluate always` to hand the single patch to
the official harness, or omit `--dry-run` for a run whose `--evaluate` default is `auto`. The
adapter invokes `python -m swebench.harness.run_evaluation` with a local `dataset.json` built from
the selected official records, so the evaluation sees exactly the instances that were run.

| Option | Meaning |
| --- | --- |
| `--instance <id>` | Instance id to run; repeat for more than one. Required unless `--dry-run` selects it. |
| `--dry-run` | Run exactly one instance; evaluation defaults to `never`. |
| `--model <id>` | Exact Lunate model id, passed to `lunate -p --model`. |
| `--lunate-bin <path>` | Self-contained `linux-x64` single-file Lunate executable. A non-ELF file is refused with the publish command. |
| `--dataset-file <path>` | Local SWE-bench JSON array or JSONL records; use this for offline and reproducible runs. |
| `--dataset-name <name>` | Official dataset name when no local file is given (default `princeton-nlp/SWE-bench_Verified`). |
| `--split <split>` | Dataset split (default `test`). |
| `--output-dir <dir>` | Parent of the run directories (default `eval/runs/swebench`, ignored by git). |
| `--run-id <id>` | Run directory name (default a UTC timestamp). An existing non-empty directory is refused. |
| `--evaluate <auto\|always\|never>` | `auto` outside a dry run, `never` inside it. `always` evaluates even when a patch is empty. |
| `--env <name>` | Provider credential variable to forward by name; repeatable. Without it the known provider variables that are set are forwarded. |
| `--timeout <seconds>` | Container and official test timeout (default `1800`). |
| `--eval-python <path>` | Interpreter that can import the official `swebench` package (default: this Python). |
| `--docker <path>` | Docker client (default `docker`). |
| `--keep` | Keep the instance container for debugging instead of `--rm`. |

Each run writes `eval/runs/swebench/<run-id>/`:

| Path | Contents |
| --- | --- |
| `run.json` | Run id, model, dataset, forwarded environment variable *names*, per-instance exit code and relative artifact paths, official evaluation command and exit code, redaction count. |
| `predictions.jsonl` | The official prediction schema: `instance_id`, `model_name_or_path`, `model_patch`. |
| `dataset.json` | The selected official instance records, passed to the official evaluation. |
| `evaluation.log` | Combined stdout/stderr of the official harness. |
| `official-results.json` | The official run summary, when the harness produced it. |
| `<instance-id>/trajectory.jsonl` | The unmodified `lunate -p --json` event stream. |
| `<instance-id>/patch.diff` | Baseline-aware `git diff HEAD --binary` from the image's `/testbed`. |
| `<instance-id>/lunate.log` | Lunate diagnostics (stderr). |
| `<instance-id>/exit-code` | Lunate's semantic exit code. |
| `<instance-id>/official-report.json` | The official per-instance report, when produced. |

Credentials are environment-only. The adapter forwards each selected variable with `docker run -e
NAME` so the value never appears in the adapter's process arguments or in `run.json`; after the run
it scans the trajectory, logs, patch, predictions and report and replaces any occurrence of a
forwarded value with `[REDACTED:<NAME>]`.

The adapter pins `--platform linux/amd64` and needs a Docker host that can run the official x86_64 images (native or emulated). It does not build
images: it uses the image named by the dataset, or derives the official
`swebench/sweb.eval.x86_64.<id with __ -> _1776_>` name for older records, and pulls it when absent.

### ProgramBench is out of scope

ProgramBench is deliberately not covered by this adapter. Its inference images provide a compiled
binary and documentation rather than a git `/testbed`, and its submission is a `submission.tar.gz`
of the whole generated codebase evaluated by `programbench eval`; a SWE-bench patch has no place in
that handoff. A second adapter for ProgramBench, if wanted, is a separate change against the
official `programbench` CLI.

## Published SWE-bench run

The first published run is pinned in `eval/swebench/`:

| File | Contents |
| --- | --- |
| `instances.txt` | The fixed five-instance subset: every 100th id in lexicographic order of the 500-instance Verified test split. |
| `run.json` | The dataset and split, the budget and frontier models, one run cost cap, the timeout, the evaluation worker count and the mini-swe-agent settings. |
| `litellm-models.json` | The baseline models' token prices, because LiteLLM does not know these new ids. |
| `mini-swe-agent.yaml` | The `x-opencode-session` header that OpenCode Go requires and LiteLLM does not send. |

Run both arms and write the committed summary:

```bash
dotnet publish src/Lunate.Coding -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o artifacts/linux-x64
export OPENCODE_GO_API_KEY=... OPENROUTER_API_KEY=...
python3 scripts/eval/swebench_run.py --arm all \
  --lunate-bin artifacts/linux-x64/lunate --mini-extra mini-extra --run-id <run-id>
```

The arms can also be run and merged separately (for example when one arm resumes after an
interruption):

```bash
python3 scripts/eval/swebench_run.py --arm lunate --lunate-bin artifacts/linux-x64/lunate --run-id <run-id>
python3 scripts/eval/swebench_run.py --arm baseline --mini-extra mini-extra --run-id <run-id>
python3 scripts/eval/swebench_run.py --arm merge --run-id <run-id>
```

The first two write `<run-id>.lunate.json` and `<run-id>.baseline.json`; `merge` writes the
committed `summary.json` and `summary.md`. `--resume` reuses a completed Lunate instance run.

The orchestrator passes the run's remaining budget to each Lunate invocation as `--max-cost`, runs
mini-swe-agent on the same subset and models with its own cost limit, and takes every resolved count
from `swebench.harness.run_evaluation`. When an instance's spend cannot be priced, the run stops and
the summary reports that instance as unpriced and the run total as unknown — never the cap or a
number above it. The committed `eval/swebench/summary.json` and `summary.md` are the evidence; the
per-instance patches and trajectories stay under the ignored `eval/runs/`.

On an arm64 host running the official `linux/amd64` images under QEMU, export
`DOTNET_EnableWriteXorExecute=0`; the adapter sets invariant globalization itself because the
images have no ICU.

## Row schema

`results.csv` header (committed once): `date,phase,tool,model,tasks,passed,pass_rate,steps,total_tokens,seconds,edit_tiers,notes`.

One row per suite run: ISO date (UTC), phase, tool (`lunate`, `claude` or `opencode`), the exact model id (for claude, the model the CLI reported), task and pass counts, `pass_rate` with three decimals, summed steps/tokens/seconds, the distinct edit tiers (`;`-separated, `none` when no edit ran, `n/a` for tools without a tier concept), and `notes` naming failed tasks. Rows are written with Python's `csv` module, so quoting is exact and the decimals are invariant.
