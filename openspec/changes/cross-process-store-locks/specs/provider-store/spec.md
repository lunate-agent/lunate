## ADDED Requirements

### Requirement: Cross-process provider-store writes

The provider store SHALL hold a cross-process claim for every save and for its migration
read-modify-write, so a migration in one process does not replace a document another process saved
from a stale read. The claim SHALL be a lock file beside `providers.json`, distinct from the store
file so a reader is not refused. A migration SHALL take the claim and re-read the store under it
before writing. The setup wizard's commit SHALL take the claim before it reads the store and SHALL
merge its entry into the document it reads under the claim, not into a reading taken before the
walk. A read-only load SHALL NOT take the claim and SHALL NOT create the lock file. A write that
cannot take the claim within the bounded wait SHALL refuse with an error naming the store file and
the lock file, and SHALL leave the store unchanged.

#### Scenario: A migration re-reads under the claim

- **GIVEN** a provider entry saved by another process after a load's first read
- **WHEN** that load needs migration and takes the claim
- **THEN** it re-reads the store under the claim and preserves the other process's entry when it
  writes

#### Scenario: A migration that cannot take the claim refuses

- **GIVEN** another process holds the provider-store claim past the bounded wait
- **WHEN** a migration needs to write
- **THEN** it fails with an error naming `providers.json` and the lock file, and the store is
  unchanged

#### Scenario: A read-only load creates no claim

- **GIVEN** a provider store with entries and nothing to migrate
- **WHEN** it loads
- **THEN** no lock file exists beside it

#### Scenario: The setup wizard's commit keeps an entry saved during the walk

- **GIVEN** a provider entry another process saved after the wizard's walk had read the store
- **WHEN** the wizard's commit takes the claim, seeds its staged copy and writes its own entry
- **THEN** the saved entry is still in the store, beside the entry the walk staged
