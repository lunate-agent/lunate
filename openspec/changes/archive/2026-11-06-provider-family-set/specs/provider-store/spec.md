## MODIFIED Requirements

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
