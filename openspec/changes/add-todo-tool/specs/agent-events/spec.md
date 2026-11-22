# Agent events delta — todo update event

## ADDED Requirements

### Requirement: Todo update event

The event set SHALL include a `TodoUpdated` extension event carrying the run id and the complete todo list. Each item SHALL carry its text and one status from the closed set `pending`, `in_progress` and `done`. The event SHALL be a member of both `AgentEvent` and `ExtensionEvent`, SHALL carry the same stamped session, parent-run and source fields as every other event, and SHALL be emitted once for each successful `todo` call before that call's `ToolCallResult`. Consumers that do not know the event SHALL ignore it through their declared fallback arm, as the closed-set rule already requires.

#### Scenario: The event carries the complete list

- **GIVEN** a successful `todo` call with three items
- **WHEN** the event stream is read
- **THEN** exactly one `TodoUpdated` event appears with all three items in call order and their statuses

#### Scenario: The event is part of the extension set

- **GIVEN** a `TodoUpdated` record
- **WHEN** it is folded into `ExtensionEvent` and then `AgentEvent`
- **THEN** both carry the record, its run id and its stamped session id

#### Scenario: Consumers without a todo view keep working

- **GIVEN** a consumer whose event switch handles a subset of the declared kinds
- **WHEN** a `TodoUpdated` event arrives
- **THEN** its declared fallback arm ignores the event and the stream continues
