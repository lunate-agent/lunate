# provider-store Specification

## Purpose
The user-scoped provider store: provider entries that name an endpoint and a credential reference, model catalogs layered over them, and the profiles and usages a session composes from both — validated at load, never carrying a resolved secret.

## Requirements

### Requirement: User-scoped provider entries

The provider store SHALL define providers only in user scope. Each provider entry SHALL have a unique user-facing name, a kind selected from the twelve existing templates (`openai`, `anthropic`, `openrouter`, `deepseek`, `moonshotai`, `zai`, `opencode`, `opencode-go`, `groq`, `togetherai`, `xai`, or `google`), a credential reference, one base URL per API family the provider offers, an optional models/discovery URL, optional headers, and optional region (`eu`, `us`, or `local`). The supported API families SHALL be `openai-chat`, `openai-responses`, `anthropic-messages`, `google` and `systemone`; an unknown family SHALL be refused at load with an error naming it and the supported set. A single `baseUrl` SHALL remain accepted as the shorthand for the default family of the entry's kind, and reading SHALL NOT rewrite a user file. Multiple entries MAY use the same kind. A credential reference SHALL identify an auth-store key, environment variable, or configured helper. A header value SHALL be a literal, a credential reference, or a template over the fixed variable set `{session.id}`, `{run.id}` and `{lunate.version}`; any other placeholder SHALL be refused at load with the placeholder named, and a template SHALL have no access to the environment or to the credential. Each provider entry SHALL declare how its credential is sent: a header (default `Authorization: Bearer`), a named header (`x-api-key`, `api-key`, …), or a query parameter (`key`); the placement SHALL be validated at load and SHALL be the only place the credential appears on the wire. Provider and project settings SHALL never contain resolved credentials or resolved secret header values. The `region` value SHALL be preserved as metadata; this change SHALL NOT route data or enforce a region policy.

#### Scenario: Multiple entries share a template kind

- **GIVEN** two user provider entries with different names and kind `openai`
- **WHEN** the provider store loads
- **THEN** both entries resolve independently with their own base URL, credential reference, headers, and region

#### Scenario: Region is retained without routing policy

- **GIVEN** a provider entry with region `eu`
- **WHEN** a profile uses that provider
- **THEN** the region remains available as provider metadata and does not select an endpoint or route a request

#### Scenario: Provider files contain references, never secrets

- **GIVEN** an auth key, environment variable, or helper resolves to a credential or header value
- **WHEN** provider settings are written or listed
- **THEN** only references and non-secret literals are stored or shown and no resolved secret value is present

#### Scenario: An entry declares one base URL per family

- **GIVEN** a provider entry with `baseUrls` for `openai-chat` and `anthropic-messages`
- **WHEN** the provider store loads
- **THEN** both URLs are available to routing and neither family is inferred for a model that declares none

#### Scenario: The single base URL shorthand still loads

- **GIVEN** a provider entry written before this change with one `baseUrl` and kind `openai`
- **WHEN** the provider store loads
- **THEN** the entry is usable as `openai-chat` and the file is not rewritten

#### Scenario: An unknown family is refused by name

- **GIVEN** a provider entry whose `baseUrls` contains `openai-realtime`
- **WHEN** the provider store loads
- **THEN** loading fails with an error naming the family and the supported set

#### Scenario: A templated header is validated at load

- **GIVEN** a provider entry with the header `x-session: {session.id}` and another with `x-session: {user.email}`
- **WHEN** the provider store loads
- **THEN** the first is accepted and the second is refused with `{user.email}` named

#### Scenario: Each credential placement loads and is the only placement

- **GIVEN** four entries: default bearer, `x-api-key`, named `api-key`, and query `key`
- **WHEN** each sends a request
- **THEN** the credential appears once, in exactly the declared place, and in no other header or query parameter

#### Scenario: A template cannot reference the credential

- **GIVEN** a header value `{credential}`, `{apiKey}` or `{key}`
- **WHEN** the provider store loads
- **THEN** loading is refused with that placeholder named as not one of `{session.id}`, `{run.id}`, `{lunate.version}`

#### Scenario: An invalid placement is refused by field

- **GIVEN** a placement with an empty name, an unknown `in`, or a scheme containing a control character
- **WHEN** the provider store loads
- **THEN** loading is refused naming `credentialPlacement` and the offending field

### Requirement: Provider listing and credential migration

`lunate auth list` SHALL list provider entries with credential present/absent and endpoint reachability (`reachable`, `unreachable`, or `unknown`), without printing credential values or header values. Reachability SHALL be a bounded endpoint-connectivity check that does not send a credential or optional header. Loading an existing auth store SHALL automatically create the matching provider entry for each stored key using its existing preset or custom provider definition; migration SHALL preserve the key in the auth store and SHALL NOT replace an explicit provider entry. Provider environment overrides SHALL continue to take precedence over stored credentials, including in CI.

#### Scenario: Auth list reports providers safely

- **GIVEN** one reachable provider with a credential, one unreachable provider without one, and a header value
- **WHEN** `lunate auth list` runs
- **THEN** both providers show credential presence and reachability, and neither the header value nor any credential value is printed

#### Scenario: Existing stored keys create provider entries

- **GIVEN** a legacy auth store with a stored key and no matching provider entry
- **WHEN** settings load and migration completes
- **THEN** the corresponding provider entry is created using the existing template/custom definition and the secret remains only in the auth store

#### Scenario: Explicit provider settings survive key migration

- **GIVEN** an existing stored key and a provider entry with the same name but a custom base URL
- **WHEN** migration runs
- **THEN** the explicit provider entry is preserved and the stored key remains referenced without its value being copied

#### Scenario: Environment credentials still win in CI

- **GIVEN** a migrated provider with an auth-store key and its environment override set
- **WHEN** a client resolves the credential
- **THEN** the environment value is used and the auth-store value remains unchanged

### Requirement: Named model profiles

A model profile SHALL map a unique profile name to one provider and one catalog model, with optional reasoning level or budget, maximum output, temperature, context-window override, and decision thresholds. One provider MAY back multiple profiles. Profile settings SHALL be validated against the selected catalog model and provider when configuration loads. An unsupported setting SHALL fail loading with the setting name and profile name; it SHALL NOT be silently dropped or ignored. Project profiles MAY reference an existing provider and define settings on top of it, but SHALL NOT create provider definitions.

A profile's reasoning level SHALL be the default thinking level for every session that runs on the profile: the launch `--thinking` flag SHALL win over it for that run, and the settings file's global `thinking` key SHALL apply only where the profile does not name a reasoning level. The session's own `thinking` command SHALL change the session only and never write the profile. A reasoning budget stays a validated configuration fact; it is not applied as the session default — the session-level parameter is the thinking level.

#### Scenario: One provider backs multiple profiles

- **GIVEN** two profiles that name the same provider and different catalog models or settings
- **WHEN** both profiles load
- **THEN** both resolve independently against that provider

#### Scenario: Unsupported profile setting is named

- **GIVEN** a profile sets temperature on a model that does not support temperature
- **WHEN** profiles load
- **THEN** loading is refused with the profile name and `temperature`, and no model call starts

#### Scenario: A profile validates every configured setting

- **GIVEN** a profile with supported reasoning, output, temperature, context-window, and decision-threshold settings
- **WHEN** profiles load
- **THEN** the resolved settings match the validated catalog/provider capabilities and unsupported fields are not discarded

#### Scenario: The profile's reasoning level is the session default

- **GIVEN** the chat usage names a profile whose `reasoningLevel` is `high` and the user settings carry `thinking: "low"`
- **WHEN** a session starts on that profile
- **THEN** its thinking default is `high` without any launch flag, and the same level applies to print and ACP sessions on the profile

#### Scenario: The launch flag wins over the profile

- **GIVEN** a chat profile whose `reasoningLevel` is `high`
- **WHEN** `--thinking off` is passed
- **THEN** the session (and the print and ACP paths) start with the thinking level `off`

#### Scenario: The global key fills where the profile is silent

- **GIVEN** a chat profile without a reasoning setting and a user settings file with `thinking: "low"`
- **WHEN** a session starts on that profile
- **THEN** its thinking default is `low`, as it was before this requirement existed

#### Scenario: The session command never writes the profile

- **GIVEN** a session running on a profile whose `reasoningLevel` is `high`
- **WHEN** the session's thinking level is changed
- **THEN** the settings file still carries `reasoningLevel: "high"`

### Requirement: A profile's model resolves in its provider's scope

A profile names one provider entry and one model. Resolution SHALL read the model id in that provider's scope first — the entry's own catalogue id `<provider>/<model>`, then the exact catalogue id, then the single catalogue entry whose wire id the id is.

An entry whose name is not a provider preset id is a custom entry: its kind is only the adapter that speaks its API family's wire. A profile on a custom entry that names a model the catalogue holds under another provider SHALL resolve its model through the entry itself — its resolved id is `<provider id>/<model>` and its wire id is what the endpoint is sent — and the catalogue SHALL NOT be a reason to refuse it: the matching catalogue entry supplies model metadata only. A bare id no catalogue entry matches at all SHALL resolve through the entry with unknown limits. A model the catalogue already holds under the entry's own kind SHALL keep that catalogue entry, exactly as a preset entry's model does.

A profile on a preset entry SHALL keep the existing rule: the catalogue entry SHALL belong to the preset's kind, and a bare id that several catalogue entries hold SHALL keep the refusal that names every candidate.

A profile SHALL be refused when the model's API family is not one the entry serves. The refusal SHALL name the entry, the families it serves and the family the model speaks.

#### Scenario: A custom endpoint's profile names a bare id the catalogue holds under another provider

- **GIVEN** a custom entry `local` of kind `openai` whose endpoint is an OpenAI-compatible server, and a catalogue that holds `kimi-k2` only as `opencode/kimi-k2`
- **WHEN** a profile naming `local` with model `kimi-k2` resolves
- **THEN** it resolves as `local/kimi-k2` with wire id `kimi-k2` and the entry's endpoint, and nothing refuses it

#### Scenario: A custom endpoint's metadata comes from a matching catalogue entry

- **GIVEN** a custom entry `local` of kind `openai` and a catalogue that holds `opencode/kimi-k2` with a context window and tool support
- **WHEN** a profile naming `local` with model `kimi-k2` resolves
- **THEN** the resolved model carries that context window and capability, and its provider is the entry's kind `openai`

#### Scenario: A custom endpoint's profile is refused for another family

- **GIVEN** a custom entry `local` of kind `openai` and a catalogue entry `claude-sonnet-4-5` whose provider is `anthropic`
- **WHEN** a profile naming `local` with model `claude-sonnet-4-5` resolves
- **THEN** it is refused with a message naming `local`, the family it serves (`openai-chat`) and the family the model speaks (`anthropic-messages`)

#### Scenario: A custom provider's bare model id resolves

- **GIVEN** a provider entry `local` whose catalogue entry is `local/kimi-k2.7-code` with wire id `kimi-k2.7-code`, and a profile naming `local` with model `kimi-k2.7-code`
- **WHEN** the profile resolves
- **THEN** it resolves `local/kimi-k2.7-code`, keeps the entry's endpoint, and sends the wire id `kimi-k2.7-code`

#### Scenario: A custom endpoint naming the vendor's own model keeps the catalogue entry

- **GIVEN** a custom entry `local` of kind `openai` and a profile naming it with model `gpt-4o`, whose catalogue entry belongs to `openai`
- **WHEN** the profile resolves
- **THEN** it resolves the catalogue entry `gpt-4o` exactly as a preset profile does

#### Scenario: A preset profile keeps a bare id that its provider owns

- **GIVEN** a `moonshotai` provider entry and a profile naming it with model `kimi-k2.7-code`
- **WHEN** the profile resolves
- **THEN** it resolves `moonshotai/kimi-k2.7-code` instead of failing as ambiguous

#### Scenario: A preset profile refuses a model of another provider

- **GIVEN** a `moonshotai` provider entry and a profile naming it with model `gpt-4o`, whose catalogue entry belongs to `openai`
- **WHEN** the profile resolves
- **THEN** it is refused naming the entry's kind and the model's provider, as before

#### Scenario: A bare id no provider owns still names its candidates

- **GIVEN** a `groq` provider entry and a profile naming it with a model id that matches several catalogue entries and no entry of `groq`
- **WHEN** the profile resolves
- **THEN** it is refused with every candidate named, as before

### Requirement: Usage-to-profile selection

Core selection SHALL resolve usages to profiles and profiles to providers; it SHALL NOT route an individual request among providers. The configured usages SHALL include `chat`, optional `fast`, `decision`, and defaults for `subagents` and `compaction`. `/model` SHALL switch the chat profile. An explicit `provider/model` SHALL resolve using that provider's default settings. A bare model id SHALL resolve only when exactly one provider/profile candidate matches; otherwise it SHALL be refused with every candidate named. A provider MAY perform its own per-request routing internally, as in the `model-router` sample; core SHALL add no routing DSL.

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

### Requirement: User-selected decision profiles and contract safety

Any model profile SHALL be accepted for `decision` when the user assigns it; model capability is the user's judgement, not a product gate. Catalog/discovery `apiType` MAY label the call method but SHALL NOT reject a profile. A provider decision API SHALL be labelled `native`; a chat-model path SHALL be labelled `adapter`, directly or through the explicit wrapper. Every response SHALL contain one of the defined options and well-formed probabilities: one finite value in [0, 1] per option, summing to 1 within the contract tolerance. A malformed response SHALL be retried once; if the retry is also malformed, the decision SHALL fail with an error naming the model. This is a transient technical failure and SHALL NOT create a persistent model-capability state. Decision provenance SHALL carry model id, method (`native` or `adapter`) and probability, in addition to normal usage/profile/provider-name-and-kind attribution. Shadow mode SHALL be an opt-in user setting, not a per-model promotion gate. A decision MAY only tighten the approval result: it SHALL NOT override a deny or approve outside the user's allow envelope. If no decision profile is configured, the decision layer SHALL report unavailable and remain off; it SHALL NOT implicitly borrow chat.

#### Scenario: A user-selected model is accepted regardless of metadata

- **GIVEN** a model profile explicitly assigned to decision, including one whose `apiType` is `chat`
- **WHEN** the decision usage resolves
- **THEN** the profile is accepted and labelled `native` for a provider decision API or `adapter` for a chat-model path, without capability, vendor or id inference

#### Scenario: One provider instance backs chat and decision profiles

- **GIVEN** an OpenRouter-shaped preset with chat and decision API endpoints and one provider entry with one credential reference
- **WHEN** the user assigns profiles using that provider entry to chat and decision
- **THEN** both profiles resolve through the same provider entry and credential, using the endpoint for each API

#### Scenario: A malformed contract is retried once then fails with model named

- **GIVEN** a model returns malformed decision contracts on the initial call and the retry
- **WHEN** the second response is validated
- **THEN** the decision fails with an error naming the model and no persistent capability verdict is stored

#### Scenario: A valid contract is accepted

- **GIVEN** a model returns one defined option and a well-formed probability distribution
- **WHEN** the response is validated
- **THEN** the decision is accepted and records only its model id, `native`/`adapter` method and probability

#### Scenario: No decision profile leaves the layer unavailable

- **GIVEN** chat is configured and no decision profile is configured
- **WHEN** the decision layer initializes or is requested
- **THEN** it reports unavailable and stays off without invoking the chat profile

#### Scenario: Shadow mode is explicitly opt-in

- **GIVEN** a user has not enabled shadow mode
- **WHEN** a decision profile is configured
- **THEN** the decision consumer does not enter shadow mode or require a calibration gate

#### Scenario: A decision cannot override deny or exceed the allow envelope

- **GIVEN** a deny outcome or an action outside the user's allow envelope
- **WHEN** the decision result is applied
- **THEN** the denial remains final and the out-of-envelope action remains unapproved

### Requirement: Usage and provider cost attribution

Every recorded model usage SHALL identify its usage (`chat`, `fast`, `decision`, `subagents`, or `compaction`), profile name, provider name and kind, resolved model id, and token lanes. Decision records SHALL additionally carry decision method (`native` or `adapter`) and probability. `/cost` SHALL report priced totals grouped by usage and by provider, retaining the existing rule that unknown pricing is unpriced rather than zero. Session schema 4 SHALL write this attribution and SHALL read schemas 1–3 without changing their interpretation; no provider configuration, credential reference or secret SHALL be written to a session.

#### Scenario: Cost is split by usage and provider

- **GIVEN** priced chat, fast, subagent, and compaction usage across two providers
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

### Requirement: Project provider-definition security boundary

Project settings MAY reference existing user providers and profiles, define project profiles on top of existing user providers, and lower user-configured limits. A project profile MAY reference an existing user profile. Project settings SHALL NOT define a provider, endpoint/base URL, or credential reference. A refused definition SHALL name the offending key and SHALL fail closed before resolving a secret or opening a connection. Providers SHALL be user-scoped because otherwise a repository could direct a provider to an attacker-controlled URL and receive the user's credential.

#### Scenario: Project profile references an existing provider

- **GIVEN** a project profile that references an existing user provider and lowers an output limit
- **WHEN** project settings load
- **THEN** the profile is accepted and the effective limit is the lower value

#### Scenario: Project settings reference an existing user profile

- **GIVEN** project settings that assign a usage to an existing user-defined profile
- **WHEN** project settings load
- **THEN** the reference resolves to that profile without creating or changing its provider definition

#### Scenario: Project cannot raise a user limit

- **GIVEN** a user limit and a higher project override
- **WHEN** project settings load
- **THEN** the override is refused by key and the user limit remains effective

#### Scenario: Attacker endpoint and credential reference are refused

- **GIVEN** a project settings file defining a provider with an attacker-controlled URL and a credential reference
- **WHEN** project settings load
- **THEN** loading is refused with the offending provider/endpoint/credential key named, and no secret is resolved or request sent

### Requirement: Cross-process provider-store writes

The provider store SHALL hold a cross-process claim for every save and for its migration
read-modify-write, so a migration in one process does not replace a document another process saved
from a stale read. The claim SHALL be a lock file beside `providers.json`, distinct from the store
file so a reader is not refused. A migration SHALL take the claim and re-read the store under it
before writing. The setup wizard's commit SHALL take the claim before it reads the store and SHALL
merge its entry into the document it reads under the claim, not into a reading taken before the
walk. A read-only load SHALL NOT take the claim and SHALL NOT create the lock file. A write that
cannot take the claim within the bounded wait SHALL refuse with an error naming the store file and
the lock file, and SHALL leave the store unchanged.

#### Scenario: A migration re-reads under the claim

- **GIVEN** a provider entry saved by another process after a load's first read
- **WHEN** that load needs migration and takes the claim
- **THEN** it re-reads the store under the claim and preserves the other process's entry when it
  writes

#### Scenario: A migration that cannot take the claim refuses

- **GIVEN** another process holds the provider-store claim past the bounded wait
- **WHEN** a migration needs to write
- **THEN** it fails with an error naming `providers.json` and the lock file, and the store is
  unchanged

#### Scenario: A read-only load creates no claim

- **GIVEN** a provider store with entries and nothing to migrate
- **WHEN** it loads
- **THEN** no lock file exists beside it

#### Scenario: The setup wizard's commit keeps an entry saved during the walk

- **GIVEN** a provider entry another process saved after the wizard's walk had read the store
- **WHEN** the wizard's commit takes the claim, seeds its staged copy and writes its own entry
- **THEN** the saved entry is still in the store, beside the entry the walk staged
