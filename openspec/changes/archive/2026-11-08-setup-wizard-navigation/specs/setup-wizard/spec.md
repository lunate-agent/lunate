## ADDED Requirements

### Requirement: The walk is navigable

The wizard SHALL state its path on every step as the four stages that group the ten steps —
`Provider` (provider, credential, connection test), `Model` (model, model check), `Profile`
(chat, fast, decision, decisions settings) and `Review` (summary, write) — with the current stage
and its position (`1/4` … `4/4`). The stages are information, not a second navigation layer: the
user still walks one step at a time.

The wizard SHALL accept three verbs at its pickers and menus, and two at a multi-select question:

- **Enter** confirms or advances: the highlighted answer becomes the step's answer and the walk
  moves on.
- **Back** (`b` or Left at a picker, menu or multi-select) returns to the previous walked step with
  every answer kept. Esc inside a menu returns to the step that opened it, and Esc at a question
  ends the run with nothing written.
- **Forward** (`f` or Right) moves through steps that already have an answer without re-asking
  them, stopping at the first step that still needs an answer or at the summary. A step the walk
  has not reached, or whose answer a change cleared, SHALL NOT be skipped into.
- **Space** toggles the active row of a multi-select question, Enter confirms the selection, and a
  count of the selected rows is visible while toggling.

A menu — an action list whose rows change settings rather than answer the step — SHALL return to
itself after an action with the changed value visible on the row, and SHALL leave only through its
**Done** row. The decisions-settings menu SHALL be this shape.

A model list SHALL filter as text is typed — that search is its documented affordance. Any other
wizard list SHALL filter as text is typed only when its unfiltered rows exceed 15, so that bare `b`
and `f` remain the back and forward verbs on a short list; `Left` and `Right` are the verbs at every
picker.

#### Scenario: The path states the stage and the position

- **GIVEN** a fresh-machine walk at the model step
- **WHEN** the step renders
- **THEN** its header names the four-stage path with `Model` as the current stage and `2/4` as the
  position

#### Scenario: Back from the summary, change two answers, come forward

- **GIVEN** a walk that reached the summary with the chat profile and the decisions settings answered
- **WHEN** the user presses Back to the profile step, changes the profile name, presses Back to the
  decisions menu and changes one switch, then presses Forward
- **THEN** the changed answers are staged, the walk returns to the summary, and the steps between the
  two changes are not asked again

#### Scenario: Forward refuses an unanswered step

- **GIVEN** a walk that went back past the model step with no model answer staged
- **WHEN** the user presses Forward at the provider step
- **THEN** the walk stops at the model step and asks it, instead of skipping to the summary

#### Scenario: A menu returns to itself and shows the change

- **GIVEN** the decisions-settings menu
- **WHEN** the user changes the approval gate and the action returns
- **THEN** the same menu renders with the approval row showing the chosen value, and only **Done**
  leaves it

#### Scenario: Esc in a menu returns to its parent

- **GIVEN** the decisions-settings menu opened from the walk
- **WHEN** the user presses Esc
- **THEN** the walk returns to the previous step and nothing is written

#### Scenario: A three-item multi-select shows its count

- **GIVEN** a decision-routing question with three candidate profiles
- **WHEN** the user toggles two of them with Space
- **THEN** two rows show `[x]`, the other stays `[ ]`, and the question shows `2 selected` before
  Enter confirms exactly those two

#### Scenario: Keys, not wording, drive the tests

- **GIVEN** the tests for back, forward, the returning menu and the multi-select
- **WHEN** they drive the walk
- **THEN** they send key sequences and assert the staged model and the written files, not the text
  on screen
