# Provider Store Specification Delta

## ADDED Requirements

### Requirement: Embeddings usage and its declared requirement

The configured usages SHALL gain an optional `embeddings` role whose declared requirement is the
model's `embeddings` API, following the usage rule ADR-0027 records: one settings key
(`usages.embeddings`), one requirement checked at resolution, and one attribution value.

Resolution SHALL read the profile through the same selection path as every usage. A model whose
known capability facts declare an API set without `embeddings` SHALL be refused with an error naming
the profile, the model and the requirement; a model whose API set is unknown SHALL be accepted,
because an unknown capability is not a known failure (ADR-0027 decision 3). A profile whose resolved
API family is not `openai-chat` SHALL be refused, naming the family and the families this build can
serve embeddings on, because only that family has an embeddings wire here. An unset
`usages.embeddings` SHALL leave nothing configured: no embeddings client is built and no notice is
printed for a run that does not use one.

The embeddings call SHALL be attributed as usage `embeddings` with its profile name, provider name
and kind, resolved model id, and token lanes, and SHALL be charged to the run that made it like
every other usage. This SHALL add one settings key and one attribution value and SHALL NOT change
the session schema: schema-4 attribution is a free string.

#### Scenario: A model known to lack the embeddings API is refused

- **GIVEN** a profile whose model's capabilities declare `apis` without `embeddings`
- **WHEN** `usages.embeddings` resolves it
- **THEN** resolution is refused with the profile, the model and `embeddings` named

#### Scenario: An unknown capability set is accepted

- **GIVEN** a profile whose model declares no API capability facts
- **WHEN** `usages.embeddings` resolves it
- **THEN** the profile is accepted and the embeddings client is built for it

#### Scenario: A family without an embeddings wire is refused

- **GIVEN** a profile whose model resolves to `anthropic-messages`
- **WHEN** `usages.embeddings` resolves it
- **THEN** resolution is refused naming the family and `openai-chat`

#### Scenario: The embeddings call is attributed as embeddings

- **GIVEN** a configured embeddings profile and a `semantic_search` call
- **WHEN** the usage is recorded
- **THEN** it carries usage `embeddings`, the profile, the provider kind and the resolved model id,
  and `/cost` groups it under its own usage

## MODIFIED Requirements

### Requirement: Usage and provider cost attribution including vision

Every recorded model usage SHALL identify its usage (`chat`, `fast`, `decision`, `subagents`,
`compaction`, `vision`, or `embeddings`), profile name, provider name and kind, resolved model id,
and token lanes. Decision records SHALL additionally carry decision method (`native` or `adapter`)
and probability. `/cost` SHALL report priced totals grouped by usage and by provider, retaining the
existing rule that unknown pricing is unpriced rather than zero. Session schema 4 SHALL write this
attribution and SHALL read schemas 1–3 without changing their interpretation; no provider
configuration, credential reference or secret SHALL be written to a session.

#### Scenario: Cost is split by usage and provider

- **GIVEN** priced chat, fast, subagent, compaction, vision and embeddings usage across two providers
- **WHEN** `/cost` is requested
- **THEN** it reports subtotals by usage and by provider and a total consistent with those subtotals

#### Scenario: Unknown pricing is not reported as zero

- **GIVEN** a usage record with a positive token lane whose rate is unknown
- **WHEN** `/cost` is requested
- **THEN** that amount is marked unpriced and is not shown as `$0`

#### Scenario: An embeddings usage needs no schema change

- **GIVEN** an embeddings call recorded in a schema-4 session
- **WHEN** the session is reloaded and `/cost` is requested
- **THEN** the usage still reads `embeddings` with its profile and model, and the header schema is
  unchanged

#### Scenario: Decision provenance is recorded without credentials

- **GIVEN** a native or adapter decision using a provider profile
- **WHEN** its session record is written
- **THEN** it records provider kind and resolved model id and contains no credential reference or secret

#### Scenario: Earlier session schemas remain readable

- **GIVEN** a schema 1, 2, or 3 session
- **WHEN** the schema-4 reader loads it
- **THEN** it preserves the old model/usage interpretation and loads without inventing usage or provider attribution
