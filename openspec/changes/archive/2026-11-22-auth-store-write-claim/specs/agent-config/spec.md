## ADDED Requirements

### Requirement: Cross-process auth-store writes

An auth-store write SHALL hold a cross-process claim for its whole read-modify-write, so two Lunate
processes that each load the store and each save merge instead of each replacing the file with a
document that lacks the other's key. The claim SHALL be an owner-only lock file beside the auth
file (`auth.lock` for `auth.json`), distinct from the auth file itself so a reader is not refused.
A save SHALL re-read the document under the claim and apply the keys this store set and removed
since its load to that reading, not to the document it loaded. The claim SHALL be released only
after the replacement lands. A save that changed no key SHALL take no claim and write nothing. A
write that cannot take the claim within the store's bounded wait SHALL refuse with an error naming
the auth file and the lock file, and SHALL leave the auth file unchanged; a document that no longer
parses under the claim SHALL refuse with the store's own invalid-file error, naming the file and
never a secret value. A read SHALL NOT take the claim and SHALL NOT create the lock file.

#### Scenario: A save merges a key another process wrote

- **GIVEN** process A holds the auth claim and has saved a new key
- **WHEN** process B's save reaches the claim, takes it after A releases, and re-reads
- **THEN** B sees A's key, and the saved file carries both A's key and B's change

#### Scenario: A key removed here stays removed

- **GIVEN** a store that loaded a key and removed it, and a process that saved another key meanwhile
- **WHEN** the store saves
- **THEN** the removed key is absent and the other process's key is present

#### Scenario: A save that cannot take the claim refuses

- **GIVEN** another process holds the auth claim past the bounded wait
- **WHEN** an auth save tries to take it
- **THEN** the save fails with an error naming the auth file and the lock file, and the auth file is
  unchanged

#### Scenario: A read never creates the claim

- **GIVEN** an auth file that exists and no auth write has run
- **WHEN** the auth store loads
- **THEN** no lock file exists beside the auth file

#### Scenario: A save with nothing changed writes nothing

- **GIVEN** a loaded auth store whose keys were not set or removed
- **WHEN** it saves
- **THEN** the auth file keeps its bytes and no lock file is created by that save
