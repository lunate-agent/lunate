## MODIFIED Requirements

### Requirement: Usage-to-profile selection

Core selection SHALL resolve usages to profiles and profiles to providers; it SHALL NOT route an individual request among providers. The configured usages SHALL include `chat`, optional `fast`, optional `vision`, `decision`, and defaults for `subagents` and `compaction`. `/model` SHALL switch the chat profile. An explicit `provider/model` SHALL resolve using that provider's default settings. A bare model id SHALL resolve only when exactly one provider/profile candidate matches; otherwise it SHALL be refused with every candidate named. A provider MAY perform its own per-request routing internally, as in the `model-router` sample; core SHALL add no routing DSL.

The `vision` usage SHALL be the vision fallback profile and SHALL require image input. When its resolved model declares image input, a chat model known not to accept images SHALL hand each request image to that profile for a description. An unset `vision` usage, or a `vision` profile whose model does not declare image input, SHALL leave the existing image refusal unchanged. The `fast` usage SHALL carry no vision role. This SHALL add one settings key, `usages.vision`, and no session-schema value.

#### Scenario: Chat model selection changes only chat usage

- **GIVEN** a configured chat profile and other usage defaults
- **WHEN** `/model` selects another profile
- **THEN** chat switches to that profile and fast, vision, decision, subagent, and compaction assignments remain unchanged

#### Scenario: Qualified model uses default settings

- **GIVEN** `provider-a/model-x` and a provider default profile with its settings
- **WHEN** the qualified model is selected ad hoc
- **THEN** it resolves through `provider-a` using the provider's default settings

#### Scenario: Ambiguous bare model id is refused with candidates

- **GIVEN** two providers expose the same bare model id
- **WHEN** that bare id is selected
- **THEN** selection is refused and the error names both `provider/model` candidates

#### Scenario: Routing stays inside a declared provider

- **GIVEN** a declared provider that routes requests internally like the `model-router` sample
- **WHEN** a usage resolves to its profile
- **THEN** core selects that provider once and does not apply a routing DSL or per-request provider selection

#### Scenario: The fast profile describes an image for a text-only chat model

- **GIVEN** a `vision` usage bound to a profile whose model declares image input and a chat model known not to accept images
- **WHEN** a request carries an image
- **THEN** the image goes to the vision profile for a description and the chat request carries the description as text

#### Scenario: A text-only fast profile leaves the refusal unchanged

- **GIVEN** a `vision` usage bound to a profile whose model does not declare image input and a chat model known not to accept images
- **WHEN** a request carries an image
- **THEN** the existing image refusal stands and no vision request is made

#### Scenario: A fast profile does not become the vision profile

- **GIVEN** a `fast` usage bound to a profile whose model declares image input, no `vision` usage, and a chat model known not to accept images
- **WHEN** a request carries an image
- **THEN** the existing image refusal stands and no vision request is made

### Requirement: Usage and provider cost attribution

Every recorded model usage SHALL identify its usage (`chat`, `fast`, `decision`, `subagents`, `compaction`, or `vision`), profile name, provider name and kind, resolved model id, and token lanes. Decision records SHALL additionally carry decision method (`native` or `adapter`) and probability. `/cost` SHALL report priced totals grouped by usage and by provider, retaining the existing rule that unknown pricing is unpriced rather than zero. Session schema 4 SHALL write this attribution and SHALL read schemas 1–3 without changing their interpretation; no provider configuration, credential reference or secret SHALL be written to a session.

#### Scenario: Cost is split by usage and provider

- **GIVEN** priced chat, fast, subagent, compaction, and vision usage across two providers
- **WHEN** `/cost` is requested
- **THEN** it reports subtotals by usage and by provider and a total consistent with those subtotals

#### Scenario: Unknown pricing is not reported as zero

- **GIVEN** a usage record with a positive token lane whose rate is unknown
- **WHEN** `/cost` is requested
- **THEN** that amount is marked unpriced and is not shown as `$0`

#### Scenario: Decision provenance is recorded without credentials

- **GIVEN** a native or adapter decision using a provider profile
- **WHEN** its session record is written
- **THEN** it records provider kind and resolved model id and contains no credential reference or secret

#### Scenario: Earlier session schemas remain readable

- **GIVEN** a schema 1, 2, or 3 session
- **WHEN** the schema-4 reader loads it
- **THEN** it preserves the old model/usage interpretation and loads without inventing usage or provider attribution
