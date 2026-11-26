# Agent Config Specification Delta

## MODIFIED Requirements

### Requirement: Settings file and precedence

The agent SHALL read user settings from `~/.lunate/settings.json` (`schemaVersion: 2`; schema 1
SHALL remain readable as the same settings without favourites): optional `model` (default model id),
`approval` (`ask` — default — non-read tools require approval; `auto-edit` — file writes and edits
are approved automatically, commands still require approval), `verify` (`block` — default; `warn`;
`off`), and `output.toolResultLimit` (default 30,000 characters), and `startupHeader` (default
`true`; `false` suppresses the interactive welcome header). It SHALL also read optional `favorites`,
an ordered array of at most eight unique non-empty model ids, and optional `tools.extra`, an array of
optional search tool names
(`grep`, `find`, `ls`, `semantic_search`; default empty), where an unknown name or a name
colliding with a core tool SHALL be rejected with the accepted values. It SHALL also read `notify`, a
boolean that enables desktop notifications for long interactive turns (default `false`; the
`--notify` launch flag enables it for one run), and `notifyMinSeconds`, a non-negative integer
threshold in seconds (default 10; `0` removes the threshold). The value `yolo` SHALL be
rejected: it exists only as a per-run flag, never a saved setting. A missing file SHALL yield
defaults; a present file SHALL be validated with all problems collected into one actionable error.
Environment variables (`LUNATE_MODEL`, `LUNATE_APPROVAL`, `LUNATE_TOOL_OUTPUT_LIMIT`,
`LUNATE_STARTUP_HEADER`) SHALL override their corresponding file values and SHALL be validated
identically, with one documented alias: where the file takes a JSON boolean, the environment
variable also accepts `1` and `0` (case-insensitive) beside `true` and `false`, and a rejected value
SHALL name the accepted literals. `verify`, `notify` and `notifyMinSeconds` are settings-file values
without an environment-variable override. The resolved output limit SHALL govern tool-output
truncation in the loop.

A settings write SHALL be read-modify-write over the existing JSON: unrelated keys and values SHALL
survive, favourites SHALL be validated through the same rules, and the replacement SHALL be atomic.
A failed write SHALL leave the old file intact. A write SHALL emit `schemaVersion: 2`.

#### Scenario: Missing file yields defaults

- **GIVEN** no settings file
- **WHEN** settings are resolved
- **THEN** the model is unset, approval is `ask`, `verify` is `block`, the tool result limit is
  30,000, favourites are empty, notifications are off and the threshold is 10 seconds

#### Scenario: Environment overrides the file

- **GIVEN** a settings file setting `approval` to `ask`
- **WHEN** `LUNATE_APPROVAL=auto-edit` is set and settings are resolved
- **THEN** the resolved approval is `auto-edit`

#### Scenario: Invalid file reports every problem once

- **GIVEN** a settings file with an unknown key and an out-of-range limit
- **WHEN** settings are resolved
- **THEN** one error names both problems and no partial settings are applied

#### Scenario: Yolo is not a saved setting

- **GIVEN** a settings file with `"approval": "yolo"`
- **WHEN** settings are resolved
- **THEN** one error says yolo is per-run only, and no partial settings are applied

#### Scenario: Missing file enables the startup header

- **GIVEN** no settings file and no environment override
- **WHEN** settings are resolved
- **THEN** the startup header is enabled

#### Scenario: Startup header disabled by file and by environment

- **GIVEN** a settings file setting `startupHeader` to `false`
- **WHEN** settings are resolved with no environment override
- **THEN** the resolved startup header is disabled; and with `LUNATE_STARTUP_HEADER=1` set instead,
  it is enabled again

#### Scenario: Missing file defaults the tool details to collapsed

- **GIVEN** no settings file and no environment override
- **WHEN** settings are resolved
- **THEN** the tool details are `collapsed`

#### Scenario: The file expands the tool details

- **GIVEN** a settings file setting `toolDetails` to `expanded`
- **WHEN** settings are resolved with no environment override
- **THEN** the resolved tool details are `expanded`

#### Scenario: The environment overrides the tool details

- **GIVEN** a settings file setting `toolDetails` to `expanded`
- **WHEN** `LUNATE_TOOL_DETAILS=collapsed` is set and settings are resolved
- **THEN** the resolved tool details are `collapsed`; and with no settings file but
  `LUNATE_TOOL_DETAILS=expanded`, they are `expanded`

#### Scenario: An invalid tool details value is collected with the other problems

- **GIVEN** a settings file setting `toolDetails` to `Expanded` and an unknown key
- **WHEN** settings are resolved
- **THEN** one error names both problems and the accepted `collapsed`/`expanded` literals, and no
  partial settings are applied

#### Scenario: An invalid tool details environment value names its variable

- **GIVEN** `LUNATE_TOOL_DETAILS=full` and no settings file
- **WHEN** settings are resolved
- **THEN** one error names `LUNATE_TOOL_DETAILS` with the accepted `collapsed`/`expanded` literals
  and no partial settings are applied

#### Scenario: A schema 1 settings file still loads

- **GIVEN** a settings file with `schemaVersion: 1` and no favourites
- **WHEN** settings are resolved
- **THEN** every existing setting keeps its meaning, `verify` defaults to `block`, and favourites
  are empty

#### Scenario: Favourites are ordered and bounded

- **GIVEN** a settings file with nine favourites, or a duplicate id
- **WHEN** settings are resolved
- **THEN** one error refuses the list and names the eight-entry/unique rule, and no partial settings
  are applied

#### Scenario: A write preserves unrelated settings

- **GIVEN** a settings file carrying a hand-written `thinking` value
- **WHEN** a favourite is added
- **THEN** the file still has that value, gains the favourite, and carries schema 2

#### Scenario: Verify accepts only its three modes

- **GIVEN** a settings file with `verify` set to a value other than `block`, `warn` or `off`
- **WHEN** settings are resolved
- **THEN** validation reports the `verify` field and accepted values, with no partial settings
  applied

#### Scenario: Warn and off are settings-file values

- **GIVEN** a settings file with `verify` set to `warn` or `off`
- **WHEN** settings are resolved without an environment override
- **THEN** the resolved verification mode is the configured value

#### Scenario: Notification settings resolve and validate

- **GIVEN** a settings file with `notify` set to `true` and `notifyMinSeconds` set to `0`
- **WHEN** settings are resolved
- **THEN** notifications are enabled with no duration threshold; and a negative or non-integer
  `notifyMinSeconds` is refused with the accepted value shape and no partial settings are applied


#### Scenario: semantic_search is an accepted optional name

- **GIVEN** `tools.extra` set to `["semantic_search"]`
- **WHEN** settings are resolved
- **THEN** the list is accepted exactly as `["grep"]` would be

#### Scenario: An optional entry cannot name a core tool

- **GIVEN** `tools.extra` set to `["read"]`
- **WHEN** settings are resolved
- **THEN** it is refused naming the core tool and every accepted value, including
  `semantic_search`
