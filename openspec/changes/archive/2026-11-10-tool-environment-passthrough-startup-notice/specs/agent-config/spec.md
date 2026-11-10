## ADDED Requirements

### Requirement: The startup names the admitted tool-environment variables

When the resolved `toolEnvironment.passthrough` list is non-empty, every session SHALL emit one
startup notice naming the admitted variables, in the configured order, before a tool child could
run. The interactive session, print mode and ACP mode SHALL behave the same way where they resolve
settings. The notice SHALL appear whether or not a listed name also collides with a Lunate
credential name; the existing one-per-startup collision warning SHALL remain a separate line and
SHALL NOT be the only signal that a variable is admitted. An empty list SHALL stay silent, so a
default install gains no startup line.

#### Scenario: A configured list is named at startup

- **GIVEN** resolved settings whose `toolEnvironment.passthrough` is `["MY_PROJECT_TOKEN", "PROXY_URL"]`
- **WHEN** the session starts through its normal settings resolution
- **THEN** one startup line names `MY_PROJECT_TOKEN` and `PROXY_URL` in that order, with no collision warning

#### Scenario: An empty list stays quiet

- **GIVEN** resolved settings without `toolEnvironment.passthrough`
- **WHEN** the session starts
- **THEN** no pass-through line is emitted

#### Scenario: The admission line is still emitted when a name collides

- **GIVEN** resolved settings whose `toolEnvironment.passthrough` is `["OPENAI_API_KEY"]`
- **WHEN** the session starts
- **THEN** the startup line names `OPENAI_API_KEY` as admitted, and the collision warning follows it once

#### Scenario: ACP mode names the admitted variables

- **GIVEN** an ACP session resolving settings whose `toolEnvironment.passthrough` is non-empty
- **WHEN** the session context is created
- **THEN** its startup notice names the admitted variables before the session runs a tool
