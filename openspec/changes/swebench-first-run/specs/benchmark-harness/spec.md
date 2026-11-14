## ADDED Requirements

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

The repository SHALL commit the run summary and a write-up at `docs/dev/eval/swebench-first-run.md` containing the resolved counts, the spend, the failure modes and an explicit statement of what the run does and does not show. The README's C# claim SHALL cite this write-up and the number it reports, or state no benchmark claim when the run does not support one. A workflow that would publish is not evidence: the summary SHALL come from a completed run whose artifacts are linked.

#### Scenario: The write-up states the scope

- **GIVEN** a completed run
- **WHEN** a reader opens the write-up
- **THEN** it names the subset size, the two models, the spend against the cap, every failed or unresolved instance, and the limits of a run at that size

#### Scenario: The README claim follows the evidence

- **GIVEN** the committed write-up
- **WHEN** a reader follows the README's C# benchmark sentence
- **THEN** the sentence names the measured result and links the write-up, and no claim precedes the evidence

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
