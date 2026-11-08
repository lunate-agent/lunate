## ADDED Requirements

### Requirement: The execution environment setting

The settings file SHALL accept an optional `environment` key holding `docker:<image>` or
`podman:<image>`, defaulting to unset (host execution). The key SHALL have user scope and SHALL be
read from the same settings file as `model` and `approval`. `--env` SHALL override it for the run.
A value with another engine, an empty image, or a shape the parser does not accept SHALL fail
settings resolution with one message naming the accepted shapes and the offending value; a partial
settings object SHALL NOT be applied. There SHALL be no environment-variable override for this key;
the flag is the per-run override. The key SHALL appear in the generated settings reference and its
schema entry SHALL carry the same description the store enforces.

#### Scenario: The settings key selects the environment
- **GIVEN** a settings file with `"environment": "podman:alpine"`
- **WHEN** settings resolve without `--env`
- **THEN** the resolved environment is `podman:alpine`

#### Scenario: A malformed settings value is refused whole
- **GIVEN** a settings file with `"environment": "docker:"`
- **WHEN** settings resolve
- **THEN** one error names the accepted shapes and no partial settings are applied
