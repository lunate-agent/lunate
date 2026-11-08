# child-environment Specification

## Purpose
What a child process sees of the parent's environment: the one builder both policies live in, the
`Scrubbed` allow-list the extension hosts get, the `Inherited` whole-environment policy the tool
child gets with Lunate's own provider credentials removed, and the rule every spawn site names its
policy.

## Requirements

### Requirement: One child-environment builder with two named policies

Every child environment Lunate starts SHALL come out of `EnvironmentScrubber`, and a call SHALL name one of its two policies explicitly at the call site — there is no defaulted policy and no second file that builds an environment.

`Scrubbed` SHALL produce the minimal base set (the parent's values of `PATH`, `PATHEXT`, `HOME`/`USERPROFILE`, `SystemRoot`, `ComSpec`, `TEMP`/`TMP`/`TMPDIR`, `APPDATA`/`LOCALAPPDATA`, `LANG`), then the declared pass-through names, then the declared values, and nothing else — the extension hosts' policy.

`Inherited` SHALL produce the caller's whole environment (every variable it holds), then the declared values, and SHALL remove the names the caller declares as Lunate's own credentials — the tool child's policy, because the bash tool is the user's shell and must still see the user's own ambient environment.

#### Scenario: A spawn site names its policy
- **GIVEN** a child process Lunate starts
- **WHEN** its environment is built
- **THEN** the call site names `Scrubbed` or `Inherited`, and no source file other than `EnvironmentScrubber.cs` builds a child environment

#### Scenario: The extension host keeps the minimal set
- **GIVEN** a remote extension host with declared `env` and passthrough names
- **WHEN** its environment is built with `Scrubbed`
- **THEN** it carries the minimal base set plus the declared names, and carries nothing else the parent holds

#### Scenario: The tool child keeps the user's environment
- **GIVEN** a parent environment holding a toolchain's own variable
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** that variable and `PATH` reach the child

### Requirement: The inherited policy strips Lunate's own credentials

`Inherited` SHALL remove, from the environment it inherits, every variable Lunate resolves as a provider credential: each built-in provider preset's key variable and key alias, and each configured custom provider's `env:` credential reference. The user's own variables SHALL survive; only the names Lunate holds as credentials are removed. A credential name declared by the caller SHALL also be removed when it arrives with a declared value, so a spawn site cannot re-introduce a strip by accident.

#### Scenario: A provider key does not reach the tool child
- **GIVEN** a parent environment holding `OPENAI_API_KEY` and a built-in alias such as `GEMINI_API_KEY`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** neither variable is present, while `PATH` and an unrelated user variable are

#### Scenario: A custom provider's env reference is stripped too
- **GIVEN** a configured custom provider whose credential is `env:MY_GATEWAY_KEY`
- **WHEN** a tool child starts after that provider was resolved
- **THEN** `MY_GATEWAY_KEY` is not in the child's environment

#### Scenario: The strip is a name list, not a pattern match
- **GIVEN** a parent variable named close to a credential name but not one (for example `OPENAI_API_KEY_BACKUP`)
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** that variable survives — the strip removes exactly the names Lunate resolved

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
