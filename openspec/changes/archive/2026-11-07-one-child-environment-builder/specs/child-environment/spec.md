## ADDED Requirements

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
