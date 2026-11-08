## ADDED Requirements

### Requirement: A POSIX child inherits only its own descriptors

Every POSIX child Lunate spawns SHALL be given exactly the descriptors its own spawn names: standard
input at `/dev/null` or the line-pump pipe, standard output and standard error on captured pipes, and
nothing else. The pipe ends SHALL be created close-on-exec, and the child SHALL receive its
descriptors explicitly through the spawn file actions (`adddup2`/`addopen`) rather than by
inheritance. On Darwin the spawn SHALL also set `POSIX_SPAWN_CLOEXEC_DEFAULT`, so the child keeps
only the descriptors its file actions name; Linux keeps the close-on-exec handles, because glibc and
musl have no equivalent spawn flag. A child's descriptor set SHALL NOT contain a pipe end belonging
to a concurrent spawn.

#### Scenario: An overlapping spawn does not hold the earlier child's stdout

- **GIVEN** a spawn's captured stdout pipe is open in the parent while it is starting
- **WHEN** a second child is spawned at the same instant and stays alive
- **THEN** the first child's stdout reader reaches end-of-file as soon as the first child exits,
  while the second child is still alive

#### Scenario: An overlapping spawn does not hold the earlier child's stderr

- **GIVEN** a spawn's captured stderr pipe is open in the parent while it is starting
- **WHEN** a second child is spawned at the same instant and stays alive
- **THEN** the first child's stderr reader reaches end-of-file as soon as the first child exits,
  while the second child is still alive

#### Scenario: A child's descriptor list names no pipe end it did not receive

- **GIVEN** a child spawned while a concurrent child is alive
- **WHEN** the child lists its own open descriptors
- **THEN** every descriptor is one of its standard streams or the descriptor used to list them, and
  no pipe end from the concurrent spawn appears
