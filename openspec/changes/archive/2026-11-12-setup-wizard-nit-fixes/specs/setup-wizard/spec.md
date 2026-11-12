## ADDED Requirements

### Requirement: The rendered credential answer never carries a typed key

Every text rendering of the wizard's staged answers — the summary, a notice, a diagnostic, and the
answers' own `ToString()` — SHALL name a credential answer by its reference (`env:<VARIABLE>`,
`auth:<name>`) or as "the key you typed", and SHALL NOT contain a typed key value. A staged answer
that holds a key SHALL render the key's presence as redacted text, so nothing that formats an answer
can echo the secret.

#### Scenario: A typed key never reaches an answer's text

- **GIVEN** a credential answer holding a typed key
- **WHEN** the answer is rendered as text (its `ToString()`, the summary, a failure notice)
- **THEN** the reference is named, the key is rendered as redacted, and the key text appears nowhere

### Requirement: The decision profile never replaces a chat profile that shares its name

The wizard SHALL NOT stage two profiles under one name. The decision profile's name is the fixed
`decisions`; when the walk's chat profile already carries that name, the decision step SHALL skip
itself with one notice naming the clash and SHALL stage no decision profile, so the commit leaves the
chat profile and `usages.chat` as the walk collected them and writes nothing for the decisions layer.
A walk whose chat profile has any other name behaves as before.

#### Scenario: A chat profile named `decisions` survives the walk

- **GIVEN** a full walk whose chat profile step was answered with the name `decisions`
- **WHEN** the walk reaches the decision profile step
- **THEN** the step names the clash, skips the decision profile, the summary marks it skipped, and the
  committed configuration keeps `profiles.decisions` as the chat profile with `usages.chat` bound to
  it and no `decisions.profile` written

### Requirement: A failed model check names the model, the family and the status

A failed chat model check SHALL record a detail that names the model id, the resolved API family and
the HTTP status or response shape the request came back with, with a typed or referenced credential
redacted. The interactive notice and the summary SHALL print that recorded detail unchanged, so the
wizard and `lunate setup` name the same three facts in the same words.

#### Scenario: The flag path names all three facts

- **GIVEN** a `lunate setup` run whose model check is rejected with an HTTP status
- **WHEN** the run prints its summary
- **THEN** the model check line names the model id, the API family and the status, and the credential
  text appears nowhere

#### Scenario: The interactive failure notice names all three facts

- **GIVEN** an interactive walk whose model check is rejected with an HTTP status
- **WHEN** the failure is presented
- **THEN** the notice names the model id, the API family and the status, and it equals the detail the
  summary records
