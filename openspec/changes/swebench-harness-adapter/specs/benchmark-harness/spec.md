## ADDED Requirements

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
