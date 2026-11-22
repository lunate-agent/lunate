## MODIFIED Requirements

### Requirement: Usage-to-profile selection

Core selection SHALL resolve usages to profiles and profiles to providers; it SHALL NOT route an individual request among providers. The configured usages SHALL include `chat`, optional `fast`, `decision`, and defaults for `subagents` and `compaction`. `/model` SHALL switch the chat profile. An explicit `provider/model` SHALL resolve using that provider's default settings. A bare model id SHALL resolve only when exactly one provider/profile candidate matches; otherwise it SHALL be refused with every candidate named. A provider MAY perform its own per-request routing internally, as in the `model-router` sample; core SHALL add no routing DSL.

The `fast` usage SHALL also be the vision fallback profile. When its resolved model declares image input, a chat model known not to accept images SHALL hand each request image to that profile for a description. An unset `fast` usage, or a `fast` profile whose model does not declare image input, SHALL leave the existing image refusal unchanged. This SHALL add no usage key, settings key or session-schema value.

#### Scenario: Chat model selection changes only chat usage

- **GIVEN** a configured chat profile and other usage defaults
- **WHEN** `/model` selects another profile
- **THEN** chat switches to that profile and fast, decision, subagent, and compaction assignments remain unchanged

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

- **GIVEN** a `fast` usage bound to a profile whose model declares image input and a chat model known not to accept images
- **WHEN** a request carries an image
- **THEN** the image goes to the fast profile for a description and the chat request carries the description as text

#### Scenario: A text-only fast profile leaves the refusal unchanged

- **GIVEN** a `fast` usage bound to a profile whose model does not declare image input and a chat model known not to accept images
- **WHEN** a request carries an image
- **THEN** the existing image refusal stands and no vision request is made
