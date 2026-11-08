## ADDED Requirements

### Requirement: Tool-environment pass-through setting

The settings file SHALL accept the user-scope key `toolEnvironment.passthrough`: an array of exact
environment variable names to admit into tool processes after the inherited policy's credential
strip. A missing key SHALL default to the empty list. Each entry SHALL be a non-empty variable name
(a value with `=` or a NUL is refused), duplicates SHALL be refused, and every bad entry SHALL be
collected into the one settings error rather than applied. The key SHALL be listed in
`settings-schema.json`, in the generated configuration reference, and SHALL never be accepted in a
project settings file.

#### Scenario: Missing key yields an empty pass-through list

- **GIVEN** a settings file without `toolEnvironment`
- **WHEN** the settings resolve
- **THEN** the resolved pass-through list is empty and no tool child receives more than the inherited policy allows

#### Scenario: Exact names resolve in order

- **GIVEN** a settings file with `toolEnvironment.passthrough` set to `["MY_PROJECT_TOKEN", "PROXY_URL"]`
- **WHEN** the settings resolve
- **THEN** the resolved list is those two names in that order

#### Scenario: A bad entry is refused with the key named

- **GIVEN** a settings file whose `toolEnvironment.passthrough` holds a `KEY=value` entry, an empty entry, or a duplicate
- **WHEN** the settings resolve
- **THEN** resolution fails with one error naming `toolEnvironment.passthrough`, and no settings are applied

#### Scenario: The setting is user scope only

- **GIVEN** a project settings file carrying `toolEnvironment.passthrough`
- **WHEN** the project settings merge
- **THEN** the merge refuses the key as user-only
