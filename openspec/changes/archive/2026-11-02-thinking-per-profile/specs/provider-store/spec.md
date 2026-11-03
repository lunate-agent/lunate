## MODIFIED Requirements

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

