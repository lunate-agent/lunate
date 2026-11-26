## ADDED Requirements

### Requirement: Provider-native search opt-in setting

`tools.nativeSearch` SHALL be a boolean key in the user-scoped `tools` settings section, default
`false`, and SHALL be the explicit opt-in for provider-native web search: a search the provider runs
inside the chat request, which Lunate cannot approve per call. A non-boolean value SHALL be refused
at load, naming `tools.nativeSearch` and that it must be `true` or `false`, and applying no
settings. The key SHALL have no environment-variable override. It SHALL be user scope only, so a
project settings file carrying it is refused like every other key a project may not hold: a
repository SHALL NOT be able to switch on a search path the user cannot approve. The key SHALL
appear in the generated settings reference like every other accepted key.

#### Scenario: Native search is off when nothing is configured

- **GIVEN** a settings file without `tools.nativeSearch`
- **WHEN** settings resolve
- **THEN** the provider-native search opt-in is off

#### Scenario: The opt-in resolves

- **GIVEN** `tools.nativeSearch` set to `true`
- **WHEN** settings resolve
- **THEN** the provider-native search opt-in is on

#### Scenario: A non-boolean opt-in is refused by its key

- **GIVEN** `tools.nativeSearch` set to the string `"yes"`
- **WHEN** settings resolve
- **THEN** loading is refused naming `tools.nativeSearch` and that it must be `true` or `false`

#### Scenario: A project settings file cannot opt in

- **GIVEN** a project settings file carrying `tools.nativeSearch`
- **WHEN** settings resolve with that project file
- **THEN** the project file is refused and the user's opt-in is unchanged
