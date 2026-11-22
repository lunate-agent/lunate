# Print-mode delta — todo updates in the JSON stream

## ADDED Requirements

### Requirement: Todo updates in the JSON stream

With `--json`, each successful `todo` call SHALL add one `todo_updated` line to the stream, carrying the complete list as an `items` array whose entries have `text` and a `status` of `pending`, `in_progress` or `done`. The line SHALL follow the existing stamped envelope (`type`, `runId`, and `sessionId` when the run has a session). A call refused by the tool SHALL emit no `todo_updated` line, and the default non-JSON print output SHALL remain exactly the final assistant answer.

#### Scenario: One line per update carries the full list

- **GIVEN** a print run whose model calls `todo` twice
- **WHEN** it runs with `--json`
- **THEN** the stream carries two `todo_updated` lines in call order, each with the complete list that call sent

#### Scenario: A refused update is not in the stream

- **GIVEN** a print run whose model sends two `in_progress` items
- **WHEN** it runs with `--json`
- **THEN** no `todo_updated` line appears for that call and the tool result is an error

#### Scenario: Default output stays the answer

- **GIVEN** a print run whose model calls `todo` and then answers
- **WHEN** it runs without `--json`
- **THEN** stdout carries only the final assistant answer
