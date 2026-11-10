# setup-wizard Specification

## Purpose

The first-run and re-run setup experience: when the wizard applies, what the full walk collects, how a configured install re-enters it through `/setup` or `lunate setup`, and the invariant that nothing is written before the summary confirms.

## Requirements

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

### Requirement: The full walk

The wizard SHALL present ten steps in order — provider, credential, connection test, model, model check, chat profile, optional fast and decision profiles, decisions settings, summary, write — and SHALL implement every step with the existing list picker, masked prompt and question components (no new interaction primitives): only the credential value is read through the masked prompt; every other typed value — provider id, endpoint URL, API family, credential placement, region, models URL, model id, profile name, routing description — is read through the visible prompt, which echoes what is typed, including spaces. The masked prompt hides the credential behind one asterisk per character and keeps pasted text to its non-whitespace characters; the visible prompt shows the characters as typed and keeps pasted spaces, stripping only line breaks. Each step:

1. **Provider** lists the presets in `providers.json` order followed by `Custom OpenAI-compatible endpoint…`, and shows each provider's region (from the preset; asked for a custom provider). The custom path asks the endpoint, its API family (default `openai-chat`), its credential placement (default bearer; `x-api-key` for an Anthropic endpoint), its region and an optional models URL, and refuses an unknown family by name.
2. **Credential** offers "use `<VAR>` from the environment" first when the provider's standard variable is already set (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GOOGLE_API_KEY`, `OPENROUTER_API_KEY`, …), storing an `env:<VARIABLE>` reference; otherwise it reads a key through the masked prompt, and offers a `lunate auth`-stored key when one exists. The credential value SHALL never be written to `providers.json` or `settings.json`.
3. **Connection test** makes one real discovery request through the entry's models URL (the provider's, or the configured one), refusing to leave the entry's host; the failure text names what failed — network, authentication, or an undecipherable response — and offers going back to the credential step rather than aborting the wizard. There is no automatic retry; the go-back loop is the retry.
4. **Model** lists the models the discovery returned (search filters as text is typed); when discovery is unavailable or skipped, the step accepts a manually typed model id. The chosen model's API family is shown beside it.
5. **Model check** makes one minimal real request through the model's resolved API family: one short completion for a chat profile, one tiny noul for a decision profile. The cost SHALL be stated before it runs and the check SHALL be skippable (`--no-model-check` on the flag path). A failure SHALL name the model, the family and the HTTP status or response shape, and offer going back to the model step; a skip or failure SHALL be marked unverified in the summary. A discovery success alone SHALL NOT count as a verified model.
6. **Chat profile** asks the profile name and its thinking level, offering only the levels the chosen model supports; the question is skipped entirely for a model without a reasoning style.
7. **Fast and decision profiles** are optional and default to skip. Both steps offer a model from any configured provider (see Fast and decision usages from any provider). When the chosen provider exposes the `systemone` family, its Jev model is offered as a **native** decision profile with the preset's pinned version; otherwise the decision profile is offered as an **adapter**, labelled as such in the picker and summary. When the chosen provider is `opencode-go`, the native Jev line is labelled "native Jev — billed to your OpenCode pay-as-you-go balance, not the Go plan; no box questions" and no box question is asked for it; other presets keep the existing native/adapter labels. When routing is enabled, the step asks for each routing profile's description and warns once if one is left blank.
8. **Decisions settings** presents every switch with its `off`/unset default and one explanatory line each, plus a **Done — leave the decisions settings** entry. After each switch is answered the step SHALL present the list again with the earlier answers kept, so several switches are collected in one pass; only the Done entry leaves the step. Accepting the defaults — Done with no switch changed — writes nothing for the layer.
9. **Summary** shows every collected value — provider, region, credential source, endpoint, API family, credential placement, models URL, tested model, model-check result, profiles with their models and thinking levels, usage profiles with their provider and model, decision profile kind (native or adapter), every provider added inline with its credential and check, routing descriptions, decisions settings, and what was skipped — and writes nothing until it is confirmed with Enter.
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

#### Scenario: The decisions list stays open until Done

- **GIVEN** the decisions settings step is open and the user changes the approval gate
- **WHEN** the approval question is answered
- **THEN** the decisions list is presented again with the approval answer kept, and choosing **Done — leave the decisions settings** leaves the step

#### Scenario: Two decision settings change in one pass

- **GIVEN** the decisions settings step is open and the user changes the approval gate and then the decide tool
- **WHEN** the user chooses **Done — leave the decisions settings**
- **THEN** the summary and the commit carry both changed settings from the same pass

#### Scenario: Skipped steps surface in the summary

- **GIVEN** a wizard run where the auth step was skipped and fast/decision profiles were skipped
- **WHEN** the summary renders
- **THEN** each skipped item is named as skipped and no value was invented for it

#### Scenario: The Go Jev label names the billing

- **GIVEN** the provider step chose the `opencode-go` preset and the decisions step offers a decision profile
- **WHEN** the native Jev line is presented
- **THEN** it says native Jev on the pinned version, states that the cost is billed to the OpenCode pay-as-you-go balance rather than the Go plan, and no box question is asked for it; a non-Go provider keeps the existing native/adapter labels

#### Scenario: Only the credential is masked

- **GIVEN** a scripted walk of the wizard's custom provider path that reaches the credential step
- **WHEN** the provider id, endpoint URL, region, model id and profile name are typed at their steps, and then an API key is typed at the credential step
- **THEN** every non-credential prompt renders and records what was typed, including spaces, and the credential prompt renders one asterisk per character with its plaintext never in the live state

### Requirement: The model step resolves the id in the walked provider's scope

The model step SHALL resolve the chosen model id against the provider entry the walk is configuring before it resolves anything else: the catalogue entry `<provider id>/<model id>` first, then the exact catalogue id, then the single catalogue entry whose wire id the chosen id is. A discovered listing of a custom endpoint SHALL keep serving bare wire ids, and such an id SHALL never abort the walk. Where the walked provider's own entry answers, the walk SHALL stage the id without an extra prompt; where it does not, and the catalogue holds the id under several providers, the walk SHALL offer those qualified ids in a picker and stage the chosen one. Where nothing in the catalogue matches, the walk SHALL stage the id as given and let the model check decide. No step of the walk SHALL fail with an internal ambiguity message.

#### Scenario: A custom endpoint's bare id that several catalogue providers serve

- **GIVEN** a custom endpoint whose discovery listing offers `kimi-k2.7-code`, which the catalogue holds as `moonshotai/kimi-k2.7-code`, `opencode/kimi-k2.7-code` and `opencode-go/kimi-k2.7-code`
- **WHEN** the walk stages that id at the model step
- **THEN** the walk continues to the model check without a prompt, and the summary's model is `kimi-k2.7-code`

#### Scenario: An id one catalogue entry of the walked provider answers

- **GIVEN** the `moonshotai` preset and a discovery listing that offers `kimi-k2.7-code`
- **WHEN** the walk stages that id at the model step
- **THEN** it stages it without a picker, and the model check resolves `moonshotai/kimi-k2.7-code`

#### Scenario: An id several unrelated catalogue providers serve

- **GIVEN** the `openai` preset and a model id the catalogue holds under `moonshotai`, `opencode` and `opencode-go`
- **WHEN** the walk stages that id at the model step
- **THEN** the step offers `moonshotai/kimi-k2.7-code`, `opencode/kimi-k2.7-code` and `opencode-go/kimi-k2.7-code` and stages the one the user picks

#### Scenario: The flag path takes the same resolution

- **GIVEN** `lunate setup --provider local --endpoint <url> --model kimi-k2.7-code`
- **WHEN** the walk resolves the model's API family and runs its checks
- **THEN** it completes and exits 0 instead of failing with an ambiguity error

#### Scenario: The written configuration resolves on the next start

- **GIVEN** a walk that staged a custom endpoint with the bare id `kimi-k2.7-code`
- **WHEN** the confirmed configuration is loaded for the next session
- **THEN** the profile resolves that id through its own provider entry, not through the whole catalogue

#### Scenario: A custom endpoint writes the same entry as `auth login --custom`

- **GIVEN** a custom endpoint answered in the provider step with a typed key and a model
- **WHEN** the summary is confirmed
- **THEN** `settings.providers` receives the same entry shape `auth login --custom` writes and the key lands only in the auth store

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

### Requirement: The decision check carries a request identity

The decision-profile check SHALL send its request with a request identity: the current session's id when the wizard runs inside a session (including `/setup`), and a fresh ephemeral probe id minted for the check when it runs standalone through `lunate setup`. The identity SHALL travel through the same resolved profile, provider entry, API family and client path a session uses for the same profile; the check SHALL NOT patch a provider header at its call site. A provider whose profile declares no session header SHALL send the same request it sends today.

#### Scenario: The OpenCode Go decision check passes

- **GIVEN** the `opencode-go` preset and a native `jev-1.13` decision profile
- **WHEN** the decision check runs outside a session through `lunate setup`
- **THEN** the check passes and its request carries a non-empty `x-opencode-session` probe id

#### Scenario: An in-session decision check names that session

- **GIVEN** a session with an id and a header-templated provider chosen through `/setup`
- **WHEN** the wizard runs the decision check
- **THEN** the request on the wire carries exactly that session's id in `x-opencode-session`

#### Scenario: A provider without a session header is unaffected

- **GIVEN** a provider whose profile declares no session header
- **WHEN** the decision check runs
- **THEN** the check passes and the request carries no session header

### Requirement: Fast and decision usages from any provider

The fast and decision steps SHALL each offer a flat `provider/model` list built from the provider the walk configured in this run, every other provider already configured on the machine, and every provider added inline during this run. Each row SHALL name the provider that serves it, the model's API family, and — for the decision step — the native/adapter label; a model whose family declares a reasoning style SHALL show the thinking levels the following picker offers. The list SHALL end with `Set up another provider inline…` and `Skip`.

Choosing the model the chat profile already uses SHALL keep today's binding: the chat profile also serves the `fast` usage. Choosing any other model SHALL write a profile for the chosen provider and model and bind the usage to it; a decided profile is named `decisions`, and a fast profile `fast` (suffixed with the provider name when `fast` is taken). No usage answer SHALL carry an `apiFamily` the chosen model's provider does not resolve, and the profile SHALL name the provider that serves it, not the walk's own provider.

`Set up another provider inline…` SHALL run the provider, credential, connection-test and model sub-walk without leaving the usage step, then return to the same usage step with the answer it already had; the added provider and its credential SHALL appear in the review and SHALL be written in the same one-pass commit. A check the usage step runs SHALL send its request with the selected model's own provider entry and credential — a typed inline key stays in memory until the commit — and SHALL state the estimated cost before it runs; a skip SHALL leave that usage marked unverified in the summary.

The `lunate setup` flag path SHALL keep its current answers: `--fast` binds the chat profile as `fast`, and `--decision` takes the run's own provider and model.

#### Scenario: Fast from the provider configured in this run

- **GIVEN** a walk that answered the chat profile from the chosen provider and no fast profile yet
- **WHEN** the fast step picks the chat profile's model
- **THEN** the usage `fast` binds the chat profile, a passing fast check is recorded, and the commit writes `usages.fast` without a second profile

#### Scenario: Fast from another configured provider

- **GIVEN** a configured `anthropic` provider besides the `openai` provider configured in this run
- **WHEN** the fast step picks `anthropic/claude-sonnet-5-5` and its check passes
- **THEN** a `fast` profile naming the `anthropic` provider is written, `usages.fast` binds it, and the review names both the provider and the model

#### Scenario: Decision from another configured provider

- **GIVEN** a configured `anthropic` provider and a walk that configured `openai`
- **WHEN** the decision step picks an adapter model on `anthropic` and the decision check passes
- **THEN** the `decisions` profile names `anthropic`, its model and the adapter label, `decisions.profile` binds it, and the check's request went to the Anthropic entry's credential

#### Scenario: Inline provider for a usage

- **GIVEN** a walk whose only provider is `openai` and a decision usage that needs an `opencode-go` native Jev model
- **WHEN** the decision step chooses `Set up another provider inline…` and completes the sub-walk
- **THEN** the sub-walk returns to the decision step with every earlier answer intact, the `opencode-go` provider and credential appear in the review, the native decision profile binds it, and the commit writes the provider, the credential and the profile in one pass

#### Scenario: No route loses the answer the step already had

- **GIVEN** a fast or decision step that has already selected a model
- **WHEN** the user opens the inline provider sub-walk and returns, or picks a different model
- **THEN** the chat profile, the model check and the decisions settings are unchanged, and only the usage answer is replaced

#### Scenario: A skipped usage check stays unverified

- **GIVEN** a usage whose check the user skips
- **WHEN** the summary renders
- **THEN** the usage is named unverified and confirming writes it anyway, because the skip was explicit

### Requirement: The chat usage moves across configured providers

On a configured install, the "change the chat profile" entry SHALL offer one list: the configured profiles with the current binding marked, and every configured provider's models as `provider/model` with the model's family, plus `Set up another provider inline…`. Choosing an existing profile SHALL repoint `usages.chat` and write nothing else. Choosing a model SHALL move the current profile to that model's provider and model under the same name, ask the thinking level the model declares, write that profile and `usages.chat`, and touch no other profile, provider or credential entry. The inline row SHALL run the provider, credential, connection-test and model sub-walk and return to the same list with the added provider's models offered. A model whose family is decision-only SHALL be refused with the shared chat-binding message naming `/setup` and `lunate setup`. The first-run path is unchanged: it walks the provider and model steps and binds the chat usage it collected.

#### Scenario: Move chat to another configured provider's model

- **GIVEN** a configured install with an `openai` chat profile and an `anthropic` provider
- **WHEN** "change the chat profile" picks `anthropic/claude-sonnet-5-5`
- **THEN** the chat profile names `anthropic` and that model, `usages.chat` still binds it, and no other profile with its provider and credential entries changes

#### Scenario: An existing profile still rebinds the usage

- **GIVEN** a configured install with two profiles and `usages.chat` on the first
- **WHEN** "change the chat profile" picks the second profile's row
- **THEN** `usages.chat` names the second profile and no profile entry changes

#### Scenario: The first-run walk keeps its order

- **GIVEN** a fresh machine with one provider
- **WHEN** the full walk reaches the chat profile step
- **THEN** it asks the profile name for the model the model step collected, not a provider/model list
