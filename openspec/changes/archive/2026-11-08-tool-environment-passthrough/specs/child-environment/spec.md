## ADDED Requirements

### Requirement: The inherited policy admits an explicit pass-through list after the strip

`Inherited` SHALL re-admit, from the set it inherits, only the variable names in the tool-process
pass-through list, and SHALL apply that list **after** the credential strip so an explicitly named
variable wins over the deny for that exact name. Matching SHALL be by exact name — the platform's
environment-name comparison and nothing else — never a prefix, suffix or pattern. A name the parent
does not hold SHALL stay absent from the child, and a credential name the user did not list SHALL
still be stripped.

The tool child's pass-through list SHALL be read where its policy is built
(`ChildProcess.BuildEnvironment`), never at a spawn site, and SHALL be handed only to the
`Inherited` policy — the `Scrubbed` policy receives only its own declared names, because extension
hosts never receive the inherited set at all.

#### Scenario: A passed-through name reaches the POSIX tool child

- **GIVEN** a parent environment holding `MY_PROJECT_TOKEN` and a tool-process pass-through list naming it
- **WHEN** the POSIX child environment is built and the real child runs
- **THEN** the child reads `MY_PROJECT_TOKEN` from its own environment

#### Scenario: A passed-through name reaches the Windows tool child

- **GIVEN** a parent environment holding `MY_PROJECT_TOKEN` and a tool-process pass-through list naming it
- **WHEN** the Windows child environment is built and the real child runs
- **THEN** the child reads `MY_PROJECT_TOKEN` from its own environment

#### Scenario: A passed-through credential name is an explicit user act

- **GIVEN** a parent environment holding `OPENAI_API_KEY` and a pass-through list naming it
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** `OPENAI_API_KEY` reaches the child, and startup prints one warning naming the variable

#### Scenario: A credential name not in the list stays stripped

- **GIVEN** a parent environment holding `OPENAI_API_KEY` and `GEMINI_API_KEY` and a pass-through list naming only `OPENAI_API_KEY`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** `OPENAI_API_KEY` arrives and `GEMINI_API_KEY` does not

#### Scenario: The pass-through is an exact name, never a pattern

- **GIVEN** a parent environment holding `OPENAI_API_KEY` and `OPENAI_API_KEY_BACKUP`, and a pass-through list naming only `OPENAI_API_KEY_BACKUP`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** `OPENAI_API_KEY_BACKUP` arrives and `OPENAI_API_KEY` does not; a listed `OPENAI_*` matches nothing

#### Scenario: A listed name the parent does not hold stays absent

- **GIVEN** a parent environment without `MY_PROJECT_TOKEN` and a pass-through list naming it
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** `MY_PROJECT_TOKEN` is absent from the child, never set to an empty value

#### Scenario: The extension host never receives the tool pass-through list

- **GIVEN** a configured tool pass-through list naming a variable the parent holds
- **WHEN** an extension host's environment is built with `Scrubbed`
- **THEN** the variable is absent unless the host's own declared names include it

#### Scenario: The list is read where the policy is built

- **GIVEN** Lunate's source
- **WHEN** the tool-process pass-through list is read
- **THEN** the read happens in `ChildProcess.BuildEnvironment`, and no file that hands a child an environment reads it at the spawn site
