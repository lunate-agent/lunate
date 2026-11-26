## MODIFIED Requirements

### Requirement: The inherited policy strips Lunate's own credentials

`Inherited` SHALL remove, from the environment it inherits, every variable Lunate resolves as a
provider credential: each built-in provider preset's key variable and key alias, each configured
custom provider's `env:` credential reference, and each configured web search backend's `env:`
credential reference. This strip SHALL remove exactly those names, whether or not their shape
matches the credential-shape rule, and no others. A credential name declared by the caller SHALL
also be removed when it arrives with a declared value, so a spawn site cannot re-introduce a strip by
accident.

#### Scenario: A provider key does not reach the tool child

- **GIVEN** a parent environment holding `OPENAI_API_KEY` and a built-in alias such as `GEMINI_API_KEY`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** neither variable is present, while `PATH` and an unrelated user variable are

#### Scenario: A custom provider's env reference is stripped too

- **GIVEN** a configured custom provider whose credential is `env:MY_GATEWAY_CRED`
- **WHEN** a tool child starts after that provider was resolved
- **THEN** `MY_GATEWAY_CRED` is not in the child's environment, although its name matches no
  credential word

#### Scenario: A web search backend's env reference is stripped too

- **GIVEN** a configured search backend whose credential reference is `env:MY_SEARCH_CRED`
- **WHEN** a tool child starts after the session composed its tools
- **THEN** `MY_SEARCH_CRED` is not in the child's environment, although its name matches no
  credential word

#### Scenario: The strip is a name list, not a pattern match

- **GIVEN** a parent variable named close to a provider's own name but not resolved by Lunate, such as `OPENAI_BASE_URL`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** that variable survives — this strip removes exactly the names Lunate resolved, and the
  separate credential-shape rule does not match it either
