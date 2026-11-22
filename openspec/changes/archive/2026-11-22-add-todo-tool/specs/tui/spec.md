# TUI delta — todo list in the live area

## ADDED Requirements

### Requirement: Todo list in the live area

The live area SHALL render the current todo list as one line per item above the input box: a status glyph followed by the item text, with a pending item, an in-progress item and a done item each distinguishable without colour. The block SHALL show at most five lines, including an overflow line that names how many items were not shown (`… N more`, ASCII `... N more` without Unicode), and every line SHALL be clipped to the terminal width. The todo lines SHALL share the live area's height budget with the tail, activity, thinking, notice and prompt, so the input box always keeps its own lines and is never pushed off screen. A `TodoUpdated` event for a child run SHALL NOT replace the parent session's list. An empty or absent list SHALL render no line, so a session with no `todo` call renders exactly as it does today.

#### Scenario: Items render in order above the input

- **GIVEN** a `TodoUpdated` event with a pending, an in-progress and a done item
- **WHEN** the live area renders
- **THEN** three todo lines appear above the input rule in call order, and each status is distinguishable without colour

#### Scenario: The block is bounded

- **GIVEN** a todo list with more than five items
- **WHEN** the live area renders
- **THEN** it shows at most five todo lines, the last one names the unshown count, and the input line is still on screen

#### Scenario: No todo call changes nothing

- **GIVEN** a session that never called `todo`
- **WHEN** the live area renders
- **THEN** the frame is identical to the frame before the tool existed

#### Scenario: A child's plan does not replace the parent's

- **GIVEN** a parent session showing a todo list
- **WHEN** a child run emits `TodoUpdated`
- **THEN** the parent's list stays on screen
