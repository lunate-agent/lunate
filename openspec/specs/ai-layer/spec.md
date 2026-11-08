# ai-layer Specification

## Purpose
Model access for Lunate: the factory that builds the correctly ordered `IChatClient` pipeline (OpenTelemetry → logging → accumulator → recorder → provider), the model catalog that merges built-in and user definitions without code changes, and the layer rules that keep tool invocation and retries out of model access.

## Requirements

### Requirement: Chat-client factory pipeline order
`IChatClientFactory.Create` SHALL build the pipeline in the fixed order, outermost first: OpenTelemetry (opt-in) → logging → accumulator → recorder slot → provider. Replay will replace recorder + provider so tests still pass through the telemetry and logging layers.

#### Scenario: Pipeline order is fixed
- **GIVEN** a factory configured with a stub provider client
- **WHEN** a pipeline is created and a request flows through it
- **THEN** the telemetry, logging, accumulator, recorder and provider layers observe the request in exactly that order

#### Scenario: FunctionInvokingChatClient is never used
- **GIVEN** any factory configuration
- **WHEN** the pipeline is built
- **THEN** no `FunctionInvokingChatClient` or `AIFunctionFactory` appears anywhere in it

### Requirement: Model catalog merge
`ModelCatalog` SHALL merge the embedded `models.json` with `~/.lunate/models.json`; user entries SHALL override built-ins by id; adding a model SHALL never require a code change. A model MAY declare its reasoning style with `reasoning`: `"effort"` (OpenAI-compatible reasoning effort), `"budget"` (Anthropic extended thinking) or `"inline"` (the model embeds ` thinking…</think>` in its text stream); a model without the field declares no reasoning support and behaves exactly as before. A model MAY declare `supportsVision` (boolean, default `false`); `ModelInfo.SupportsVision` SHALL expose it and a model without the field SHALL report no vision support. A user entry SHALL be able to turn the flag on or off for an overridden built-in.

#### Scenario: User override wins
- **GIVEN** a built-in model and a user file redefining the same id
- **WHEN** the catalog is loaded
- **THEN** the user definition is used

#### Scenario: New model without code changes
- **GIVEN** a user `models.json` with a new id
- **WHEN** the catalog is loaded
- **THEN** the model is available

#### Scenario: Duplicate id within one file fails
- **GIVEN** a `models.json` containing the same id twice
- **WHEN** the catalog is loaded
- **THEN** loading fails with an error naming the id

#### Scenario: A model declaring its reasoning style

- **GIVEN** a `models.json` entry with `"reasoning": "effort"`
- **WHEN** the catalog is loaded
- **THEN** the model's reasoning style is available to the factory, and a model without the field reports none

#### Scenario: A schema 1 catalog still loads

- **GIVEN** a user `models.json` with `schemaVersion: 1` and a model entry without pricing
- **WHEN** the catalog is loaded
- **THEN** the model is available with no pricing, no maximum output, vision off, and its own id as the wire id

#### Scenario: The managed cache merges below the user file

- **GIVEN** an embedded entry, a managed cache entry with the same id, and a user entry with that id
- **WHEN** the catalog is loaded
- **THEN** the user entry wins, and an id present only in the managed cache is available

#### Scenario: Vision is declared per model

- **GIVEN** a `models.json` entry with `"supportsVision": true` and one without the field
- **WHEN** the catalog is loaded
- **THEN** the first reports vision support and the second reports none

#### Scenario: A user override changes the vision flag

- **GIVEN** a built-in model with `supportsVision` and a user entry redefining it with the opposite value
- **WHEN** the catalog is loaded
- **THEN** the user value is reported

### Requirement: Layer reuse and retry boundary
`Lunate.Ai` SHALL reuse `UseLogging` and `UseOpenTelemetry` (opt-in) instead of bespoke middleware, SHALL use `AIJsonUtilities` options for serializing Microsoft.Extensions.AI types where serialization exists, and SHALL NOT implement retries — the loop owns them and surfaces them as events.

#### Scenario: Retries are absent from the layer
- **GIVEN** the factory pipeline
- **WHEN** the provider throws a transient error
- **THEN** the exception surfaces unchanged (no retry inside `Lunate.Ai`)

### Requirement: Recording and replay of provider streams
`Lunate.Ai` SHALL record provider exchanges as JSONL when `LUNATE_RECORD=1` for both call styles: a streamed exchange (`GetStreamingResponseAsync`) records its update sequence, and an aggregated exchange (`GetResponseAsync`) records the response as its update sequence so replay serves both call styles from one exchange. `Lunate.Ai` SHALL replay fixtures deterministically: `ReplayChatClient` answers requests from the recorded exchanges in order, verifying each request digest, with actionable errors on mismatch or exhaustion. Fixtures SHALL serialize Microsoft.Extensions.AI types with `AIJsonUtilities` options and replay SHALL NOT require API keys or network access. When `LUNATE_REPLAY` names a fixture path, the factory's standard resolution path SHALL wire `ReplayChatClient` for it without resolving a credential or constructing a provider client, so a session replays with no key and no network; the explicit-credential overload (`Create(model, apiKey)`) SHALL NOT replay, and an explicit recording request takes precedence when both are set. When `LUNATE_RECORD=1` is set without an explicit path, recordings SHALL default under the user's home directory (`~/.lunate/recordings/`), never inside the current working directory. The request digest SHALL cover the messages, the model id and the tool names; tool descriptions and schemas are deliberately outside the digest, so presentation changes do not invalidate a fixture (revisit if it bites). Scripted fixtures SHALL be marked with the model id `synthetic`; live recordings carry the real model id. For the digest, every image `DataContent` in a message SHALL be replaced by its media type and lowercase SHA-256 hex digest, so the image identity is covered deliberately while base64 bytes never enter the digest input or a fixture.

#### Scenario: Record then replay is identical
- **GIVEN** a provider stream recorded to a fixture
- **WHEN** `ReplayChatClient` serves the same request
- **THEN** the replayed updates are identical to the recorded ones

#### Scenario: Aggregated exchange record then replay is identical
- **GIVEN** a provider response recorded via `GetResponseAsync`
- **WHEN** `ReplayChatClient` serves the same request
- **THEN** the replayed aggregated response matches the recorded response

#### Scenario: Request mismatch fails actionably
- **GIVEN** a fixture whose next exchange has digest A
- **WHEN** a request with digest B arrives
- **THEN** replay fails with an error naming the fixture, the exchange index and the digests

#### Scenario: Replay needs no API keys or network
- **GIVEN** any committed fixture
- **WHEN** it is replayed
- **THEN** no network call and no API key is required

#### Scenario: Factory wires the recorder
- **GIVEN** `LUNATE_RECORD=1` with a recording path
- **WHEN** the factory builds a pipeline
- **THEN** a recorder writes each exchange to the path while the stream passes through unchanged

#### Scenario: Default recordings are user-scoped
- **GIVEN** `LUNATE_RECORD=1` without `LUNATE_RECORD_PATH`
- **WHEN** the factory wires the recorder
- **THEN** the recording path is under `~/.lunate/recordings/`, not inside the current working directory

#### Scenario: Factory wires the replay client

- **GIVEN** `LUNATE_REPLAY` set to a committed fixture path
- **WHEN** the factory builds a pipeline
- **THEN** the pipeline serves the fixture deterministically without resolving a credential or constructing a provider client

#### Scenario: Recording wins over replay

- **GIVEN** `LUNATE_RECORD=1` and `LUNATE_REPLAY` both set
- **WHEN** the factory builds a pipeline
- **THEN** the recorder is wired, never the replay client

#### Scenario: The same image has the same digest

- **GIVEN** two requests whose messages carry identical image bytes in different encodings
- **WHEN** their request digests are computed
- **THEN** the digests match and neither input contains base64 image bytes

#### Scenario: Different pixels change the digest

- **GIVEN** two requests whose image bytes differ by one pixel
- **WHEN** their request digests are computed
- **THEN** the digests differ

### Requirement: Anthropic provider adapter
`Lunate.Ai` SHALL provide Anthropic support through the official Anthropic .NET package and its first-party Microsoft.Extensions.AI adapter, selected by `ModelInfo.Provider == "anthropic"` and authenticated from `ANTHROPIC_API_KEY`. The factory SHALL construct it with transport retries disabled so retries remain exclusively the loop's. When the model's reasoning style is `budget` and the session's thinking level is on, the factory SHALL enable extended thinking with the level's token budget and ensure the request carries `max_tokens` (clamped above the budget); signed thinking blocks returned by the model SHALL be round-tripped unchanged within the model's own tool-use loops, and this round-trip SHALL be covered by the live contract test.

#### Scenario: Provider selection
- **GIVEN** a model with provider "anthropic"
- **WHEN** the factory builds the pipeline
- **THEN** the Anthropic client serves it, and an unknown provider still fails with an actionable error naming the supported providers

#### Scenario: Contract coverage
- **GIVEN** `LUNATE_LIVE=1` and `ANTHROPIC_API_KEY` set
- **WHEN** the contract test runs a streamed round-trip
- **THEN** the consumer receives Microsoft.Extensions.AI updates (text, tool calls, usage); without the flag the test is skipped

#### Scenario: Thinking round-trips through a tool loop

- **GIVEN** `LUNATE_LIVE=1`, `ANTHROPIC_API_KEY` set and a model with reasoning style `budget`
- **WHEN** a streamed round-trip carries signed thinking blocks through a tool-use loop
- **THEN** the consumer receives the reasoning content and the follow-up request returns the blocks unchanged without an API rejection

### Requirement: Complete function calls at the consumer
The pipeline SHALL present function calls only in complete form: a `StreamAccumulator` above the recorder assembles streamed argument fragments per call id, so consumers never act on partial calls. A fragment is a `FunctionCallContent` whose `Arguments` contains exactly one entry under the reserved `$arguments` key holding a raw JSON fragment (a string, or a `JsonElement` of string kind); fragments merge by call id and are emitted as one parsed call in a synthesized update at stream end. When assembly fails, the synthesized call SHALL keep the concatenated raw fragment text under the reserved `$arguments` key and SHALL set its `Exception`. Recordings SHALL remain raw provider output.

#### Scenario: Fragmented arguments are assembled
- **GIVEN** a stream that delivers one function call's arguments in fragments
- **WHEN** it passes the accumulator
- **THEN** the consumer sees one complete call with the full arguments

#### Scenario: Already-complete calls pass unchanged
- **GIVEN** a stream whose function calls are already complete
- **WHEN** it passes the accumulator
- **THEN** the updates pass through unchanged

#### Scenario: Recordings stay raw
- **GIVEN** recording is enabled
- **WHEN** a fragmented stream is recorded
- **THEN** the fixture contains the raw fragments, and replay through the pipeline still assembles them

#### Scenario: Failed assembly preserves the raw text
- **GIVEN** a stream whose concatenated function-call arguments are not valid JSON
- **WHEN** it passes the accumulator
- **THEN** the emitted call carries the raw fragment text under the reserved `$arguments` key and its `Exception` is set

### Requirement: Local endpoints without a dummy key

The factory SHALL send a provider's environment API key only to the provider's default endpoint: when a model declares a custom endpoint without an explicit credential reference, both the OpenAI-compatible and the Anthropic adapter SHALL construct their clients with a placeholder credential instead of the environment key, and SHALL NOT fail for a missing key — so local servers work without dummy environment variables and real keys are never sent to third-party endpoints. A model that spells out the provider's own default URL counts as a custom endpoint. A model MAY declare an explicit credential reference (`"auth": "<name>"` in `models.json`), the deliberate opt-in that resolves that named key from the user's auth store and sends it to the declared endpoint; a reference that cannot be resolved SHALL fail with an error naming the key. For models without a reference: default endpoints resolve the provider credential from the environment first, then the auth store, and fail with an actionable error when neither yields a key.

#### Scenario: Custom endpoint without a key

- **GIVEN** a model with a custom endpoint and no `OPENAI_API_KEY`
- **WHEN** the factory builds the pipeline
- **THEN** no error is raised and requests target the custom endpoint

#### Scenario: Custom endpoint never receives the environment key

- **GIVEN** a model with a custom endpoint and `OPENAI_API_KEY` set
- **WHEN** the factory builds the pipeline
- **THEN** the client uses the placeholder credential, not the environment key

#### Scenario: Explicit reference sends the named key

- **GIVEN** a model with a custom endpoint and `"auth": "work"` resolving to a stored key
- **WHEN** the factory builds the pipeline
- **THEN** the client is constructed with that key for the declared endpoint

#### Scenario: Unresolvable reference names the key

- **GIVEN** a model with `"auth": "missing"` and no such stored key
- **WHEN** the factory builds the pipeline
- **THEN** the error names `missing` and mentions auth.json, and no secret value appears

#### Scenario: The Anthropic adapter is symmetric

- **GIVEN** an Anthropic model with a custom endpoint and no `ANTHROPIC_API_KEY`
- **WHEN** the factory builds the pipeline
- **THEN** no error is raised and the client uses the placeholder credential

#### Scenario: Default endpoint requires the environment key

- **GIVEN** a model without a custom endpoint and no key in the environment or auth store
- **WHEN** the factory builds the pipeline
- **THEN** it fails with an actionable error naming the variable and auth.json

### Requirement: Reasoning options from the session level

The factory SHALL resolve the model's API family once and choose the wire parameter from that resolved family: `anthropic-messages` maps the session's thinking level (`off` | `low` | `medium` | `high`) to an extended-thinking budget (2,048 / 8,192 / 32,768 tokens), and `openai-chat`, `openai-responses` and `google` map it to the OpenAI-compatible reasoning effort (low/medium/high). The model's declared reasoning style SHALL be metadata and fallback only: a declared `inline` sends no provider parameter (the model embeds the thinking text in its stream), a model with no declared style sends no reasoning parameters, and a declared `effort` or `budget` decides the wire shape only when the family cannot be resolved from the model or its provider. A level of `off` SHALL send no reasoning parameters for every family and style. The level-to-value mapping and the `/thinking` notice SHALL be culture-invariant, the notice SHALL name what the resolved family will send, and every case SHALL be covered by tests.

#### Scenario: Off sends nothing

- **GIVEN** any resolved API family and any declared reasoning style
- **WHEN** the thinking level is `off`
- **THEN** the request carries no reasoning parameters

#### Scenario: A declared budget on an OpenAI-compatible family still sends effort

- **GIVEN** a model declared `budget` whose resolved API family is `openai-chat`
- **WHEN** the thinking level is `high`
- **THEN** the request carries the high reasoning effort and no Anthropic extended-thinking parameters

#### Scenario: A declared effort on the Anthropic family still sends a budget

- **GIVEN** a model declared `effort` whose resolved API family is `anthropic-messages`
- **WHEN** the thinking level is `high`
- **THEN** the request carries a 32,768-token thinking budget, clamped below the request's max tokens

#### Scenario: Budget maps the level

- **GIVEN** a model whose resolved API family is `anthropic-messages`
- **WHEN** the level is `high`
- **THEN** the request carries a 32,768-token thinking budget, clamped below the request's max tokens

#### Scenario: Effort maps the level

- **GIVEN** a model whose resolved API family is `openai-chat` or `openai-responses`
- **WHEN** the level is `medium`
- **THEN** the request carries the medium reasoning effort

#### Scenario: Inline sends no parameter

- **GIVEN** a model declared `inline`
- **WHEN** the level is `high`
- **THEN** the request carries no reasoning parameters and the inline splitter is active

#### Scenario: A model without a declared style sends nothing

- **GIVEN** a model with no declared reasoning style
- **WHEN** the level is `high`
- **THEN** the request carries no reasoning parameters

#### Scenario: The declared style decides when the family cannot be resolved

- **GIVEN** a model with no API family annotation whose provider resolves to no single chat family
- **WHEN** the thinking level is `high`
- **THEN** the declared `effort` or `budget` style decides the wire shape

### Requirement: Reasoning content is provider-scoped

Reasoning content SHALL round-trip only to the provider that produced it: a request SHALL carry `TextReasoningContent` from history entries whose recorded model id equals the current call's model id — signed thinking blocks require being returned unchanged within a model's own tool-use loops — and SHALL strip reasoning content from entries recorded under any other model id. Stripping SHALL apply to the request copy only; the session file SHALL keep the content. Assistant reasoning content (including provider `ProtectedData`) SHALL persist inside the embedded `ChatMessage`, byte-identically re-serializable, with no schema change.

#### Scenario: Same-model loops keep the blocks

- **GIVEN** a history with reasoning content recorded under the current model
- **WHEN** a request is built
- **THEN** the reasoning content is included unchanged

#### Scenario: Cross-model requests strip

- **GIVEN** a history with reasoning content recorded under a different model id
- **WHEN** a request is built for the current model
- **THEN** the reasoning content is absent from the request and still present in the session file

#### Scenario: A reasoning session round-trips byte-identically

- **GIVEN** a session entry whose message carries reasoning content with `ProtectedData`
- **WHEN** the session is loaded and re-serialized
- **THEN** the bytes are identical

### Requirement: Anthropic prompt-cache breakpoints

For a model whose provider is `anthropic`, `Lunate.Ai` SHALL place prompt-cache breakpoints (`cache_control: {"type":"ephemeral"}`) on the last system block, the last tool declaration, and the last content block of the latest message. The latest-message breakpoint SHALL roll forward on every turn; the system and tool breakpoints SHALL stay on the same blocks. No request SHALL carry more than four breakpoints. The marker client SHALL count every `cache_control` marker present on the outgoing request — its own three plus any pre-existing markers on content blocks or tool declarations — and SHALL throw `InvalidOperationException` in all builds, before the request is sent, when the total exceeds four. For a model whose provider is not `anthropic` (OpenAI included) `Lunate.Ai` SHALL add no cache markers — those providers cache automatically.

Markers SHALL be request-local: the client SHALL NOT mutate the session history, the prompt sections or the registered tool declarations. The system prompt, tool declarations and unchanged prompt sections SHALL therefore lead consecutive requests byte-identically. The existing thinking parameters (`RawRepresentationFactory` for `budget` models) SHALL compose unchanged, and the adapter SHALL continue to build the wire messages from the `ChatMessage`s so signed thinking blocks and tool results round-trip exactly as before.

#### Scenario: Three breakpoints reach the wire

- **GIVEN** an Anthropic model, a system prompt, one tool and a two-message history
- **WHEN** a request passes through the factory and the real Anthropic adapter to a capturing handler
- **THEN** the JSON body carries `cache_control` on the last system block, on the last tool, and on the last content block of the latest message, and nowhere else

#### Scenario: The rolling breakpoint moves

- **GIVEN** two consecutive requests built from the same system prompt, tools and growing history
- **WHEN** both request bodies are captured
- **THEN** the system and tool bytes are identical, turn one's latest-message breakpoint is not repeated on turn two, and turn two carries its breakpoint on its own last content block

#### Scenario: At most four breakpoints

- **GIVEN** a request with several system blocks and several tools
- **WHEN** the marker client prepares it
- **THEN** the body contains at most four `cache_control` markers

#### Scenario: An over-limit request fails loudly

- **GIVEN** a request whose history already carries markers and whose system, tool and latest-message markers bring the total above four
- **WHEN** the marker client prepares it
- **THEN** it throws `InvalidOperationException` in every build and nothing reaches the provider

#### Scenario: OpenAI gets no markers

- **GIVEN** an OpenAI model
- **WHEN** a request is prepared
- **THEN** no `cache_control` field appears in the request and no marker client is inserted into the OpenAI pipeline

### Requirement: Cache-stable system prefix

The core system prompt SHALL be composed once per harness build and SHALL NOT be recomposed between turns of one harness. Consecutive turns of one harness SHALL send a byte-identical system message, tool list and unchanged prompt sections; only the rolling latest-message breakpoint and the appended history may differ. A wholesale reload (`/new`, resume, `/model`) MAY recompose the prompt, and an extension `RunStarting` edit MAY intentionally replace a section — both invalidate the cache by design, never silently mid-turn.

#### Scenario: Two turns keep the same prefix

- **GIVEN** a harness with a system prompt and a scripted client that records each request
- **WHEN** two consecutive runs execute without a reload
- **THEN** both requests carry the same system message bytes and the same tool declarations

#### Scenario: The date does not drift between turns

- **GIVEN** a harness composed on one date and run across a simulated date change
- **WHEN** the second run's request is inspected
- **THEN** its system prompt carries the date captured when the harness was built

### Requirement: Generated catalog snapshot

The built-in catalog SHALL be a deterministic, committed snapshot of models.dev for the preset providers, generated with catalog schema 2. It SHALL carry `source` metadata naming models.dev, its URL and the source SHA-256, and SHALL NOT carry a generation timestamp. Entries SHALL map `limit.context` to `contextWindow`, `limit.output` to `maxOutput`, `tool_call` to `supportsTools`, `attachment` to `supportsVision`, the four `cost` rates to `pricing`, and a true `reasoning` flag to `budget` for Anthropic and `effort` for OpenAI-compatible presets. `endpoint` SHALL be absent from generated entries; a preset supplies the base URL. Ordering SHALL be preset-config order and then model id ordinal.

A repo script SHALL regenerate `src/Lunate.Ai/models.json` through the same C# mapper the runtime uses; there SHALL NOT be a second mapping implementation. `lunate --update-catalog` SHALL fetch models.dev explicitly, validate the mapping, write the managed cache atomically, and leave `~/.lunate/models.json` untouched. It SHALL make no model call, and a failure SHALL leave the existing cache and catalog unchanged. With an output path (used by the script) it SHALL write the embedded-shaped file instead. No catalog download SHALL happen at startup.

#### Scenario: The snapshot is deterministic

- **GIVEN** the same models.dev response and the same preset configuration
- **WHEN** the generator runs twice
- **THEN** the two `models.json` files are byte-identical and carry the same source SHA-256

#### Scenario: Generated capabilities are mapped

- **GIVEN** a models.dev entry with `limit.context = 200000`, `limit.output = 64000`, `attachment = true`, `tool_call = true` and four costs
- **WHEN** it is mapped
- **THEN** the catalog entry carries that context window, maximum output, vision support, tool support and the four prices

#### Scenario: An explicit refresh writes the managed cache

- **GIVEN** no managed cache
- **WHEN** `lunate --update-catalog` succeeds
- **THEN** `~/.lunate/models.catalog.json` contains the schema-2 snapshot and `~/.lunate/models.json` is unchanged

#### Scenario: A failed refresh changes nothing

- **GIVEN** an existing managed cache and an unreachable models.dev
- **WHEN** `lunate --update-catalog` runs
- **THEN** it exits non-zero with an actionable message and the existing cache bytes are unchanged

#### Scenario: Startup never downloads

- **GIVEN** a machine with no network
- **WHEN** a session or print run starts
- **THEN** no catalog download is attempted and the embedded snapshot serves the catalog

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

### Requirement: Model id namespace

The four existing ids `gpt-4o-mini`, `gpt-4o`, `claude-sonnet-5-5` and `claude-opus-5-5` SHALL stay bare. Other native OpenAI and Anthropic ids SHALL be bare. Every non-native model id SHALL be `<provider>/<model>`, and the upstream id SHALL travel in `wireId` rather than be parsed from the catalog id. Lookup SHALL be exact on the catalog id. Discovery against a preset SHALL emit qualified ids with `wireId`; discovery against a URL SHALL keep the existing bare OpenAI-provider draft.

#### Scenario: The four existing ids resolve unchanged

- **GIVEN** a catalog generated from the current models.dev snapshot
- **WHEN** the four starter ids are looked up
- **THEN** each resolves to its native provider and keeps its bare id

#### Scenario: A non-native id is qualified

- **GIVEN** an OpenRouter model `anthropic/claude-sonnet-5-5`
- **WHEN** the snapshot is generated
- **THEN** its catalog id is `openrouter/anthropic/claude-sonnet-5-5` and its wire id is `anthropic/claude-sonnet-5-5`

#### Scenario: Discovery of a preset is qualified

- **GIVEN** `lunate --discover openrouter`
- **WHEN** the endpoint lists `anthropic/claude-sonnet-5-5`
- **THEN** the draft entry's id is `openrouter/anthropic/claude-sonnet-5-5`, its provider is `openrouter`, and it carries the wire id

### Requirement: Provider-scoped catalogue lookup

The catalogue SHALL offer a lookup scoped to one provider entry: the entry's own catalogue id (`<provider>/<model>`), then the exact catalogue id, then the single entry whose wire id the id is. A bare id that matches several entries' wire ids across the catalogue SHALL come back as the full candidate list rather than an exception, so the caller that knows the provider can resolve it or ask. `ModelCatalog.Find` SHALL keep its current behaviour, including its ambiguity refusal, for callers that have no provider context.

#### Scenario: The provider's own id wins

- **GIVEN** a catalogue that holds `moonshotai/kimi-k2.7-code`, `opencode/kimi-k2.7-code` and `opencode-go/kimi-k2.7-code`
- **WHEN** `kimi-k2.7-code` is resolved in the scope of a provider entry named `opencode`
- **THEN** the lookup answers `opencode/kimi-k2.7-code`

#### Scenario: An unknown id comes back with its candidates

- **GIVEN** a provider entry named `local` and the same catalogue
- **WHEN** `kimi-k2.7-code` is resolved in its scope
- **THEN** no entry is answered and the three wire-id candidates are returned

#### Scenario: The unscoped lookup still refuses an ambiguous bare id

- **GIVEN** the same catalogue
- **WHEN** `ModelCatalog.Find` is called with `kimi-k2.7-code`
- **THEN** it throws with every candidate named, exactly as before

### Requirement: Model pricing and cost computation

`ModelInfo` SHALL expose optional pricing rates per million tokens, a maximum output limit and vision support. A cost SHALL be computed as the sum of each lane's token count multiplied by its rate, divided by one million, with cached input removed from the uncached input lane. A record SHALL be fully priced only when every lane with a positive token count has a known rate; a missing rate SHALL make the record unpriced rather than zero. A model without pricing SHALL be unpriced. The computation SHALL use `decimal` and `CultureInfo.InvariantCulture` everywhere it formats a number.

#### Scenario: Cache lanes use their own rates

- **GIVEN** 1,000,000 input tokens of which 200,000 are cache reads and 100,000 are cache writes, plus 10,000 output tokens, with all four rates known
- **WHEN** the cost is computed
- **THEN** the uncached input, cache read, cache write and output lanes are each priced at their own rate

#### Scenario: An unknown rate for a used lane is unpriced

- **GIVEN** a positive cache-read count and no cache-read rate
- **WHEN** the cost is computed
- **THEN** the result is unpriced and no currency value is produced

#### Scenario: An unused lane needs no rate

- **GIVEN** input and output rates, a null cache rate and zero cache tokens
- **WHEN** the cost is computed
- **THEN** the record is fully priced

#### Scenario: A model without pricing is unpriced

- **GIVEN** a catalog entry with no `pricing`
- **WHEN** a cost is requested
- **THEN** the result is unpriced and the display shows tokens only

### Requirement: Provider templates and user instances

The twelve built-in provider presets SHALL be provider-kind templates that supply adapter and default configuration, not a closed set of provider instances. The template kinds SHALL be `openai`, `anthropic`, `openrouter`, `deepseek`, `moonshotai`, `zai`, `opencode`, `opencode-go`, `groq`, `togetherai`, `xai`, and `google`. The provider resolver SHALL accept multiple named user instances of one kind and use each instance's declared base URL, credential reference, headers, and region. A provider instance SHALL use the adapter selected by its template kind; changing its endpoint SHALL NOT silently change its adapter.

#### Scenario: Two providers use one kind independently

- **GIVEN** two provider instances of kind `openai` with different base URLs and credential references
- **WHEN** each model profile is resolved
- **THEN** each client uses only its named provider instance's endpoint and credential

#### Scenario: Preset templates remain usable without instances

- **GIVEN** the built-in provider templates and no user-created instance for one template
- **WHEN** a legacy preset model resolves
- **THEN** it continues to use the template's existing adapter and default configuration

### Requirement: Model capability metadata

Each model descriptor SHALL carry an extensible capability set with API names (`chat`, `decision`, `image-generation`, `embeddings`, and future strings), input modalities, output modalities, and features (`tools`, `structured-output`, `reasoning`, and future strings). These names SHALL be strings, not a closed enum. Each category MAY be unknown; an omitted category SHALL NOT be interpreted as unsupported. Capability facts SHALL layer in this order: catalog, provider discovery, then the user's profile override. A later source SHALL replace only categories it explicitly supplies; omitted categories retain the previous fact or remain unknown.

#### Scenario: A future API needs no enum change

- **GIVEN** a catalog model descriptor that includes API `embeddings`
- **WHEN** the descriptor is loaded
- **THEN** the API name is retained without changing a closed enum

#### Scenario: Capability sources layer in order

- **GIVEN** catalog, provider-discovery and user-profile facts for the same capability category
- **WHEN** the profile resolves
- **THEN** the user-profile fact overrides discovery, discovery overrides catalog, and omitted categories remain inherited or unknown

#### Scenario: Unknown capability is allowed

- **GIVEN** a model whose capability category is unknown from every source
- **WHEN** a usage profile resolves
- **THEN** the host allows it rather than inferring that the capability is absent

#### Scenario: A chat model can serve a user-selected decision usage

- **GIVEN** a user assigns a chat model with known `structured-output` feature to decision
- **WHEN** the profile resolves
- **THEN** it is accepted and the request method is labelled `adapter`

#### Scenario: API metadata labels but does not gate decision use

- **GIVEN** a user-selected model with API `chat` and the required `structured-output` feature
- **WHEN** it serves decision usage
- **THEN** it is labelled `adapter` and is not rejected by its API name

#### Scenario: Image input is checked only when an image is attached

- **GIVEN** an image attachment and a chat model whose known input modalities exclude `image`
- **WHEN** the request is prepared
- **THEN** it is refused before sending with a notice naming the model and saying to switch profile or remove the image

#### Scenario: Unknown image input is attempted and provider rejection is translated

- **GIVEN** an image attachment and a chat model whose input modalities are unknown
- **WHEN** the request is sent and the provider rejects the image
- **THEN** the user receives the same clear model-named switch-profile-or-remove-image notice, not a raw provider error

### Requirement: Profile options are validated against resolved models

The model layer SHALL validate each profile option against the resolved catalog model and provider before constructing a client. It SHALL reject an unsupported setting by its field name and profile name, rather than silently removing it. The effective context window SHALL use a profile override only after validation, and otherwise SHALL remain the catalog/provider value.

#### Scenario: Unsupported option fails model resolution

- **GIVEN** a profile with an option unsupported by its selected model
- **WHEN** a session resolves its profiles
- **THEN** resolution fails with the option and profile names before a client request

#### Scenario: Context override is explicit

- **GIVEN** a profile with a valid context-window override
- **WHEN** the client model metadata is composed
- **THEN** the profile's validated window is effective and the catalog entry remains unchanged

### Requirement: Provider API families

A model SHALL resolve exactly one API family from the closed set `openai-chat`, `openai-responses`, `anthropic-messages`, `google` and `systemone`. Resolution precedence SHALL be: the profile's explicit `apiFamily` override; then discovery metadata; then the catalog entry's `apiFamily`; then the provider's single declared family. When the provider declares more than one family and no higher source answered, resolution SHALL fail with an error naming the provider, the model and the families to choose from. A family SHALL select the adapter: `openai-chat` the OpenAI chat client, `openai-responses` the OpenAI Responses client, `anthropic-messages` the Anthropic SDK adapter, `google` the Google.GenAI SDK's Microsoft.Extensions.AI client (streaming, function calling, system instructions, images, thinking with thought signatures), and `systemone` the native System One decision transport rather than a chat client. A model with no family and a provider with exactly one family SHALL behave as that family, so existing entries keep working.

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

#### Scenario: A decision-only family is refused by the chat client factory

- **GIVEN** a model that declares the decision-only `systemone` family
- **WHEN** the chat client factory is asked to build its client
- **THEN** construction fails with an error naming the family and the families that have clients, and no chat client is built

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

### Requirement: A custom entry's profile model is checked by family

Profile resolution SHALL treat a provider entry whose name is not a provider preset id as a custom entry, because such an entry's kind names only the adapter that speaks its API family's wire. When a profile on a custom entry names a model the catalogue holds under another provider, the model it resolves SHALL be the entry's own model: catalogue id `<provider id>/<wire id>`, the entry's kind as provider, and the wire id as the id the endpoint is sent. A model the catalogue already holds under the entry's own kind SHALL keep its catalogue entry unchanged, so a preset instance and a custom endpoint's own catalogue entry resolve exactly as before.

A matched catalogue entry under a different provider SHALL contribute model facts only — context window, tool and vision support, reasoning style, maximum output, capabilities, declared family and wire id. It SHALL NOT contribute pricing: the rates of the provider that bills are not the rates of this endpoint. When several catalogue entries hold a bare id in the entry's family and none of them is the entry's own id, resolution SHALL take facts from none of them, because none of them is the model this endpoint serves.

The check that the model belongs to the entry SHALL compare API families, not adapter kinds: the matched catalogue entry's declared `apiFamily`, or its provider preset's default family when it declares none, SHALL be one of the families the entry serves. A mismatch SHALL be refused, naming the entry, the families it serves and the family the model speaks. A profile that declares the family itself is the user's explicit routing intent, and the entry's family URL is what must exist for it.

A preset entry, and a model the catalogue holds under the entry's own kind, SHALL keep the existing kind check and ambiguity refusal unchanged.

#### Scenario: A custom endpoint naming the vendor's own model keeps the catalogue entry

- **GIVEN** a custom entry `local` of kind `openai` and a profile naming it with model `gpt-4o`, whose catalogue entry belongs to `openai`
- **WHEN** the profile resolves
- **THEN** it resolves the catalogue entry `gpt-4o`, its provider is `openai`, and the entry's endpoint applies

#### Scenario: The reported case resolves through the entry

- **GIVEN** a custom OpenAI-compatible entry `local` and the shipped catalogue, in which `kimi-k2` is `opencode/kimi-k2`'s wire id
- **WHEN** a profile naming `local` with model `kimi-k2` resolves
- **THEN** it resolves as `local/kimi-k2`, its provider is `openai`, its wire id is `kimi-k2`, and its context window is the catalogue entry's

#### Scenario: Selected facts come across, pricing does not

- **GIVEN** the same custom entry and catalogue entry, whose pricing and maximum output are recorded
- **WHEN** the profile resolves
- **THEN** the resolved model carries the maximum output, capabilities and reasoning style, and carries no pricing

#### Scenario: A model of another family is still refused

- **GIVEN** a custom entry `local` of kind `openai` and the catalogue entry `claude-sonnet-4-5` of provider `anthropic`
- **WHEN** a profile naming `local` with model `claude-sonnet-4-5` resolves
- **THEN** it is refused with a message naming `local`, `openai-chat` and `anthropic-messages`

#### Scenario: Several catalogue entries in the family contribute no facts

- **GIVEN** a custom entry `local` and a bare id that three catalogue entries hold, all of them in `openai-chat`
- **WHEN** a profile naming `local` with that id resolves
- **THEN** it resolves through the entry with unknown limits instead of failing as ambiguous

#### Scenario: A preset entry keeps the kind check

- **GIVEN** a `moonshotai` entry and a profile naming it with a model whose catalogue entry belongs to `openai`
- **WHEN** the profile resolves
- **THEN** it is refused exactly as before, naming the entry's kind and the model's provider
