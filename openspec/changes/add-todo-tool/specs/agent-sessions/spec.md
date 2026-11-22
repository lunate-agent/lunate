# Agent sessions delta — todo list replay

## ADDED Requirements

### Requirement: Todo list replay from tool calls

The todo list SHALL live in the session's existing `todo` tool-call and tool-result messages, with no new session entry and no schema change. Loading a session SHALL reconstruct the current list from the last valid `todo` call on the active path, so a resumed session shows the same list before any new call. A later invalid call SHALL NOT replace the list a valid earlier call established, and a session with no valid `todo` call SHALL reconstruct no list. Replaying a session twice SHALL produce the same list, and the session file's bytes and golden fixtures SHALL be unchanged by the tool.

#### Scenario: Resume replays the last valid list

- **GIVEN** a session whose model called `todo` twice with valid lists
- **WHEN** it is loaded and resumed
- **THEN** the active path yields the second list, with every item and status identical to the recorded call

#### Scenario: An invalid later call does not replace the list

- **GIVEN** a session with a valid `todo` call followed by a refused one
- **WHEN** it is resumed
- **THEN** the valid list is the one reconstructed

#### Scenario: No todo call reconstructs nothing

- **GIVEN** a session that never called `todo`
- **WHEN** it is resumed
- **THEN** no todo list is reconstructed and the live area shows no plan

#### Scenario: The session format does not change

- **GIVEN** a session containing `todo` calls
- **WHEN** it is written and loaded again
- **THEN** its header schema is unchanged, no new entry type appears, and the committed golden session fixtures still pass
