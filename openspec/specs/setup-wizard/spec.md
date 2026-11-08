# setup-wizard Specification

## Purpose

The first-run and re-run setup experience: when the wizard applies, what the full walk collects, how a configured install re-enters it through `/setup` or `lunate setup`, and the invariant that nothing is written before the summary confirms.

## Requirements

### Requirement: The wizard trigger

An interactive run of `lunate` SHALL determine at session start whether the wizard applies: the user provider store has no provider entry, or the `chat` usage has no resolvable profile (`usages.chat` names no configured profile and no `model` default exists). When it applies, and stdin and stdout are terminals and the session is not resuming a printed or non-interactive mode, the session SHALL open the setup wizard once instead of the first-run auth picker offer; Esc at the wizard's first prompt ends it and leaves every file untouched. When any provider entry and a chat profile resolve, the session SHALL start with no wizard, no prompt and no changed welcome-header state. Non-interactive contexts (print mode, ACP, piped stdin, CI, `TERM=dumb`) SHALL never open the wizard; the configure-it refusal SHALL name `lunate setup` and its flags and exit 2.

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
- **THEN** no wizard opens, the message points at `lunate setup` with flags, and the run exits 2

### Requirement: The full walk

The wizard SHALL present ten steps in order — provider, credential, connection test, model, model check, chat profile, optional fast and decision profiles, decisions settings, summary, write — and SHALL implement every step with the existing list picker, masked prompt, visible prompt and question components (no new interaction primitives): only the credential value is read through the masked prompt; every other typed value — provider id, endpoint URL, API family, credential placement, region, models URL, model id, profile name, routing description — is read through the visible prompt, which echoes what is typed, including spaces. The masked prompt hides the credential behind one asterisk per character and keeps pasted text to its non-whitespace characters; the visible prompt shows the characters as typed and keeps pasted spaces, stripping only line breaks. Each step:

1. **Provider** lists the presets in `providers.json` order followed by `Custom OpenAI-compatible endpoint…`, and shows each provider's region (from the preset; asked for a custom provider). The custom path asks the endpoint, its API family (default `openai-chat`), its credential placement (default bearer; `x-api-key` for an Anthropic endpoint), its region and an optional models URL, and refuses an unknown family by name.
2. **Credential** offers "use `<VAR>` from the environment" first when the provider's standard variable is already set (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GOOGLE_API_KEY`, `OPENROUTER_API_KEY`, …), storing an `env:<VARIABLE>` reference; otherwise it reads a key through the masked prompt, and offers a `lunate auth`-stored key when one exists. The credential value SHALL never be written to `providers.json` or `settings.json`.
3. **Connection test** makes one real discovery request through the entry's models URL (the provider's, or the configured one), refusing to leave the entry's host; the failure text names what failed — network, authentication, or an undecipherable response — and offers going back to the credential step rather than aborting the wizard. There is no automatic retry; the go-back loop is the retry.
4. **Model** lists the models the discovery returned (search filters as text is typed); when discovery is unavailable or skipped, the step accepts a manually typed model id. The chosen model's API family is shown beside it.
5. **Model check** makes one minimal real request through the model's resolved API family: one short completion for a chat profile, one tiny noul for a decision profile. The cost SHALL be stated before it runs and the check SHALL be skippable (`--no-model-check` on the flag path). A failure SHALL name the model, the family and the HTTP status or response shape, and offer going back to the model step; a skip or failure SHALL be marked unverified in the summary. A discovery success alone SHALL NOT count as a verified model.
6. **Chat profile** asks the profile name and its thinking level, offering only the levels the chosen model supports; the question is skipped entirely for a model without a reasoning style.
7. **Fast and decision profiles** are optional and default to skip. When the chosen provider exposes the `systemone` family, its Jev model is offered as a **native** decision profile with the preset's pinned version; otherwise the decision profile is offered as an **adapter**, labelled as such in the picker and summary. When the chosen provider is `opencode-go`, the native Jev line is labelled "native Jev — billed to your OpenCode pay-as-you-go balance, not the Go plan; no box questions" and no box question is asked for it; other presets keep the existing native/adapter labels. When routing is enabled, the step asks for each routing profile's description and warns once if one is left blank.
8. **Decisions settings** presents every switch with its `off`/unset default and one explanatory line each; accepting the defaults writes nothing for the layer.
9. **Summary** shows every collected value — provider, region, credential source, endpoint, API family, credential placement, models URL, tested model, model-check result, profiles with their models and thinking levels, decision profile kind (native or adapter), routing descriptions, decisions settings, and what was skipped — and writes nothing until it is confirmed with Enter.
10. **Write** stores the provider entry and credential through the auth and provider stores and the profiles and usages through `SettingsStore`, in one pass.

Every step accepts skip; a skipped step leaves the value it would have configured untouched. A step failed in the connection test or the model check is the card's go-back case: the wizard returns to the earlier step without losing the later answers.

#### Scenario: The walk

- **GIVEN** a fresh machine, a scripted discovery response, a scripted model-check response and an idle session
- **WHEN** the wizard runs from the trigger through step 10
- **THEN** every step is presented in the listed order via the existing components and the final write stores the collected configuration

#### Scenario: Discovery failure goes back, not aborts

- **GIVEN** the connection test answers 401 for the entered key
- **WHEN** the failure is presented
- **THEN** the message names authentication, the wizard re-opens the credential step on request, and previously collected values are retained

#### Scenario: Discovery unavailable is not fatal

- **GIVEN** a provider whose discovery URL returns an undecipherable body
- **WHEN** the model step opens
- **THEN** it accepts a manually typed model id and the wizard continues

#### Scenario: The model check catches a wrong model or family

- **GIVEN** a model id or API family that discovery lists but a real request rejects
- **WHEN** the model check runs
- **THEN** the failure names the model, the resolved family and the response status, and the wizard re-opens the model step without writing anything

#### Scenario: The model check is skippable and never silently verified

- **GIVEN** a user who skips the model check
- **WHEN** the summary renders
- **THEN** the chosen model is labelled unverified, and confirming writes it anyway because the skip was explicit

#### Scenario: An environment key is offered before typing one

- **GIVEN** `ANTHROPIC_API_KEY` is set in the environment and an Anthropic provider is chosen
- **WHEN** the credential step opens
- **THEN** "use `ANTHROPIC_API_KEY` from the environment" is the first choice, and choosing it stores an `env:ANTHROPIC_API_KEY` reference rather than a key value

#### Scenario: Region is shown and asked for

- **GIVEN** a preset provider with a region and a custom provider without one
- **WHEN** each provider step renders
- **THEN** the preset's region is shown, the custom path asks for it, and the summary repeats the chosen value

#### Scenario: Thinking offers only supported levels

- **GIVEN** a chosen model that declares no reasoning style
- **WHEN** the chat-profile step opens
- **THEN** no thinking-level question is asked, and the summary carries no level for that profile

#### Scenario: A native decision profile is offered on the systemone family

- **GIVEN** an OpenRouter provider whose preset exposes `jev-1.14`
- **WHEN** the decisions step offers a decision profile
- **THEN** it offers the native Jev profile with the preset's pinned version and says native

#### Scenario: An adapter decision profile is labelled

- **GIVEN** a provider without the `systemone` family
- **WHEN** the decisions step offers a decision profile
- **THEN** the profile is labelled adapter in the picker and in the summary

#### Scenario: Routing asks for profile descriptions

- **GIVEN** routing is enabled with three profiles
- **WHEN** the decisions step collects them
- **THEN** it asks each profile for a description, warns once if one is blank, and sends the descriptions as criteria per #494 §7

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

#### Scenario: The Go Jev label names the billing

- **GIVEN** the provider step chose the `opencode-go` preset and the decisions step offers a decision profile
- **WHEN** the native Jev line is presented
- **THEN** it says native Jev on the pinned version, states that the cost is billed to the OpenCode pay-as-you-go balance rather than the Go plan, and no box question is asked for it; a non-Go provider keeps the existing native/adapter labels

#### Scenario: Only the credential is masked

- **GIVEN** a scripted walk of the wizard's custom provider path that reaches the credential step
- **WHEN** the provider id, endpoint URL, region, model id and profile name are typed at their steps, and then an API key is typed at the credential step
- **THEN** every non-credential prompt renders and records what was typed, including spaces, and the credential prompt renders one asterisk per character with its plaintext never in the live state

### Requirement: Re-runs open an entry menu

On a configured install, `/setup` SHALL open an entry menu — add a provider, add a profile, change the chat profile, edit the decisions layer, or walk everything — prefilled with the current values, instead of walking every step again. Each entry SHALL write only what changed and SHALL use the same store writers as the full walk. The first run SHALL keep the full walk.

#### Scenario: A configured re-run edits one thing

- **GIVEN** a configured install with one provider and one chat profile
- **WHEN** `/setup` opens and "change the chat profile" is chosen
- **THEN** the current profile and model are prefilled, only the changed values are written, and no other file section is reformatted

#### Scenario: The full walk stays reachable

- **GIVEN** a configured install
- **WHEN** `/setup` opens and "walk everything" is chosen
- **THEN** the same ten steps run as on a first run

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

The wizard SHALL render correctly at 80 and 60 columns through the real TUI component stack at the existing narrow-terminal breakpoints, using the same truncation rules the pickers already apply; no step's prompt, list, or summary SHALL exceed the terminal width. A fixture-driven test SHALL walk the full ten steps through the real component stack at 80 and at 60 columns and pin both render trees.

#### Scenario: Sixty columns stays readable

- **GIVEN** a fixture run of the full walk with discovery data and entered values
- **WHEN** the walk renders at 60 columns
- **THEN** no summary or list line overflows the width and the pinned render matches the fixture

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
