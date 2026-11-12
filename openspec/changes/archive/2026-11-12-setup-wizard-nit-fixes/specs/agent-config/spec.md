## ADDED Requirements

### Requirement: The setup usage line lists every accepted value

The usage line `lunate setup` prints when a flag is refused SHALL name every value its flags accept:
`--thinking` SHALL be listed as `off|low|medium|high`, matching what the run accepts and what this
capability's setup command line requires. The same words SHALL be the refusal text for an unknown
level, so the usage line and the refusal cannot disagree about `off`.

#### Scenario: The usage line names off

- **GIVEN** a `lunate setup` invocation whose flag value is refused
- **WHEN** the command prints the usage line
- **THEN** the `--thinking` values read `off|low|medium|high`, and the same four words appear in the
  unknown-level refusal
