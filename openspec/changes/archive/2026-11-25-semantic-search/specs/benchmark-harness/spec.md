# Benchmark Harness Specification Delta

## MODIFIED Requirements

### Requirement: One documented entry point

The repository SHALL provide one harness adapter at `scripts/eval/swebench.py` whose behaviour is driven by documented CLI flags, including a one-instance dry run, the instance id, the model id, the Lunate binary, the output directory, the evaluation mode, the per-run timeout, and an optional Lunate home directory so an arm can run with a prepared settings and provider pair. `--help` SHALL list every flag and state the prerequisites. A run SHALL be reproducible from that command plus an official dataset instance; no environment-specific shell script is required.

#### Scenario: Help lists the dry-run and evaluation flags

- **GIVEN** a checkout with no run directory
- **WHEN** the adapter is invoked with `--help`
- **THEN** it exits 0 and the output names `--dry-run`, `--instance`, `--model`, `--lunate-bin`, `--output-dir`, `--dataset-file`, `--evaluate` and `--lunate-home`

#### Scenario: A dry run is one instance

- **GIVEN** a local dataset file containing two instances
- **WHEN** the adapter runs with `--dry-run --instance <one-id> --model <id> --lunate-bin <linux-x64 binary>`
- **THEN** exactly that instance is executed and the run reports one result

#### Scenario: A missing required flag is a usage error

- **GIVEN** no `--instance` and no `--dry-run`
- **WHEN** the adapter starts
- **THEN** it exits 2 with a message that names the missing selection flag and writes no run directory

#### Scenario: A home directory supplies only settings and providers

- **GIVEN** a directory containing `settings.json` and `providers.json`
- **WHEN** the adapter runs with `--lunate-home <dir>`
- **THEN** exactly those two files are copied into the run directory's `lunate-home` and the in-image run reads them, and no other file from the directory is copied

#### Scenario: A home without settings is refused

- **GIVEN** a directory with no `settings.json`
- **WHEN** the adapter runs with `--lunate-home <dir>`
- **THEN** it refuses with a usage error naming the missing file and starts no container

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

#### Scenario: A prepared home makes the tool-on arm reproducible

- **GIVEN** a prepared home whose `settings.json` enables `semantic_search` in `tools.extra` and binds `usages.embeddings`
- **WHEN** the adapter runs the same instance subset with and without that `--lunate-home`
- **THEN** both arms run the identical container command apart from the prepared home, and each
  arm's trajectory records its own steps and tokens
