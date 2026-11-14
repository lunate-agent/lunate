# benchmark-harness Specification

## Purpose

The reproducible SWE-bench adapter: run Lunate inside the official instance images, keep the patch and trajectory under known paths, forward credentials only by environment-variable name, and hand the predictions to the official evaluation without local scoring.

## Requirements

### Requirement: One documented entry point

The repository SHALL provide one harness adapter at `scripts/eval/swebench.py` whose behaviour is driven by documented CLI flags, including a one-instance dry run, the instance id, the model id, the Lunate binary, the output directory, the evaluation mode and the per-run timeout. `--help` SHALL list every flag and state the prerequisites. A run SHALL be reproducible from that command plus an official dataset instance; no environment-specific shell script is required.

#### Scenario: Help lists the dry-run and evaluation flags

- **GIVEN** a checkout with no run directory
- **WHEN** the adapter is invoked with `--help`
- **THEN** it exits 0 and the output names `--dry-run`, `--instance`, `--model`, `--lunate-bin`, `--output-dir`, `--dataset-file` and `--evaluate`

#### Scenario: A dry run is one instance

- **GIVEN** a local dataset file containing two instances
- **WHEN** the adapter runs with `--dry-run --instance <one-id> --model <id> --lunate-bin <linux-x64 binary>`
- **THEN** exactly that instance is executed and the run reports one result

#### Scenario: A missing required flag is a usage error

- **GIVEN** no `--instance` and no `--dry-run`
- **WHEN** the adapter starts
- **THEN** it exits 2 with a message that names the missing selection flag and writes no run directory

### Requirement: Runs inside the official SWE-bench image

For each selected instance the adapter SHALL run Lunate inside the official SWE-bench image for that instance, in the image's `/testbed` checkout, as `lunate -p --json --yolo "<problem statement>"` with the model passed through. The binary SHALL be a self-contained `linux-x64` executable mounted read-only into the container; a non-ELF or non-executable binary SHALL be refused before any container starts. The dataset instance SHALL supply the problem statement and the image name, and an image missing locally SHALL be pulled from the official repository before the run.

#### Scenario: The run executes in the image's testbed

- **GIVEN** an instance whose image is present locally and a valid linux-x64 Lunate binary
- **WHEN** the adapter executes the instance
- **THEN** the container command runs in `/testbed` and carries `-p --json --yolo` and the instance's problem statement

#### Scenario: A host binary cannot run in the Linux image

- **GIVEN** a macOS executable passed as `--lunate-bin`
- **WHEN** the adapter starts
- **THEN** it exits 1 with a message naming the required `linux-x64` self-contained publish command and starts no container

#### Scenario: A missing image is pulled

- **GIVEN** an instance whose official image is absent from the local Docker daemon
- **WHEN** the adapter prepares the instance
- **THEN** it pulls the image named by the instance before the container starts

### Requirement: Patch and trajectory under known paths

A completed instance run SHALL leave the JSON event stream as `trajectory.jsonl` and the working-tree change as `patch.diff` in `<output-dir>/<run-id>/<instance-id>/`, together with the Lunate stderr log, the process exit code and a `run.json` that records the instance, model, image, exit code, timings and artifact paths. The patch SHALL be produced from the image's `/testbed` working tree by `git diff`, and the trajectory SHALL be the unmodified `--json` stdout. The paths in `run.json` SHALL be relative to the run root so the directory can be moved.

#### Scenario: A successful run writes both artifacts

- **GIVEN** a Lunate run that edits the testbed and exits 0
- **WHEN** the adapter finishes the instance
- **THEN** `<run-dir>/<instance-id>/trajectory.jsonl` contains the event lines and `<run-dir>/<instance-id>/patch.diff` contains the diff, and `run.json` names both paths

#### Scenario: A failed Lunate run still keeps what it produced

- **GIVEN** a Lunate run that writes the trajectory and then exits 1
- **WHEN** the adapter finishes the instance
- **THEN** the artifacts are still written, `run.json` records exit code 1, and the adapter reports the instance as failed rather than as a completed evaluation

### Requirement: Official evaluation only

The adapter SHALL hand the patch to the official SWE-bench evaluation by writing a predictions JSONL line in the official schema and invoking `python -m swebench.harness.run_evaluation` with the dataset, split, run id, instance ids and predictions path. It SHALL NOT compute resolved counts, pass rates or any other score itself. The official report and summary SHALL be copied into the run directory when the evaluation produces them, and a missing official harness SHALL be reported with the installation command instead of approximated.

#### Scenario: The official harness receives the prediction

- **GIVEN** a completed instance with a non-empty patch and evaluation enabled
- **WHEN** the adapter evaluates
- **THEN** `predictions.jsonl` carries `instance_id`, `model_name_or_path` and `model_patch`, and the adapter invokes `python -m swebench.harness.run_evaluation` with `--dataset_name`, `--split`, `--run_id`, `--instance_ids` and `--predictions_path`

#### Scenario: The adapter reports only the official result

- **GIVEN** the official harness writes a report with a `resolved` verdict
- **WHEN** the adapter finishes
- **THEN** it copies the report and summary into the run directory and prints their verdict without recomputing a score

#### Scenario: A missing official harness is an honest failure

- **GIVEN** an environment where `swebench` cannot be imported
- **WHEN** evaluation is requested
- **THEN** the adapter exits nonzero with a message naming the official `swebench` package and the evaluation command, and does not substitute a local score

### Requirement: Environment-only credentials

The adapter SHALL accept provider credentials only through named environment variables and SHALL forward them to the container by name (`docker run -e NAME`), never by value on a command line. It SHALL write only variable names to `run.json` and logs. Before leaving an artifact on disk it SHALL scan the trajectory, logs, patch, predictions and report for every forwarded value and replace any occurrence with a redaction marker, recording that redaction happened.

#### Scenario: A forwarded key is not written by the adapter

- **GIVEN** `OPENROUTER_API_KEY` is set in the host environment
- **WHEN** the adapter starts a container
- **THEN** the Docker arguments contain `-e OPENROUTER_API_KEY` and not the key's value, and `run.json` lists only the variable name

#### Scenario: A leaked value is redacted before the run is left behind

- **GIVEN** a fake model stream that echoes the forwarded key into the trajectory
- **WHEN** the adapter finishes the instance
- **THEN** every copy of the value in the run directory is replaced with the redaction marker and `run.json` records that a redaction happened

### Requirement: Honest preflight and dry-run outcome

Before starting a container the adapter SHALL check Docker, the Lunate binary, the instance metadata, the model id and at least one forwarded credential, and SHALL exit nonzero with one message per missing prerequisite. A dry run SHALL exit 0 only when the instance produced both artifacts and Lunate exited 0; any other outcome SHALL be a nonzero exit with the instance's failure reason recorded in `run.json` and on stderr.

#### Scenario: Missing credentials stop before the container

- **GIVEN** no provider key in the environment
- **WHEN** the adapter starts a dry run
- **THEN** it exits nonzero, names the credential environment variable to set, and starts no container

#### Scenario: A non-zero Lunate exit is an honest failed dry run

- **GIVEN** Lunate exits 2 inside the image after writing its trajectory
- **WHEN** the dry run finishes
- **THEN** the adapter exits nonzero, records `exitCode: 2`, and prints the path to the trajectory and log

### Requirement: ProgramBench boundary is explicit

The adapter SHALL document that ProgramBench is not covered and why: its inference images provide a compiled binary and documentation rather than a git `/testbed`, and its submission is a whole-codebase archive evaluated by `programbench eval`, so the SWE-bench patch handoff does not fit. The documentation SHALL name the separate ProgramBench entry point rather than imply the benchmark was forgotten.

#### Scenario: The docs name the boundary

- **GIVEN** the harness documentation
- **WHEN** a reader looks for ProgramBench coverage
- **THEN** the documentation says it is out of scope, gives the submission-shape reason and names `programbench eval` as the separate official entry point

### Requirement: Published run definition

The repository SHALL record the published benchmark run as data, not as prose: a pinned subset of SWE-bench Verified instance ids in `eval/swebench/instances.txt`, the selection rule that produced it, and a model matrix in `eval/swebench/run.json` naming one budget model and one frontier model. The subset and both models SHALL be the same for the Lunate arm and the baseline arm. The run definition SHALL record the dataset name and split, the per-instance timeout and the single cost cap for the run. Re-running the recorded commands with the recorded dataset SHALL select exactly the recorded instances and models.

#### Scenario: The subset is pinned and reproducible

- **GIVEN** the committed `eval/swebench/instances.txt` and `eval/swebench/run.json`
- **WHEN** the orchestrator reads them against the official Verified test split
- **THEN** it selects exactly the listed instance ids and rejects an id the dataset does not contain

#### Scenario: Both models run the same subset

- **GIVEN** a run definition with one budget model and one frontier model
- **WHEN** the orchestrator runs the Lunate arm
- **THEN** each model is invoked on every instance in the pinned subset, and the summary labels the model tier (`budget` or `frontier`) beside the exact model id

### Requirement: One cost cap per run

A published run SHALL have exactly one cost cap in US dollars. The orchestrator SHALL pass the remaining budget to each Lunate invocation as `--max-cost` so a single instance cannot spend past the run's remaining budget, and SHALL pass the remaining budget to each mini-swe-agent invocation as its own cost limit. It SHALL stop starting new instances when the cap is reached. A cost limit stop SHALL be reported as a limit stop with the cap and the spend reached, not as an agent failure. The committed summary SHALL report the final spend for every arm and the total against the cap. When an instance's spend cannot be priced, in either arm, the run SHALL record that instance as unpriced, SHALL treat the cap as reached, and SHALL report the run total as unknown instead of adding a substitute amount. A missing or non-positive mini-swe-agent `instance_cost` SHALL be treated as unpriced, never as zero spend. A reported total SHALL never exceed the cap on paper. A run whose model has no known price SHALL NOT start with a cap unless the operator explicitly allows unpriced usage.

#### Scenario: The cap stops the run exactly at the limit

- **GIVEN** a run with a cost cap and a remaining budget smaller than the next instance could spend
- **WHEN** an invocation reaches the cap
- **THEN** the agent exits with the cost-limit code, the orchestrator records the spend and the limit stop, and it starts no further instance

#### Scenario: The final spend is reported

- **GIVEN** a completed run
- **WHEN** the orchestrator writes the summary
- **THEN** it sums the priced usage from each trajectory (or the limit event's spend) and reports the per-arm and total spend against the cap with invariant decimal formatting

#### Scenario: An unpriced instance does not inflate the total

- **GIVEN** a run where one instance's trajectory yields no price
- **WHEN** the orchestrator writes the summary
- **THEN** it marks that instance as unpriced, stops before starting another instance, and reports the run total as unknown rather than as the cap or a value above it

#### Scenario: A baseline instance without a positive cost is unpriced

- **GIVEN** a mini-swe-agent baseline trajectory whose `instance_cost` is missing, zero or negative
- **WHEN** the baseline arm records the instance
- **THEN** the instance's spend is unknown, the arm stops before the next instance, and the run total is reported as unknown rather than zero

### Requirement: mini-swe-agent baseline on the same subset

The published run SHALL run the mini-swe-agent baseline on the same pinned subset and the same two models, with its own cost limit, and SHALL submit its patches to the official SWE-bench evaluation. The baseline's resolved counts, spend and failures SHALL be recorded in the same committed summary as the Lunate arm. The committed run definition SHALL name the mini-swe-agent version that produced the numbers and any provider-specific routing alias the baseline needed.

#### Scenario: The baseline uses the same instances and models

- **GIVEN** the committed subset and model matrix
- **WHEN** the baseline arm runs
- **THEN** it is invoked with the same instance ids and the same two models, and a provider-specific routing alias is recorded in the run definition

#### Scenario: The baseline is scored officially

- **GIVEN** a baseline run that produced predictions
- **WHEN** it finishes
- **THEN** its resolved count comes from the official evaluation report, not a local count

### Requirement: Committed evidence and honest scope

The repository SHALL commit the run summary and a write-up at `docs/dev/eval/swebench-first-run.md` containing the resolved counts, the spend, the failure modes and an explicit statement of what the run does and does not show. The README's C# claim SHALL cite this write-up and the number it reports, or state no benchmark claim when the run does not support one. A workflow that would publish is not evidence: the summary SHALL come from a completed run whose artifacts are linked. For every instance and arm, the repository SHALL commit the official evaluation result that the resolved count comes from under `eval/swebench/runs/<run-id>/`, and every instance row of the committed summary SHALL record a repository-relative path to that result, so a reader of a clean checkout can follow the number to its official verdict without the run host. The orchestrator SHALL write its working tree to that tracked directory by default rather than to a git-ignored or machine-local path. Raw artifacts the run host did not retain SHALL be named as not retained in the run's evidence README and in the write-up; they SHALL NOT be reconstructed to look complete.

#### Scenario: The write-up states the scope

- **GIVEN** a completed run
- **WHEN** a reader opens the write-up
- **THEN** it names the subset size, the two models, the spend against the cap, every failed or unresolved instance, and the limits of a run at that size

#### Scenario: The README claim follows the evidence

- **GIVEN** the committed write-up
- **WHEN** a reader follows the README's C# benchmark sentence
- **THEN** the sentence names the measured result and links the write-up, and no claim precedes the evidence

#### Scenario: A committed summary links evidence a checkout can read

- **GIVEN** the committed run summary
- **WHEN** a reader opens the `workDir` of any instance row
- **THEN** the path is relative to the repository, resolves inside `eval/swebench/runs/<run-id>/`, and the official result there names the instance with the same resolved verdict the row reports

#### Scenario: A new run writes durable links

- **GIVEN** an otherwise unconfigured orchestrator invocation
- **WHEN** it records an instance row
- **THEN** the working tree it wrote to is the tracked `eval/swebench/runs/<run-id>/`, so the recorded path resolves in the checkout

#### Scenario: Missing raw artifacts are declared, not invented

- **GIVEN** a completed run whose host no longer holds the prediction patch text or the trajectories
- **WHEN** the repository commits that run's evidence
- **THEN** the run's evidence README and the write-up say which artifacts were not retained, and the committed directory contains no substitute produced for them

### Requirement: Adapter runs in an image without ICU

The adapter SHALL set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` in the instance container, because the official SWE-bench images do not ship libicu and a self-contained .NET binary otherwise terminates before its first event. Setting the invariant mode SHALL NOT change the adapter's credential handling or the product's culture-invariant output rules.

#### Scenario: A minimal image reaches the first event

- **GIVEN** an official instance image with no libicu
- **WHEN** the adapter starts Lunate in the container
- **THEN** the container environment carries `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` and Lunate writes its first trajectory event instead of terminating with an ICU failure

### Requirement: Crash artifacts never become a patch

A crashed container leaves core dumps in `/testbed`. The patch collector SHALL exclude core dumps from the patch and the prediction, so a crash cannot look like a model edit. The adapter SHALL still record the crash exit code and keep the log and trajectory.

#### Scenario: A core dump is not a prediction

- **GIVEN** a container run that terminates with a crash and leaves `qemu_*.core` or `core.*` in the testbed
- **WHEN** the adapter collects the patch
- **THEN** the core dump is absent from `patch.diff` and from `predictions.jsonl`, and the instance is reported as failed with its exit code
