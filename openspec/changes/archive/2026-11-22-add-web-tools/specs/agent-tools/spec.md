## ADDED Requirements

### Requirement: Core web tools

`Lunate.Coding`'s core tool set SHALL include `web_search` and `web_fetch` beside `read`, `write`,
`edit`, `bash` and `todo`: every harness built by the factory SHALL register both, both SHALL declare
`ToolRisk.Execute`, their schemas SHALL be hand-written JSON like every other tool, and the run's
tool filter SHALL select or drop them by name exactly as it does for `bash`. The optional
`tools.extra` list SHALL keep refusing a core name, now including the two web names, so an optional
entry can never shadow a web tool.

#### Scenario: The core set includes the web tools

- **GIVEN** a harness built without optional tools
- **WHEN** its registry is read
- **THEN** it holds `read`, `write`, `edit`, `bash`, `todo`, `web_search` and `web_fetch`, in that
  order, and both web tools declare `Execute` risk

#### Scenario: The tool filter can drop a web tool

- **GIVEN** a core registry and a run whose tool selection is `read` and `web_fetch`
- **WHEN** the filter is applied
- **THEN** only those two tools remain

#### Scenario: An optional entry cannot name a web tool

- **GIVEN** `tools.extra` set to `["web_search"]`
- **WHEN** the settings are validated or the harness is built
- **THEN** the entry is refused as a core tool name and no duplicate is registered
