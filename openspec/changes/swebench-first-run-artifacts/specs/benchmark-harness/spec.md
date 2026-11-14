## MODIFIED Requirements

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
