## MODIFIED Requirements

### Requirement: The inherited policy strips Lunate's own credentials

`Inherited` SHALL remove, from the environment it inherits, every variable Lunate resolves as a provider credential — each built-in provider preset's key variable and key alias, and each configured custom provider's `env:` credential reference — and every variable whose name carries a credential-shaped segment: a variable is stripped when one of its underscore-separated segments is `KEY`, `TOKEN`, `SECRET` or `PASSWORD`, compared case-insensitively. Matching SHALL be by whole segment under the underscore separator and never by substring, so `MONKEY` and `GITLAB_TOKENIZE_TIMEOUT` survive while `GITHUB_TOKEN` and `AWS_SECRET_ACCESS_KEY` do not; a name with no underscore is its own single segment. The user's own variables SHALL otherwise survive. A credential name or segment-matching name declared by the caller SHALL also be removed when it arrives with a declared value, so a spawn site cannot re-introduce a strip by accident.

#### Scenario: A provider key does not reach the tool child
- **GIVEN** a parent environment holding `OPENAI_API_KEY` and a built-in alias such as `GEMINI_API_KEY`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** neither variable is present, while `PATH` and an unrelated user variable are

#### Scenario: A custom provider's env reference is stripped too
- **GIVEN** a configured custom provider whose credential is `env:MY_GATEWAY_KEY`
- **WHEN** a tool child starts after that provider was resolved
- **THEN** `MY_GATEWAY_KEY` is not in the child's environment

#### Scenario: A name with a token segment is stripped
- **GIVEN** a parent environment holding `GITHUB_TOKEN`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** `GITHUB_TOKEN` is not in the child's environment

#### Scenario: A name with a secret segment is stripped
- **GIVEN** a parent environment holding `AWS_SECRET_ACCESS_KEY`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** it is not in the child's environment

#### Scenario: The strip is a name list, not a pattern match
- **GIVEN** a parent environment holding `MONKEY` (its only segment is `MONKEY`) and `KEYBOARD_LAYOUT` (its segment is `KEYBOARD`)
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** both survive — the strip removes known names and whole credential-shaped segments, never a run of characters inside a segment

#### Scenario: A look-alike segment survives
- **GIVEN** a parent environment holding `GITLAB_TOKENIZE_TIMEOUT`, whose token-shaped segment is `TOKENIZE`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** the variable survives — the strip matches `TOKEN`, not `TOKENIZE`

### Requirement: The inherited policy admits an explicit pass-through list after the strip

`Inherited` SHALL re-admit, from the set it inherits, only the variable names in the tool-process
pass-through list, and SHALL apply that list **after** both the credential strip and the
name-segment strip, so an explicitly named variable wins over either deny for that exact name.
Matching SHALL be by exact name — the platform's environment-name comparison and nothing else —
never a prefix, suffix or pattern. A name the parent does not hold SHALL stay absent from the
child, and a credential name or segment-matching name the user did not list SHALL still be
stripped.

The tool child's pass-through list SHALL be read where its policy is built
(`ChildProcess.BuildEnvironment`), never at a spawn site, and SHALL be handed only to the
`Inherited` policy — the `Scrubbed` policy receives only its own declared names, because extension
hosts never receive the inherited set at all. The list SHALL stay user-scope settings only: a
project or workspace settings file that carries `toolEnvironment` SHALL be refused by name, and the
resolved list SHALL be named in one startup admission notice.

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

#### Scenario: A passed-through name survives a matching segment

- **GIVEN** a parent environment holding `GITHUB_TOKEN` and a pass-through list naming it
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** `GITHUB_TOKEN` reaches the child — the exact name is the opt-in against the segment rule

#### Scenario: A credential name not in the list stays stripped

- **GIVEN** a parent environment holding `OPENAI_API_KEY` and `GEMINI_API_KEY` and a pass-through list naming only `OPENAI_API_KEY`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** `OPENAI_API_KEY` arrives and `GEMINI_API_KEY` does not

#### Scenario: The pass-through is an exact name, never a pattern

- **GIVEN** a parent environment holding `OPENAI_API_KEY` and `OPENAI_API_KEY_BACKUP`, and a pass-through list naming only `OPENAI_API_KEY_BACKUP`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** `OPENAI_API_KEY_BACKUP` arrives (the exact name wins over the strip and the segment rule) and `OPENAI_API_KEY` does not; a listed `OPENAI_*` matches nothing

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

#### Scenario: A project settings file cannot opt a name back in

- **GIVEN** a project-scope settings file whose `toolEnvironment.passthrough` names `GITHUB_TOKEN`
- **WHEN** the settings resolve
- **THEN** the section is refused by name, and the tool child's environment is unaffected
