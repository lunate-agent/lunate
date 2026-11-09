## MODIFIED Requirements

### Requirement: The wizard trigger

An interactive run of `lunate` SHALL determine at session start whether the wizard applies: the user provider store has no provider entry, or the `chat` usage has no resolvable profile (`usages.chat` names no configured profile and no `model` default exists). When it applies, and stdin and stdout are terminals and the session is not resuming a printed or non-interactive mode, the session SHALL open the setup wizard once instead of the first-run auth picker offer; Esc at the wizard's first prompt ends it and leaves every file untouched. When any provider entry and a chat profile resolve, the session SHALL start with no wizard, no prompt and no changed welcome-header state. A `chat` usage bound to a profile the family rule refuses (`systemone`, decision-only) SHALL NOT open the wizard: the session SHALL warn once with the binding's message and stay alive, and its first message SHALL refuse with that same binding message — naming the profile, the model, the family, `/setup` in the session and `lunate setup` outside it — without sending a request. A session whose placeholder exists because the machine has no chat answer SHALL refuse its first message with the configure-it text. Non-interactive contexts (print mode, ACP, piped stdin, CI, `TERM=dumb`) SHALL never open the wizard; the configure-it refusal SHALL name `lunate setup` and its flags and exit 2.

#### Scenario: Fresh machine starts the wizard

- **GIVEN** an empty user configuration and terminals on stdin and stdout
- **WHEN** `lunate` starts
- **THEN** the setup wizard opens before the first prompt and no run starts

#### Scenario: Configured machine starts normally

- **GIVEN** one provider entry and a `chat` usage bound to a configured profile
- **WHEN** `lunate` starts
- **THEN** no wizard opens, the welcome header renders its normal block, and the session is idle awaiting input

#### Scenario: A refused chat binding keeps the session and refuses with its own message

- **GIVEN** one provider entry and a `chat` usage bound to a `systemone` profile
- **WHEN** the interactive session starts and the first message is submitted
- **THEN** the session warns once and stays alive, the first message refuses with the binding's message naming the profile, the model, the family, `/setup` and `lunate setup`

#### Scenario: A refused chat binding sends nothing

- **GIVEN** one provider entry and a `chat` usage bound to a `systemone` profile
- **WHEN** the first message is submitted
- **THEN** no chat client is created and no request is sent

#### Scenario: No chat answer keeps the configure-it refusal

- **GIVEN** an interactive session with no chat answer
- **WHEN** the first message is submitted
- **THEN** it refuses with the configure-it text naming `lunate setup`, and no request is sent

#### Scenario: Machine modes never open the wizard

- **GIVEN** print mode, ACP, or stdin piped, on an otherwise fresh machine
- **WHEN** the run starts
- **THEN** no wizard opens, the message points at `lunate setup` with flags, and the run exits 2
