## MODIFIED Requirements

### Requirement: Published run definition

The repository SHALL record the published benchmark run as data, not as prose: a pinned subset of SWE-bench Verified instance ids in `eval/swebench/instances.txt`, the selection rule that produced it, and an arm list in `eval/swebench/run.json` whose every entry names the tool that runs the arm (`lunate` or `mini-swe-agent`), the arm's tier, its exact model id and its credential environment variable. Arm names SHALL be unique. A Lunate arm SHALL name `semanticSearch` as `on` or `off`; an `on` arm SHALL name the embeddings model and the credential variable its prepared home binds to `usages.embeddings`, and an `off` arm SHALL NOT name one. A Lunate arm MAY instead be a decision arm, named by its `main` model, its `decision` model, its `decisionLayer` value and a note, and each of those models SHALL name its credential variable. The run definition SHALL record the dataset name and split, the per-instance timeout and the single cost cap for the run. The pinned definition SHALL name exactly three arms — the Lunate budget arm with `semantic_search` off, the Lunate budget arm with `semantic_search` on, and the mini-swe-agent budget arm — and its subset SHALL be the 50 instances at indices 0, 10, …, 490 of the lexicographic order of the 500-instance Verified test split, with the selection rule recorded beside the ids. Every arm SHALL run every instance of the pinned subset, so the arms differ in their configuration, never in their sample. Re-running the recorded commands with the recorded dataset SHALL select exactly the recorded instances and arms, and the summary of a run SHALL report each of that run's arms.

#### Scenario: The subset is pinned and reproducible

- **GIVEN** the committed `eval/swebench/instances.txt` and `eval/swebench/run.json`
- **WHEN** the orchestrator reads them against the official Verified test split
- **THEN** it selects exactly the listed 50 instance ids, which are the ids at indices 0, 10, …, 490
  of the recorded lexicographic order, and it rejects an id the dataset does not contain

#### Scenario: Both models run the same subset

- **GIVEN** a run definition with three arms
- **WHEN** the orchestrator runs the definition
- **THEN** every arm is invoked on every instance in the pinned subset, and the summary labels each
  arm's tier and exact model id beside the arm's name

#### Scenario: The comparison arms differ only in the tool

- **GIVEN** the two Lunate budget arms of the pinned definition
- **WHEN** the orchestrator prepares their configuration
- **THEN** both name the same model id and credential, one names `semanticSearch: "on"` with its
  embeddings model and credential, the other names `"off"` and no embeddings model, and the third
  arm names the `mini-swe-agent` tool with that same model id

#### Scenario: An arm with an unknown tool is refused

- **GIVEN** a run definition whose arm names a tool the harness does not run
- **WHEN** the orchestrator loads the definition
- **THEN** it refuses with an error naming the arm and the accepted tools, and starts no instance

#### Scenario: An on arm without an embeddings model is refused

- **GIVEN** a Lunate arm that names `semanticSearch: "on"` and no embeddings model
- **WHEN** the orchestrator loads the definition
- **THEN** it refuses with an error naming the arm and the missing embeddings model, and starts no
  instance

#### Scenario: The decision arm carries its configuration as data

- **GIVEN** a run definition whose arm names the `decision` tier, a `main` model, a `decision` model,
  `decisionLayer: "on"` and a note
- **WHEN** the orchestrator runs that arm
- **THEN** it invokes the adapter with the `main` model id, writes the arm's prepared home whose
  settings bind the decision profile to the decision model, forwards each model entry's credential
  variable by name, and records the arm in the summary under its two model ids

#### Scenario: A decision arm without both models is refused

- **GIVEN** a run definition whose `decision` arm lacks a `main` or a `decision` model, or names two
  decision arms
- **WHEN** the orchestrator loads the definition
- **THEN** it refuses with an error naming the decision arm, and starts no instance

### Requirement: mini-swe-agent baseline on the same subset

The published run SHALL run every arm whose tool is `mini-swe-agent` on the same pinned subset and the same model its Lunate counterpart runs, with its own cost limit, and SHALL submit its patches to the official SWE-bench evaluation. A baseline arm SHALL name a model id that a Lunate arm also runs, and the definition loader SHALL refuse a baseline arm whose model no Lunate arm runs. An arm named as a decision arm SHALL NOT be run as a baseline: mini-swe-agent has no decision layer, so inventing one would compare two different things. The baseline's resolved counts, spend, steps and wall time SHALL be recorded in the same committed summary as the Lunate arms. The committed run definition SHALL name the mini-swe-agent version that produced the numbers and any provider-specific routing alias the baseline needed.

#### Scenario: The baseline uses the same instances and models

- **GIVEN** the committed subset and arm list
- **WHEN** the baseline arm runs
- **THEN** it is invoked with the same instance ids and the model id a Lunate arm runs, and a
  provider-specific routing alias is recorded in the run definition

#### Scenario: A baseline with no Lunate counterpart is refused

- **GIVEN** a run definition whose baseline arm names a model no Lunate arm runs
- **WHEN** the orchestrator loads the definition
- **THEN** it refuses with an error naming the baseline arm and the model, and starts no instance

#### Scenario: The decision arm has no baseline counterpart

- **GIVEN** a run definition whose arm matrix includes a decision arm
- **WHEN** the baseline arm runs the subset
- **THEN** no baseline row is recorded for the decision arm

#### Scenario: The baseline is scored officially

- **GIVEN** a baseline run that produced predictions
- **WHEN** it finishes
- **THEN** its resolved count comes from the official evaluation report, not a local count

### Requirement: Per-instance measurements and cost per resolved instance

The committed summary SHALL record, for every instance and every arm, the resolved verdict, the spend in US dollars, the step count, the wall time in seconds and the number of `semantic_search` calls the instance made, and SHALL record for every arm its id, the sum of its instances' `semantic_search` calls and the cost per resolved instance. An agent step SHALL be a model call of the arm's chat model: a decision-model call SHALL be counted separately as a decision step and SHALL NOT inflate the step count the single-model arms report, so the arms compare like for like. A `semantic_search` call SHALL be a `tool_call_start` event of the trajectory that names the tool, so a tool call inside a step is measured without becoming a step, and an arm that never enables the tool records zero. The wall time SHALL come from the instance's own run record, not from the summary's total runtime. Cost per resolved instance SHALL be the arm's spend divided by its resolved count, SHALL be reported as unknown when the arm resolved nothing or its spend is unpriced, and SHALL be formatted with invariant decimal digits. The rendered summary SHALL show the arm id, the same four numbers per arm and the search call count.

#### Scenario: A decision call is not an agent step

- **GIVEN** a decision-arm trajectory with four usage records, one of which names the arm's decision model
- **WHEN** the orchestrator reads that instance
- **THEN** the instance row records three agent steps and one decision step

#### Scenario: Wall time is the instance's own runtime

- **GIVEN** an instance whose run record starts at one timestamp and finishes at another
- **WHEN** the orchestrator writes the instance row
- **THEN** the row's seconds are that difference, and the arm's seconds are the sum of its instance rows

#### Scenario: The search call count is per instance and per arm

- **GIVEN** a trajectory with two `tool_call_start` events, one naming `semantic_search` and one
  naming `bash`
- **WHEN** the orchestrator reads that instance
- **THEN** the instance row records one `semantic_search` call, and the arm's total is the sum of
  its rows' counts

#### Scenario: An arm without the tool records no search calls

- **GIVEN** the Lunate budget arm with `semantic_search` off
- **WHEN** the orchestrator writes that arm's rows
- **THEN** every row and the arm report zero `semantic_search` calls rather than an unknown value

#### Scenario: Cost per resolved instance is reported for every arm

- **GIVEN** an arm with spend and at least one resolved instance
- **WHEN** the summary is written
- **THEN** the arm reports its spend divided by its resolved count, and the rendered summary shows that number beside the arm's resolved count, spend and steps

#### Scenario: An arm with no resolved instance has no cost per resolve

- **GIVEN** an arm that resolved no instance
- **WHEN** the summary is written
- **THEN** its cost per resolved instance is reported as unknown rather than as its total spend or zero
