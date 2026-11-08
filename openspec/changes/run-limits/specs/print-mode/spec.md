# print-mode delta — run limits

## MODIFIED Requirements

### Requirement: Print mode contract

`lunate -p "<prompt>"` SHALL run one prompt to completion without any interactive prompt. In default mode exactly the final answer — the text of the last assistant message with content — SHALL be written to stdout, written once at the end; all diagnostics SHALL go to stderr, so redirecting stdout captures a clean answer. With `--json` every event of the run SHALL be written to stdout as one JSON object per line in arrival order, reasoning events included (`reasoning_start`, `reasoning_message_content`, `reasoning_end`), with the same stamped fields as the event records (`type`, `runId`, `sessionId`, `parentRunId`, `source` when non-default) plus type-specific fields; `tool_call_result` SHALL carry the tool's detail record as a JSON element. Exit codes SHALL be: 0 for a run that finished with the `stop` reason, 1 for a run error or a failure before a clean finish, 2 for `length` and `step_limit` finishes, 3 for a `cost_limit` finish, 4 for a `time_limit` finish, and 130 when cancelled. Usage errors (missing prompt, unknown flags) SHALL exit 2 with a message on stderr and nothing on stdout.

`--max-cost <usd>` and `--max-time <duration>` SHALL set the run's cost and wall-clock limits for
that invocation, overriding the corresponding settings. `--max-time` SHALL accept integer seconds or
a suffix (`s`, `m`, `h`). `--allow-unpriced` SHALL permit a run with a cost limit and a model the
catalog cannot price. A cost limit on an unpriced model without that flag SHALL be a launch refusal
(exit 2) naming the model and the flag, before any model request.

#### Scenario: The final answer is the only thing on stdout

- **GIVEN** a scripted run that calls tools and streams several assistant messages
- **WHEN** print mode runs without `--json`
- **THEN** stdout is exactly the final assistant message text and diagnostics (if any) are on stderr

#### Scenario: JSON mode emits the whole stream

- **GIVEN** a scripted run with a tool call
- **WHEN** print mode runs with `--json`
- **THEN** stdout is one JSON object per event, in arrival order, each parseable on its own line, with `sessionId` present

#### Scenario: Step limit exits 2

- **GIVEN** a scripted run that reaches the step limit
- **WHEN** print mode finishes
- **THEN** the exit code is 2 and the run's `step_limit_reached` event appears in the JSON stream

#### Scenario: Cost limit exits 3

- **GIVEN** a scripted run that reaches `--max-cost`
- **WHEN** print mode finishes
- **THEN** the exit code is 3, stderr names the cost limit and the spend reached, and `cost_limit_reached` appears in the JSON stream

#### Scenario: Time limit exits 4

- **GIVEN** a scripted run that reaches `--max-time`
- **WHEN** print mode finishes
- **THEN** the exit code is 4, stderr names the time limit and the elapsed time reached, and `time_limit_reached` appears in the JSON stream

#### Scenario: An unpriced model refuses a cost limit

- **GIVEN** `--max-cost` and a model with no catalog price
- **WHEN** print mode starts without `--allow-unpriced`
- **THEN** it exits 2, names the model and `--allow-unpriced` on stderr, makes no model request, and writes nothing to stdout

#### Scenario: Missing prompt is a usage error

- **GIVEN** no prompt argument
- **WHEN** `-p` is passed alone
- **THEN** exit code 2, usage text on stderr, nothing on stdout

#### Scenario: Reasoning events appear in the JSON stream

- **GIVEN** a scripted run whose model streams reasoning
- **WHEN** print mode runs with `--json`
- **THEN** `reasoning_start`, `reasoning_message_content` and `reasoning_end` objects appear in arrival order like every other event

#### Scenario: The skills notice and the truncation warning stay on stderr

- **GIVEN** a trusted print run whose skill pack measures above the soft threshold and includes one skill whose description exceeds 1,024 characters
- **WHEN** print mode runs without `--json`
- **THEN** the skills-index notice and the description-truncation warning each appear on stderr as `lunate: skills: …` lines, stdout carries only the final assistant answer, and stdout contains no skills line
