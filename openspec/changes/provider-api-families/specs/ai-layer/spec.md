## MODIFIED Requirements

### Requirement: Provider presets and routing

Lunate SHALL provide presets for `openai`, `anthropic`, `openrouter`, `deepseek`, `moonshotai`, `zai`, `opencode`, `opencode-go`, `groq`, `togetherai`, `xai` and `google`, defined in one embedded `providers.json`. A preset SHALL declare one base URL per API family it offers (at minimum its default family), an optional models/discovery URL, optional headers, and a key variable. `openai` and `anthropic` SHALL keep their native adapters for their default families; every other preset SHALL route each model through the adapter of that model's API family (see Provider API families). The provider call SHALL use `wireId` when present and the catalog id otherwise. A model with an explicit endpoint SHALL keep that endpoint, and a user-declared endpoint SHALL keep the placeholder-credential rule. An unknown provider SHALL fail with an actionable error listing the preset ids.

For a preset whose base URL is the provider's own endpoint, the credential SHALL resolve from the preset's environment variable first, then the auth store under the provider name, unless the model declares an `auth` reference, which wins. The Google preset SHALL accept `GOOGLE_API_KEY` and `GEMINI_API_KEY`.

#### Scenario: A preset routes through the OpenAI adapter

- **GIVEN** a catalog entry with provider `groq` and no endpoint
- **WHEN** the factory builds the pipeline
- **THEN** the client targets the Groq preset base URL and sends the model's wire id

#### Scenario: A wire id differs from the catalog id

- **GIVEN** a catalog entry `openrouter/anthropic/claude-sonnet-5-5` with `wireId` `anthropic/claude-sonnet-5-5`
- **WHEN** the request is built
- **THEN** the provider receives `anthropic/claude-sonnet-5-5` and the catalog lookup still uses the qualified id

#### Scenario: Anthropic stays native

- **GIVEN** a catalog entry with provider `anthropic` and no endpoint
- **WHEN** the factory builds the pipeline
- **THEN** the Anthropic adapter serves it with the SDK default endpoint

#### Scenario: A user endpoint keeps the placeholder

- **GIVEN** a preset model overridden with a user-declared endpoint and no `auth` reference
- **WHEN** the factory builds the pipeline
- **THEN** the client uses the placeholder credential, not the preset environment key

#### Scenario: The preset key resolves from the environment then the store

- **GIVEN** a `groq` model with no `GROQ_API_KEY` and a stored `groq` key
- **WHEN** the factory builds the pipeline
- **THEN** the stored key authenticates the client; with `GROQ_API_KEY` set, the environment value wins

#### Scenario: An unknown provider names the presets

- **GIVEN** a catalog entry with provider `not-a-provider`
- **WHEN** the factory builds the pipeline
- **THEN** it fails with an error naming the supported preset ids

#### Scenario: OpenCode Go routes each model to its native API

- **GIVEN** the `opencode-go` preset with four family URLs and a catalog model whose `apiFamily` is `openai-responses`
- **WHEN** the factory builds the pipeline
- **THEN** the Responses adapter serves it against the `openai-responses` base URL, not the chat-completions one

#### Scenario: Discovery supplies the family and the base URL

- **GIVEN** a discovered model whose metadata names `messages` and a base URL on the provider's host
- **WHEN** the catalog entry is created
- **THEN** its family resolves to `anthropic-messages` and its endpoint is the discovered base URL

## ADDED Requirements

### Requirement: Provider API families

A model SHALL resolve exactly one API family from the closed set `openai-chat`, `openai-responses`, `anthropic-messages` and `google`. Resolution precedence SHALL be: the profile's explicit `apiFamily` override; then discovery metadata; then the catalog entry's `apiFamily`; then the provider's single declared family. When the provider declares more than one family and no higher source answered, resolution SHALL fail with an error naming the provider, the model and the families to choose from. A family SHALL select the adapter: `openai-chat` the OpenAI chat client, `openai-responses` the OpenAI Responses client, `anthropic-messages` the Anthropic SDK adapter, `google` the Google.GenAI SDK's Microsoft.Extensions.AI client (streaming, function calling, system instructions, images, thinking with thought signatures). A model with no family and a provider with exactly one family SHALL behave as that family, so existing entries keep working.

#### Scenario: A profile override wins

- **GIVEN** a model whose catalog entry says `openai-chat` and a profile that says `openai-responses`
- **WHEN** the pipeline is built
- **THEN** the Responses adapter is used

#### Scenario: An ambiguous provider fails loudly

- **GIVEN** a provider with `openai-chat` and `anthropic-messages` and a model that declares no family
- **WHEN** the pipeline is built
- **THEN** it fails with an error naming both families and how to choose

#### Scenario: An unknown family is refused by name

- **GIVEN** a catalog entry with `apiFamily: openai-realtime`
- **WHEN** the catalog is loaded
- **THEN** loading fails with an error naming the family and the supported set

#### Scenario: The Google family uses the native client

- **GIVEN** a model whose family is `google`
- **WHEN** the pipeline is built
- **THEN** the Google.GenAI `AsIChatClient` client is used against the Google family base URL
- **AND** a streamed tool call preserves the thought signature in returned reasoning content

### Requirement: Provider request headers

Every provider request SHALL identify the client with `User-Agent: lunate/<informational version>`. A provider's declared headers SHALL be applied to every family's requests and to discovery requests, through one shared HTTP handler rather than per-adapter code. A header value SHALL be a literal or a template over `{session.id}`, `{run.id}` and `{lunate.version}`, substituted at request time. A template that cannot be resolved — a `{session.id}` header on a call with no session — SHALL fail with an actionable error instead of sending an empty or partial value. Protocol headers set by an SDK (`anthropic-version`, `Authorization`) SHALL be left intact unless the provider declares the same name. The credential itself SHALL be applied only by the provider's declared placement — `Authorization: Bearer` by default, a named header, or a named query parameter — after removing any credential header the SDK added for that provider, so the key appears exactly once on the wire and never in a header template.

#### Scenario: Every request identifies Lunate

- **GIVEN** any configured provider
- **WHEN** a chat, responses, messages, Google or discovery request is sent
- **THEN** it carries `User-Agent: lunate/<version>`

#### Scenario: The session header follows the session

- **GIVEN** a provider header `x-opencode-session: {session.id}`
- **WHEN** a parent session and a child session each send a request
- **THEN** each request carries its own session id

#### Scenario: A missing session id fails

- **GIVEN** a provider header that templates `{session.id}` and a call created without a session
- **WHEN** the request is about to be sent
- **THEN** it fails with an error naming the header and the placeholder

#### Scenario: Static attribution headers are sent

- **GIVEN** the `openrouter` preset with `X-Title: Lunate`
- **WHEN** a request is sent
- **THEN** the header is present, and the provider's own headers win over an SDK default of the same name

#### Scenario: The default placement is bearer

- **GIVEN** a provider with no `credentialPlacement`
- **WHEN** a request is sent
- **THEN** it carries `Authorization: Bearer <credential>` exactly once

#### Scenario: A query-parameter credential is placed once

- **GIVEN** the `google` preset with a query placement named `key`
- **WHEN** a request is sent
- **THEN** the key appears in the query string and in no header

#### Scenario: An SDK credential header is removed first

- **GIVEN** an SDK that adds `x-goog-api-key` and a provider whose placement is a bearer header
- **WHEN** the request is sent
- **THEN** the SDK header is absent and the bearer header carries the key
