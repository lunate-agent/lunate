## ADDED Requirements

### Requirement: Provider-native web search behind an explicit opt-in

A chat model SHALL declare provider-native search by carrying `web_search` in its capability
features, layered from the catalog, provider discovery and the user's profile override like every
other capability fact. When `tools.nativeSearch` is on **and** the chat model of the request declares
the `web_search` feature **and** the run's tool selection offers `web_search` (no selection, or a
selection naming it), the request SHALL declare the provider's built-in search — the Microsoft
Extensions.AI `HostedWebSearchTool` or an equivalent hosted marker — and SHALL NOT declare the local
`web_search` tool as a callable function. The local tool SHALL remain registered, as the core tool
set requires, but it SHALL not be offered to the model for that request.

Without the opt-in, or with a chat model whose capability facts do not declare `web_search`,
behaviour SHALL be unchanged: the local `web_search` tool is declared and no native search is
requested. A run's tool selection SHALL still govern the decision, so the native search is declared
only when the local tool would have been offered.

The native search SHALL remain part of the chat call. It SHALL NOT add a model usage, a `/cost` lane,
a session-format entry or a settings usage value; the chat call's usage and attribution SHALL be
unchanged, and the local `tools.webSearch` backend settings SHALL keep applying only when the local
tool runs. No network is required to prove the declaration: a stub chat client observes the request
options.

#### Scenario: The opt-in and a declaring model declare the provider's search

- **GIVEN** a chat model whose capability features include `web_search` and `tools.nativeSearch` set
  to `true`
- **WHEN** the harness builds the request
- **THEN** the request carries the provider's built-in search and declares no local `web_search`
  function

#### Scenario: Without the opt-in the local tool is offered

- **GIVEN** a chat model whose capability features include `web_search` and `tools.nativeSearch`
  unset
- **WHEN** the harness builds the request
- **THEN** the request declares the local `web_search` function and carries no built-in search

#### Scenario: A model that does not declare the feature keeps the local tool

- **GIVEN** a chat model whose capability features omit `web_search` and `tools.nativeSearch` set to
  `true`
- **WHEN** the harness builds the request
- **THEN** the request declares the local `web_search` function and carries no built-in search

#### Scenario: The tool selection still governs

- **GIVEN** a declaring model, the opt-in set, and a tool selection that does not name
  `web_search`
- **WHEN** the harness builds the request
- **THEN** the request carries neither the built-in search nor the local `web_search` function

#### Scenario: Billing and attribution stay on the chat call

- **GIVEN** a run on a declaring chat model with the opt-in set and a provider usage report
- **WHEN** the run finishes
- **THEN** the usage is attributed to the `chat` call as before, with no separate native-search
  usage, cost lane or session entry
