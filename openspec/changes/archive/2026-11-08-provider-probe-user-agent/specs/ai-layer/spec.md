## MODIFIED Requirements

### Requirement: Provider request headers

Every provider request SHALL identify the client with `User-Agent: lunate/<informational version>`, the unauthenticated reachability probe behind `lunate /auth list` included. A provider's declared headers SHALL be applied to every family's requests and to discovery requests, through one shared HTTP handler rather than per-adapter code. A header value SHALL be a literal or a template over `{session.id}`, `{run.id}` and `{lunate.version}`, substituted at request time. A template that cannot be resolved — a `{session.id}` header on a call with no session — SHALL fail with an actionable error instead of sending an empty or partial value. Protocol headers set by an SDK (`anthropic-version`, `Authorization`) SHALL be left intact unless the provider declares the same name. The credential itself SHALL be applied only by the provider's declared placement — `Authorization: Bearer` by default, a named header, or a named query parameter — after removing any credential header the SDK added for that provider, so the key appears exactly once on the wire and never in a header template.

#### Scenario: Every request identifies Lunate

- **GIVEN** any configured provider
- **WHEN** a chat, responses, messages, Google, discovery or reachability-probe request is sent
- **THEN** it carries `User-Agent: lunate/<version>`

#### Scenario: The reachability probe stays unauthenticated

- **GIVEN** a provider entry with a stored credential and a declared header
- **WHEN** `lunate /auth list` probes the entry's base URL
- **THEN** the probe carries `User-Agent: lunate/<version>` and no credential and no declared provider header

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
