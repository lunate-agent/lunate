## MODIFIED Requirements

### Requirement: The inherited policy withholds credential-shaped names by default

`Inherited` SHALL withhold, from the environment it inherits, every variable whose name is shaped
like a credential, whether or not Lunate resolves it, unless the tool-process pass-through list
names it exactly. The rule SHALL be a word match and not a substring match: the name SHALL be split
on `_` and `-`, and the variable SHALL be withheld when any part equals one of `KEY`, `KEYS`,
`TOKEN`, `TOKENS`, `SECRET`, `SECRETS`, `PASSWORD`, `PASSWORDS`, `PASSWD`, `PASS`, `CREDENTIAL`,
`CREDENTIALS` or `CREDS`, compared case-insensitively. A name no part of which equals one of those
words SHALL stay inherited, and a name that matches only a substring of a word SHALL stay inherited.

One name family SHALL be exempt from the shape rule and SHALL reach the child whole: git's
environment configuration — `GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_<n>` and `GIT_CONFIG_VALUE_<n>` for
a numeric `<n>`, matched case-insensitively. Git reads the keys and values of the first
`GIT_CONFIG_COUNT` entries and refuses the whole configuration when one member is missing, so a
partial family is a broken git child rather than a withheld credential. The exemption SHALL be a
fixed family in the builder: it SHALL NOT be user-configurable, SHALL NOT widen the pass-through
list and SHALL NOT withhold, admit or rewrite any other name.

The rule SHALL be applied by the one shared builder, SHALL apply under `Inherited` only — the
`Scrubbed` policy inherits nothing, so a credential-shaped name reaches an extension host only when
that host's own declared names ask for it — and SHALL apply to declared values as well as to
inherited variables, so a spawn site cannot re-introduce a withheld name by declaring it.

#### Scenario: An unresolved ambient credential does not reach the tool child

- **GIVEN** a parent environment holding `GITHUB_TOKEN`, `NPM_TOKEN`, `AWS_SECRET_ACCESS_KEY` and `MY_TOOLCHAIN_FLAG`
- **WHEN** a tool child's environment is built with `Inherited` and no pass-through list
- **THEN** the first three are absent, and `MY_TOOLCHAIN_FLAG` and `PATH` are present

#### Scenario: The rule is a word match, not a substring match

- **GIVEN** a parent environment holding `MONKEY`, `KEYBOARD_LAYOUT`, `HOCKEY_POOL`, `GITLAB_TOKENIZE_TIMEOUT` and `OPENAI_BASE_URL`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** all five reach the child — no part of any name equals a credential word

#### Scenario: The rule is case-insensitive

- **GIVEN** a parent environment holding `github_token` and `Github_Token`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** both are absent

#### Scenario: An explicitly listed credential-shaped name is admitted

- **GIVEN** a parent environment holding `GITHUB_TOKEN` and a pass-through list naming it
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** `GITHUB_TOKEN` reaches the child

#### Scenario: A declared value cannot put a withheld name back

- **GIVEN** a tool child's environment built with `Inherited` and a declared value for `GITHUB_TOKEN`
- **WHEN** no pass-through list names it
- **THEN** `GITHUB_TOKEN` is absent from the child

#### Scenario: A credential-shaped name reaches an extension host only when declared

- **GIVEN** a parent environment holding `GITHUB_TOKEN`
- **WHEN** an extension host's environment is built with `Scrubbed`
- **THEN** `GITHUB_TOKEN` is absent, and a host whose declared names include it receives it

#### Scenario: Git's environment configuration reaches the tool child whole

- **GIVEN** a parent environment holding `GIT_CONFIG_COUNT=3`, `GIT_CONFIG_KEY_0=credential.helper` with an empty `GIT_CONFIG_VALUE_0`, `GIT_CONFIG_KEY_1=gc.auto` with `GIT_CONFIG_VALUE_1=0`, and `GIT_CONFIG_KEY_2=maintenance.auto` with `GIT_CONFIG_VALUE_2=false`, plus `GITHUB_TOKEN`, `NPM_TOKEN` and `AWS_SECRET_ACCESS_KEY`
- **WHEN** a tool child's environment is built with `Inherited`
- **THEN** all seven `GIT_CONFIG_*` names reach the child and the three credentials do not

#### Scenario: The bash change scan's git child sees the same git configuration

- **GIVEN** a session whose environment holds the three-entry git configuration above and a repository in the workspace
- **WHEN** the post-bash change scan runs `git status`
- **THEN** git answers the scan (the report's source is git, not the content-stamp backstop) and git's own `credential.helper` is empty
