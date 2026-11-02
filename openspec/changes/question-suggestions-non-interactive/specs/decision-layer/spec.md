## ADDED Requirements

### Requirement: Question suggestions attach only where a person answers

The question-suggestions consumer SHALL be proposed only in the interactive session, when the user
has enabled `decisions.questions: suggest` and the question is displayed to and answered by that
user. Print mode, unattended queue runs and ACP SHALL NOT propose a question decision for
`ask_user`: the tool SHALL degrade to its no-user result without suggested options, and enabling
`suggest` there — even with a `decision` profile available — SHALL NOT cause any question decision
exchange or event. An unattended run that needs a judgement SHALL receive it through the `decide`
tool, whose result stays information-only per its own requirement.

#### Scenario: A print run with suggestions enabled gets no suggestion exchange

- **GIVEN** print mode configured with `decisions.questions: suggest` and a `decision` profile
- **WHEN** the model calls `ask_user`
- **THEN** no question decision is proposed, and the tool result is the no-user degrade with none
  of the options marked as suggested

#### Scenario: An ACP session with suggestions enabled shows no question and no suggestion

- **GIVEN** an ACP session configured the same way
- **WHEN** the model calls `ask_user`
- **THEN** the editor is never shown a question or a suggestion, the tool returns the no-user
  result, and no question decision event reaches the event stream

#### Scenario: The decide tool stays the unattended route to a judgement

- **GIVEN** an unattended run with the `decide` tool offered
- **WHEN** the model needs a judgement
- **THEN** it gets the answer with its probability and provenance through `decide`, independently
  of `ask_user`, and no suggestion is attached to a question nobody answers
