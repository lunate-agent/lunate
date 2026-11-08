## ADDED Requirements

### Requirement: Cross-process settings writes

A settings write SHALL hold a cross-process claim for its whole read-modify-write, so two Lunate
processes that each read first merge instead of each replacing the file with a document that lacks
the other's change. The claim SHALL be a lock file beside the settings file, distinct from the
settings file itself so a reader is not refused. The writer's in-process lock SHALL continue to
serialize threads of one process. A write that cannot take the claim within the store's bounded
wait SHALL refuse with an error naming the settings file and the lock file, and SHALL leave the
settings file unchanged. A read SHALL NOT take the claim and SHALL NOT create the lock file.

#### Scenario: Two processes merge instead of losing a change

- **GIVEN** process A holds the settings claim and has written `model`
- **WHEN** process B's write of `favorites` reaches the claim, takes it after A releases, and reads
- **THEN** B sees A's `model` and the saved file carries both `model` and `favorites`

#### Scenario: A writer that cannot take the claim refuses

- **GIVEN** another process holds the settings claim past the bounded wait
- **WHEN** a settings write tries to take it
- **THEN** the write fails with an error naming the settings file and the lock file, and the
  settings file is unchanged

#### Scenario: A read never creates the claim

- **GIVEN** no settings write has run
- **WHEN** settings are resolved
- **THEN** no lock file exists beside the settings file
