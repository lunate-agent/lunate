## ADDED Requirements

### Requirement: End-of-options marker

The launch arguments SHALL accept `--` as the end-of-options marker, in print mode and with
`--json` as in every other mode. Every token after the marker SHALL be a positional argument and
SHALL never be read as an option, so `lunate -p -- "--not-a-flag"` SHALL send the prompt
`--not-a-flag`. Only the first `--` SHALL be the marker; a later `--` SHALL be an ordinary positional
argument, and a token before the marker SHALL stay positional. A `--` with no prompt after it SHALL
be the usual missing-prompt usage error (exit 2), and a positional argument in interactive mode SHALL
stay the existing usage error. A token after the marker SHALL NOT reach the pre-dispatch scans for
`--acp` or `--no-extensions`, so a prompt that contains one of them cannot change the mode.

#### Scenario: A prompt that starts with a dash after the marker

- **GIVEN** `-p`, or `-p --json`, with `--` and then `--not-a-flag`
- **WHEN** the invocation is parsed
- **THEN** the prompt is exactly `--not-a-flag`, no usage error is raised, and the run sends it

#### Scenario: The marker without a prompt

- **GIVEN** `-p --` with no token after the marker
- **WHEN** the invocation is parsed
- **THEN** it exits 2 with the missing-prompt usage message on stderr and nothing on stdout

#### Scenario: A second marker is part of the prompt

- **GIVEN** `-p -- --`
- **WHEN** the invocation is parsed
- **THEN** the prompt is `--`

#### Scenario: A mode flag after the marker is a prompt

- **GIVEN** `-p -- "--acp"`
- **WHEN** the invocation runs
- **THEN** print mode runs with the prompt `--acp` instead of starting ACP mode
