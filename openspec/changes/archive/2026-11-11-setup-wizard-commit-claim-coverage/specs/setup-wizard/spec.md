## ADDED Requirements

### Requirement: The wizard commit is serialized across processes

The wizard's confirmed commit SHALL take the target `settings.json` and
`providers.json` cross-process claims for its whole final pass. It SHALL acquire
both claims before it reads either target, re-seed its staged bodies from those
reads, apply the walk's answers to them, and replace the targets before releasing
the claims. A provider entry another process saved during the walk SHALL survive
the commit. When a claim cannot be taken within the store writers' bounded wait,
the commit SHALL refuse with an error naming the target file and its lock, and
SHALL write nothing.

#### Scenario: A concurrent provider entry survives

- **GIVEN** a wizard whose walk read `providers.json` before another process added
  an entry with the ordinary provider-store writer
- **WHEN** the wizard's confirmed commit runs
- **THEN** the committed `providers.json` keeps the other process's entry and adds
  the walk's entry

#### Scenario: The commit waits for the target claim

- **GIVEN** another process holds `settings.lock`
- **WHEN** the wizard's confirmed commit runs
- **THEN** the commit waits for the claim, then applies the walk's answers to the
  settings file as the other process left it

#### Scenario: An unclaimable store writes nothing

- **GIVEN** another process holds a target claim for longer than the bounded wait
- **WHEN** the wizard's confirmed commit runs
- **THEN** it refuses with an error naming the target file and the lock, and every
  target file stays byte-identical
