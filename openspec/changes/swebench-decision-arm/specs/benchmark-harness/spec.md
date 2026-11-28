# Benchmark Harness Specification Delta

## MODIFIED Requirements

### Requirement: Published run definition

The repository SHALL record the published benchmark run as data, not as prose: a pinned subset of SWE-bench Verified instance ids in `eval/swebench/instances.txt`, the selection rule that produced it, and a model matrix in `eval/swebench/run.json` naming one budget model, one frontier model and, optionally, one Lunate decision arm. The subset and the single-model models SHALL be the same for the Lunate arm and the baseline arm. A decision arm SHALL be named by its `tier` (`decision`), its `main` model, its `decision` model, its `decisionLayer` value and a note, and each model entry SHALL name its credential environment variable, so the arm's whole configuration is committed data rather than a flag on a command line. The run definition SHALL record the dataset name and split, the per-instance timeout and the single cost cap for the run. Re-running the recorded commands with the recorded dataset SHALL select exactly the recorded instances and models, and the summary SHALL report the decision arm as a Lunate arm beside the single-model arms.

#### Scenario: The subset is pinned and reproducible

- **GIVEN** the committed `eval/swebench/instances.txt` and `eval/swebench/run.json`
- **WHEN** the orchestrator reads them against the official Verified test split
- **THEN** it selects exactly the listed instance ids and rejects an id the dataset does not contain

#### Scenario: Both models run the same subset

- **GIVEN** a run definition with one budget model and one frontier model
- **WHEN** the orchestrator runs the Lunate arm
- **THEN** each model is invoked on every instance in the pinned subset, and the summary labels the model tier (`budget` or `frontier`) beside the exact model id

#### Scenario: The decision arm carries its configuration as data

- **GIVEN** a run definition whose third arm names the `decision` tier, a `main` model, a `decision` model, `decisionLayer: "on"` and a note
- **WHEN** the orchestrator runs that arm
- **THEN** it invokes the adapter with the `main` model id, writes the arm's prepared home whose settings bind the decision profile to the decision model, forwards each model entry's credential variable by name, and records the arm in the summary under its two model ids

#### Scenario: A decision arm without both models is refused

- **GIVEN** a run definition whose `decision` arm lacks a `main` or a `decision` model, or names two decision arms
- **WHEN** the orchestrator loads the definition
- **THEN** it refuses with an error naming the decision arm, and starts no instance


### Requirement: One cost cap per run

A published run SHALL have exactly one cost cap in US dollars. The orchestrator SHALL pass the remaining budget to each Lunate invocation as `--max-cost` so a single instance cannot spend past the run's remaining budget, and SHALL pass the remaining budget to each mini-swe-agent invocation as its own cost limit. It SHALL stop starting new instances when the cap is reached. A cost limit stop SHALL be reported as a limit stop with the cap and the spend reached, not as an agent failure. The committed summary SHALL report the final spend for every arm and the total against the cap. When an instance's spend cannot be priced, in either arm, the run SHALL record that instance as unpriced, SHALL treat the cap as reached, and SHALL report the run total as unknown instead of adding a substitute amount. An arm that runs more than one model SHALL price each instance from the per-record prices its own trajectory carries, so the main and decision models are charged at their own catalog rates, and a usage record with no price SHALL make that instance unpriced. A missing or non-positive mini-swe-agent `instance_cost` SHALL be treated as unpriced, never as zero spend. A reported total SHALL never exceed the cap on paper. A run whose model has no known price SHALL NOT start with a cap unless the operator explicitly allows unpriced usage.

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

#### Scenario: A multi-model arm prices every record

- **GIVEN** a decision arm whose trajectory carries a main-model usage record and a decision-model usage record, each with its own price
- **WHEN** the orchestrator writes the instance row
- **THEN** the spend is the sum of those two prices, and a record with no price makes the instance unpriced rather than pricing both records at the main model's rate

#### Scenario: A baseline instance without a positive cost is unpriced

- **GIVEN** a mini-swe-agent baseline trajectory whose `instance_cost` is missing, zero or negative
- **WHEN** the baseline arm records the instance
- **THEN** the instance's spend is unknown, the arm stops before the next instance, and the run total is reported as unknown rather than zero


### Requirement: mini-swe-agent baseline on the same subset

The published run SHALL run the mini-swe-agent baseline on the same pinned subset and the same two single-model models, with its own cost limit, and SHALL submit its patches to the official SWE-bench evaluation. A decision arm SHALL NOT be run as a baseline: mini-swe-agent has no decision layer, so inventing one would compare two different things. The baseline's resolved counts, spend, steps and wall time SHALL be recorded in the same committed summary as the Lunate arms. The committed run definition SHALL name the mini-swe-agent version that produced the numbers and any provider-specific routing alias the baseline needed.

#### Scenario: The baseline uses the same instances and models

- **GIVEN** the committed subset and model matrix
- **WHEN** the baseline arm runs
- **THEN** it is invoked with the same instance ids and the same two single-model models, and a provider-specific routing alias is recorded in the run definition

#### Scenario: The decision arm has no baseline counterpart

- **GIVEN** a model matrix whose third arm is a Lunate decision arm
- **WHEN** the baseline arm runs that subset
- **THEN** it starts the budget and frontier models only and records no baseline row for the decision tier

#### Scenario: The baseline is scored officially

- **GIVEN** a baseline run that produced predictions
- **WHEN** it finishes
- **THEN** its resolved count comes from the official evaluation report, not a local count

## ADDED Requirements

### Requirement: Per-instance measurements and cost per resolved instance

The committed summary SHALL record, for every instance and every arm, the resolved verdict, the spend in US dollars, the step count and the wall time in seconds, and SHALL record for every arm the cost per resolved instance. An agent step SHALL be a model call of the arm's chat model: a decision-model call SHALL be counted separately as a decision step and SHALL NOT inflate the step count the single-model arms report, so the arms compare like for like. The wall time SHALL come from the instance's own run record, not from the summary's total runtime. Cost per resolved instance SHALL be the arm's spend divided by its resolved count, SHALL be reported as unknown when the arm resolved nothing or its spend is unpriced, and SHALL be formatted with invariant decimal digits. The rendered summary SHALL show the same four numbers per arm.

#### Scenario: A decision call is not an agent step

- **GIVEN** a decision-arm trajectory with four usage records, one of which is attributed to the `decision` usage
- **WHEN** the orchestrator reads that instance
- **THEN** the instance row records three agent steps and one decision step

#### Scenario: Wall time is the instance's own runtime

- **GIVEN** an instance whose run record starts at one timestamp and finishes at another
- **WHEN** the orchestrator writes the instance row
- **THEN** the row's seconds are that difference, and the arm's seconds are the sum of its instance rows

#### Scenario: Cost per resolved instance is reported for every arm

- **GIVEN** an arm with spend and at least one resolved instance
- **WHEN** the summary is written
- **THEN** the arm reports its spend divided by its resolved count, and the rendered summary shows that number beside the arm's resolved count, spend and steps

#### Scenario: An arm with no resolved instance has no cost per resolve

- **GIVEN** an arm that resolved no instance
- **WHEN** the summary is written
- **THEN** its cost per resolved instance is reported as unknown rather than as its total spend or zero
