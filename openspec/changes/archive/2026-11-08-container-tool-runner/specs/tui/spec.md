## ADDED Requirements

### Requirement: The environment is named in the header and /context

When an execution environment is selected, the welcome header SHALL include the selected
`<engine>:<image>` as an `environment <engine>:<image>` segment, after the workspace/home and C#
workspace lines and before the counts; on the single-line ASCII form it SHALL use the ASCII
separator. When no environment is selected the header SHALL print no environment segment, so the
existing header bytes are unchanged. The `/context` notice SHALL include an
`environment: <engine>:<image> (<state>)` segment, where the state is `starting`, `ready`, or
`failed: <reason>`. The state SHALL reflect the session's environment object and SHALL NOT create a
container just to report it. Every value SHALL be plain text and culture-invariant.

#### Scenario: The header names the environment
- **GIVEN** a session started with `--env podman:alpine`
- **WHEN** the welcome header prints at width ≥ 80 with Unicode
- **THEN** the block contains a line `environment podman:alpine`

#### Scenario: No selection leaves the header unchanged
- **GIVEN** no `--env` and no `environment` setting
- **WHEN** the welcome header and `/context` render
- **THEN** neither contains an environment segment

#### Scenario: /context reports a failed start
- **GIVEN** a selected environment whose engine cannot start the container
- **WHEN** `/context` runs
- **THEN** its text contains `environment: <engine>:<image> (failed: <reason>)`
