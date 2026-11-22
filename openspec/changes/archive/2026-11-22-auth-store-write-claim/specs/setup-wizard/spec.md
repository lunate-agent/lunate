## MODIFIED Requirements

### Requirement: The wizard commit is serialized across processes

The wizard's confirmed commit SHALL take the target `settings.json`, `providers.json` and
`auth.json` cross-process claims for its whole final pass. It SHALL acquire the claims before it
reads any target, re-seed its staged bodies from those reads, apply the walk's answers to them, and
replace the targets before releasing the claims. A provider entry or an auth key another process
saved during the walk SHALL survive the commit. When a claim cannot be taken within the store
writers' bounded wait, the commit SHALL refuse with an error naming the target file and its lock,
and SHALL write nothing.

#### Scenario: A concurrent provider entry survives

- **GIVEN** a wizard whose walk read `providers.json` before another process added
  an entry with the ordinary provider-store writer
- **WHEN** the wizard's confirmed commit runs
- **THEN** the committed `providers.json` keeps the other process's entry and adds
  the walk's entry

#### Scenario: A concurrent auth key survives

- **GIVEN** a wizard whose walk read `auth.json` before another process saved a key there
- **WHEN** the wizard's confirmed commit runs and stages its own credential
- **THEN** the committed `auth.json` keeps the other process's key and adds the walk's credential

#### Scenario: The commit waits for the target claim

- **GIVEN** another process holds `settings.lock` or `auth.lock`
- **WHEN** the wizard's confirmed commit runs
- **THEN** the commit waits for the claim, then applies the walk's answers to the
  target file as the other process left it

#### Scenario: An unclaimable store writes nothing

- **GIVEN** another process holds a target claim for longer than the bounded wait
- **WHEN** the wizard's confirmed commit runs
- **THEN** it refuses with an error naming the target file and the lock, and every
  target file stays byte-identical
