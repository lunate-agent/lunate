## ADDED Requirements

### Requirement: The wizard trigger

An interactive run of `lunate` SHALL determine at session start whether the wizard applies: the user provider store has no provider entry, or the `chat` usage has no resolvable profile (`usages.chat` names no configured profile and no `model` default exists). When it applies, and stdin and stdout are terminals and the session is not resuming a printed or non-interactive mode, the session SHALL open the setup wizard once instead of the first-run auth picker offer; Esc at the wizard's first prompt ends it and leaves every file untouched. When any provider entry and a chat profile resolve, the session SHALL start with no wizard, no prompt and no changed welcome-header state. Non-interactive contexts (print mode, ACP, piped stdin, `TERM=dumb`) SHALL never open the wizard; their existing explicit refusals and exit codes are unchanged.

#### Scenario: Fresh machine starts the wizard

- **GIVEN** an empty user configuration and terminals on stdin and stdout
- **WHEN** `lunate` starts
- **THEN** the setup wizard opens before the first prompt and no run starts

#### Scenario: Configured machine starts normally

- **GIVEN** one provider entry and a `chat` usage bound to a configured profile
- **WHEN** `lunate` starts
- **THEN** no wizard opens, the welcome header renders its normal block, and the session is idle awaiting input

#### Scenario: Machine modes never open the wizard

- **GIVEN** print mode, ACP, or stdin piped, on an otherwise fresh machine
- **WHEN** the run starts
- **THEN** no wizard opens and the run keeps its explicit configure-it error and exit code

### Requirement: The nine steps

The wizard SHALL present nine steps in order — provider, credential, connection test, model, chat profile, optional fast and decision profiles, decisions settings, summary, write — and SHALL implement every step with the existing list picker, masked prompt and question components (no new interaction primitives). Each step:

1. **Provider** lists the presets in `providers.json` order followed by `Custom OpenAI-compatible endpoint…`; the custom path asks the endpoint, its API family (default `openai-chat`), its credential placement (default bearer; `x-api-key` for an Anthropic endpoint) and an optional models URL, and refuses an unknown family by name.
2. **Credential** reads a key through the masked prompt, or accepts an `env:<VARIABLE>` reference; a `lunate auth`-stored key is offered as a choice when one exists. The credential value SHALL never be written to `providers.json` or `settings.json`.
3. **Connection test** makes one real discovery request through the entry's models URL (the provider's, or the configured one), refusing to leave the entry's host; the failure text names what failed — network, authentication, or an undecipherable response — and offers going back to the credential step rather than aborting the wizard.
4. **Model** lists the models the discovery returned (search filters as text is typed); when discovery is unavailable or skipped, the step accepts a manually typed model id. The chosen model's API family is shown beside it.
5. **Chat profile** asks the profile name and its thinking level (`off`/`low`/`medium`/`high`, off the already approved profile schema).
6. **Fast and decision profiles** are optional and default to skip.
7. **Decisions settings** presents every switch with its `off`/unset default and one explanatory line each; accepting the defaults writes nothing for the layer.
8. **Summary** shows every collected value — provider, credential source, endpoint, API family, credential placement, models URL, tested model, profiles with their models and thinking levels, decisions settings, and what was skipped — and writes nothing until it is confirmed with Enter.
9. **Write** stores the provider entry and credential through the auth and provider stores and the profiles and usages through `SettingsStore`, in one pass.

Every step accepts skip; a skipped step leaves the value it would have configured untouched. A step failed in the connection test is the card's go-back case: the wizard returns to the earlier step without losing the later answers.

#### Scenario: The walk

- **GIVEN** a fresh machine, a scripted discovery response and an idle session
- **WHEN** the wizard runs from the trigger through step 9
- **THEN** every step is presented in the listed order via the existing components and the final write stores the collected configuration

#### Scenario: Discovery failure goes back, not aborts

- **GIVEN** the connection test answers 401 for the entered key
- **WHEN** the failure is presented
- **THEN** the message names authentication, the wizard re-opens the credential step on request, and previously collected values are retained

#### Scenario: Discovery unavailable is not fatal

- **GIVEN** a provider whose discovery URL returns an undecipherable body
- **WHEN** the model step opens
- **THEN** it accepts a manually typed model id and the wizard continues

#### Scenario: Skipped steps surface in the summary

- **GIVEN** a wizard run where the auth step was skipped and fast/decision profiles were skipped
- **WHEN** the summary renders
- **THEN** each skipped item is named as skipped and no value was invented for it

#### Scenario: A custom endpoint writes the same entry as `auth login --custom`

- **GIVEN** the wizard's custom path with an Anthropic-compatible endpoint, family `anthropic-messages`
  and placement `x-api-key`
- **WHEN** the summary is confirmed
- **THEN** `providers.json` carries the same entry shape the CLI writes (`baseUrls` keyed by family,
  `credentialPlacement`, optional `modelsUrl`), the credential stays in the auth store, and no secret
  appears in either file

### Requirement: Nothing written before the summary confirms

The wizard SHALL stage every write in memory and commit the collected writes in one pass when the summary is confirmed. An abort — Esc, Ctrl+C, or the session ending before confirmation — SHALL leave `providers.json`, `settings.json` and `auth.json` byte-identical to what they were before the wizard opened. A confirmed summary SHALL keep the files' existing schema versions and their formatting convention (indented JSON, newlines) so the wizard's first write does not reformat a file a user already owns. A summary confirmed after a complete skip-everything run SHALL produce a valid but effectively empty configuration (no new entries, no new usages), not an error and not a partial write.

#### Scenario: Abort writes nothing

- **GIVEN** the wizard walked to the summary and was aborted with Esc
- **WHEN** the bytes of `providers.json` and `settings.json` are compared with the pre-run copies
- **THEN** both files are byte-identical

#### Scenario: Skip everything is a valid empty configuration

- **GIVEN** the wizard walked to the summary with every step skipped and was confirmed
- **WHEN** the session continues
- **THEN** no file contains a new entry or usage, the load succeeds with default settings, and the session starts with the normal not-configured state

### Requirement: One settings writer

The wizard SHALL write every profile, usage and provider value through the same `SettingsStore`, `AuthStore` and provider-store write paths `/settings` and `auth login` use. It SHALL NOT format those files itself, keep its own JSON emitter, or bypass the load-validate-write cycle. A fixture test SHALL fail if a second writer is introduced for the wizard's keys.

#### Scenario: The wizard writes through the store

- **GIVEN** a confirmed wizard run
- **WHEN** the written files are parsed by `SettingsStore`
- **THEN** every value the summary showed is present, and the files carry no value an equivalent `/settings` write would have omitted

### Requirement: Narrow widths and fixtures

The wizard SHALL render correctly at 80 and 60 columns through the real TUI component stack at the existing narrow-terminal breakpoints, using the same truncation rules the pickers already apply; no step's prompt, list, or summary SHALL exceed the terminal width. A fixture-driven test SHALL walk the nine steps through the real component stack at 80 and at 60 columns and pin both render trees.

#### Scenario: Sixty columns stays readable

- **GIVEN** a fixture run of the nine steps with discovery data and entered values
- **WHEN** the walk renders at 60 columns
- **THEN** no summary or list line overflows the width and the pinned render matches the fixture
