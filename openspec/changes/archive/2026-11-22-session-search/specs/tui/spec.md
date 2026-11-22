## MODIFIED Requirements

### Requirement: SelectList pickers

A picker SHALL render as a select list in the live area — a title and one line per item (a bounded window keeps the selection visible), the selected item marked and emphasized — navigated with Up/Down (clamped at the ends), confirmed with Enter, dismissed with Esc; every other key SHALL be ignored while a picker is open. Pickers SHALL open only while idle. The model picker (Ctrl+L or `/model`) SHALL list the catalog's models, mark the current one, and on selection record a model change in the session and continue the conversation on the new model; the footer SHALL show the new model. The session picker (`/resume`) SHALL list the session directory's sessions, newest first, and on selection resume the chosen session — its history becomes the conversation and a notice names the resumed session. The session picker SHALL also carry a search field: a typed query SHALL be run over past turns through the store's session search, one row per matching turn labelled with its timestamp, session id and a snippet, and confirming a row SHALL resume that session with the matching entry as its active leaf so the conversation continues from that turn; clearing the query SHALL return to the newest-first listing and a search that matches nothing SHALL leave a dismissable empty picker. The search field SHALL keep the picker's title and keys otherwise unchanged, and the picker render SHALL be pinned by a golden.

#### Scenario: Choosing a model switches without losing the conversation

- **GIVEN** an idle session with a completed turn
- **WHEN** the model picker is opened and another catalog model is chosen
- **THEN** the session gains a model-change entry, the footer names the new model, and the next request continues the same history

#### Scenario: Esc dismisses without switching

- **GIVEN** an open picker
- **WHEN** Esc is pressed
- **THEN** the picker closes and nothing changed

#### Scenario: Resume restores the conversation

- **GIVEN** two sessions in the session directory
- **WHEN** the picker resumes the older one
- **THEN** the next request carries that session's history and a notice names it

#### Scenario: The search field finds a past turn

- **GIVEN** a session whose earlier turn says "settle the retry budget"
- **WHEN** the session picker is open and its search field is typed "retry budget"
- **THEN** the picker's rows are the matching turns, labelled with the turn's timestamp, the session id and the snippet

#### Scenario: A confirmed hit continues from that turn

- **GIVEN** the session picker showing a matching turn
- **WHEN** that row is confirmed
- **THEN** the session is resumed, the matching entry becomes its active leaf, and a notice names the session

#### Scenario: Clearing the query restores the listing

- **GIVEN** the session picker filtered by a query
- **WHEN** Esc clears the query
- **THEN** the rows are the session directory's newest-first listing again and the picker stays open

#### Scenario: Picker keys do not reach the input line

- **GIVEN** an open picker
- **WHEN** Up, Down, characters and Enter are pressed
- **THEN** only the selection changes; no text is edited and no run starts
