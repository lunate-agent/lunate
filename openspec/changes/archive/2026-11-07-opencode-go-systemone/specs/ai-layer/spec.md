## MODIFIED Requirements

### Requirement: Provider presets and routing

Lunate SHALL provide presets for `openai`, `anthropic`, `openrouter`, `deepseek`, `moonshotai`, `zai`, `opencode`, `opencode-go`, `groq`, `togetherai`, `xai` and `google`, defined in one embedded `providers.json`. A preset SHALL declare one base URL per API family it offers (at minimum its default family), an optional models/discovery URL, optional headers, and a key variable. `openai` and `anthropic` SHALL keep their native adapters for their default families; every other preset SHALL route each model through the adapter of that model's API family (see Provider API families). The provider call SHALL use `wireId` when present and the catalog id otherwise. A model with an explicit endpoint SHALL keep that endpoint, and a user-declared endpoint SHALL keep the placeholder-credential rule. An unknown provider SHALL fail with an actionable error listing the preset ids. The `opencode-go` preset SHALL additionally expose the `systemone` family at the OpenCode System One surface, keep the `x-opencode-session` header on every family, send the System One image header on the `systemone` family, and accept `OPENCODE_GO_API_KEY` as the alias of its key variable.

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

#### Scenario: OpenCode Go serves System One

- **GIVEN** the `opencode-go` preset and a catalog entry `opencode-go/jev-1.13` with `wireId` `jev-1.13` and `apiFamily` `systemone`
- **WHEN** the factory builds the client for a decision request
- **THEN** the call goes to the OpenCode System One surface with the session header and the image header, the credential resolves through `OPENCODE_GO_API_KEY` or `OPENCODE_API_KEY`, and the wire id is `jev-1.13`
