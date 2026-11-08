## ADDED Requirements

### Requirement: Fatal startup message rendering

A fatal startup message SHALL be rendered so its whole reason is readable at any terminal width. The message SHALL wrap at the terminal width instead of being clipped or elided: every physical line SHALL be sanitized and no line SHALL be wider than the terminal, and the concatenation of the wrapped lines SHALL carry every word of the reason. A fatal message SHALL never be capped at a line count and SHALL never end with an ellipsis.

The same wrapping rule SHALL serve the pre-session renderer and the live area, so a diagnostic written before the live area starts and one raised after it starts agree on the width and on the line content.

A fatal message raised after the live area has started SHALL be committed to scrollback through the live area's writer before teardown, so the live area's final clear cannot erase any wrapped row. The message SHALL remain readable on the terminal after the process exits.

#### Scenario: The workspace guard message is fully readable at 40 columns

- **GIVEN** a 40-column terminal and `LUNATE_HOME` pointing inside the workspace
- **WHEN** the session composition refuses the store and the live area tears down
- **THEN** the whole message `LUNATE_HOME points inside the workspace; trust records there could be written by the repository` is visible across its wrapped physical rows, every row is at most 40 columns, and no tail of the message was erased

#### Scenario: A pre-session fatal message wraps at the width

- **GIVEN** a 40-column terminal and a fatal diagnostic raised before the live area starts, such as the raw-mode failure or the terminal-support refusal
- **WHEN** the diagnostic is rendered
- **THEN** it wraps at 40 columns, no word is lost, and every physical line is at most 40 columns

#### Scenario: A fatal message is longer than the notice cap

- **GIVEN** a fatal message that wraps to more than four physical lines
- **WHEN** it is rendered after the live area has started
- **THEN** every line is present without an ellipsis, because the notice slot's four-line cap does not apply to a fatal message
