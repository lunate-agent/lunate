## MODIFIED Requirements

### Requirement: Cancellation kills the detached tree

Cancelling or timing out a tool process SHALL end the whole tree: the runner SHALL kill the spawned child and every descendant it started (children before parents) on cancellation and on timeout, then reap it, so no grandchild survives an interrupted call. This composes with the isolation above: the command session, its prompts and any dead-end spinner are all part of the killed tree. A background bash job SHALL apply the same rule when its timeout expires, when `bash_kill` ends it, and when its session ends.

#### Scenario: Cancel during a long call leaves nothing running
- **GIVEN** a `bash` call has started a long-running command tree
- **WHEN** the user presses `Esc` while the call runs
- **THEN** the turn stops, the process tree is gone from the process table, and the input line accepts the next command without a restart

#### Scenario: A killed background job leaves nothing running
- **GIVEN** a background bash job that has started a child
- **WHEN** `bash_kill` names the job
- **THEN** the job's whole tree is gone from the process table and the job is reaped

## ADDED Requirements

### Requirement: Background jobs end with their session

Every background bash job SHALL belong to the session that started it. Ending that session — an interactive quit, a finished print run, or the end of the ACP connection — SHALL kill every job the session still owns, children before parents, and SHALL wait for the kills before the session finishes. No background job or descendant SHALL survive its session, and a job SHALL NOT be adopted by another session.

#### Scenario: Quitting ends the session's jobs
- **GIVEN** an interactive session with a running background job
- **WHEN** the session quits
- **THEN** the job's process tree is gone before the process exits
