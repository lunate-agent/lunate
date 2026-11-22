# Agent tools delta — todo planning tool

## ADDED Requirements

### Requirement: Todo planning tool

The core tool set SHALL include a `todo` tool that lets the model record and replace one short plan for the current session. Its arguments SHALL be one object with an `items` array; each item SHALL carry a non-empty `text` and a `status` of `pending`, `in_progress` or `done`. A call SHALL replace the whole previous list, an empty array SHALL clear the list, and the list SHALL hold at most 20 items. At most one item SHALL be `in_progress`; a second one SHALL be refused with an actionable error telling the model to mark exactly one item in progress. An item with no text, an unknown status, more than 20 items, or an unknown argument SHALL be refused with an error naming what failed and what to send instead. A successful call SHALL return a short model-facing summary of the item counts and SHALL emit one `TodoUpdated` event carrying the complete new list. The tool SHALL be read-only, SHALL require no approval, and SHALL be registered beside the core file and bash tools in every frontend and child run.

#### Scenario: A call creates the list

- **GIVEN** a session with no todo list
- **WHEN** the model calls `todo` with three items, one of them `in_progress`
- **THEN** the call succeeds, its result names the item counts, and one `TodoUpdated` event carries all three items

#### Scenario: A second call replaces the whole list

- **GIVEN** a todo list from an earlier call
- **WHEN** the model calls `todo` with a different list
- **THEN** the new list replaces the old one in the event, and no item from the old list remains

#### Scenario: More than one item in progress is refused

- **GIVEN** two items marked `in_progress`
- **WHEN** the model calls `todo`
- **THEN** the call is an error naming the one-in-progress rule, no `TodoUpdated` event is emitted, and the previous list stays current

#### Scenario: An empty list clears the plan

- **GIVEN** a todo list with items
- **WHEN** the model calls `todo` with an empty `items` array
- **THEN** the call succeeds, emits an empty `TodoUpdated` list, and the live area stops showing a plan

#### Scenario: Invalid items are refused with guidance

- **GIVEN** an item with blank text, an unknown status, or a list longer than 20 items
- **WHEN** the model calls `todo`
- **THEN** the call is an error that names the offending item and the accepted shape, and no list is emitted

#### Scenario: The tool needs no approval

- **GIVEN** the default `ask` approval policy
- **WHEN** the model calls `todo`
- **THEN** the call runs without an approval prompt and changes no workspace file
