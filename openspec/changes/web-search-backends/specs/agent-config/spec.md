## ADDED Requirements

### Requirement: Web search backend setting

`tools.webSearch` SHALL be a user-scoped settings object selecting the local `web_search` backend,
with three keys: `backend` (`duckduckgo`, `searxng`, `brave` or `tavily`; default `duckduckgo`),
`url` (a SearXNG instance's base URL, absolute and https, or http only for a loopback host) and
`credential` (a credential reference in the provider form `auth:<name>`, `env:<VARIABLE>` or
`helper:<name>`). The credential value SHALL be a reference only: a literal secret is not a valid
value, and settings SHALL never carry a resolved credential.

Loading SHALL refuse, naming the offending key and the fix and applying no settings: an unknown key
under `tools.webSearch`, an unknown backend (naming the accepted values), a `url` with a backend
other than `searxng`, a missing `url` for `searxng`, a `url` that is not https or loopback http, a
`credential` with `duckduckgo`, a missing `credential` for `brave` or `tavily`, and a `credential`
that is not one of the three reference forms. The section SHALL be user scope only, so a project
settings file carrying it is refused like every other key a project may not hold: a repository SHALL
NOT be able to point the search at an endpoint of its choosing and spend the user's credential there.
The section SHALL have no environment-variable override, and it SHALL appear in the generated
settings reference like every other accepted key.

#### Scenario: The default is DuckDuckGo

- **GIVEN** a settings file without `tools.webSearch`
- **WHEN** settings resolve
- **THEN** the backend is `duckduckgo` with no URL and no credential reference

#### Scenario: A SearXNG section resolves its URL

- **GIVEN** `tools.webSearch` with `backend: "searxng"` and a loopback `url`
- **WHEN** settings resolve
- **THEN** the backend is `searxng`, the URL is the instance base URL, and no credential is required

#### Scenario: A Brave section keeps its reference unresolved

- **GIVEN** `tools.webSearch` with `backend: "brave"` and `credential: "auth:brave"`
- **WHEN** settings resolve
- **THEN** the credential value is still the reference `auth:brave` and no secret value is present

#### Scenario: An unknown backend names the accepted values

- **GIVEN** `tools.webSearch.backend` set to `bing`
- **WHEN** settings resolve
- **THEN** loading is refused naming `tools.webSearch.backend` and the four accepted backends

#### Scenario: Missing and irrelevant keys are refused by key

- **GIVEN** one settings file with `backend: "searxng"` and no `url`, another with `backend:
  "duckduckgo"` and a `url`, and a third with `backend: "brave"` and no `credential`
- **WHEN** each resolves
- **THEN** each is refused naming the offending `tools.webSearch` key and what it needs or must drop

#### Scenario: A literal secret is not a credential reference

- **GIVEN** `tools.webSearch.credential` holding an API key value instead of a reference
- **WHEN** settings resolve
- **THEN** loading is refused naming the key and the three accepted reference forms

#### Scenario: A project settings file cannot select a backend

- **GIVEN** a project settings file carrying `tools.webSearch`
- **WHEN** settings resolve with that project file
- **THEN** the project file is refused and the user's backend selection is unchanged
