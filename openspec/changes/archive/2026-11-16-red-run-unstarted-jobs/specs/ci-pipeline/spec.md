## MODIFIED Requirements

### Requirement: Main-run failure reporting

A red `push` run on `main` and a red nightly `schedule` run SHALL each report themselves so a failure
cannot sit unnoticed: the CI workflow SHALL run the same reporter for both events, and it SHALL read the
run's jobs and, for every job whose conclusion is red, open a `kind/bug` issue naming the failing job
and the run link, or comment on the already-open issue for that job instead of filing a duplicate. The
reporter SHALL key the duplicate check on the job's title, so a week of red runs for the same failing
job is one open card. A green run SHALL file nothing.

A job that never started is not a red job: a job whose conclusion is `cancelled` and whose `steps` list
is present and empty was never acquired by a runner, so the reporter SHALL file no card and post no
comment for it, and SHALL say so in the run's own log, naming the job and the run link, so the red run
is still visible. The rule SHALL NOT cover a job that ran and did not pass: a `cancelled` job that
reached at least one step, a `failure`, a `timed_out` and every other red conclusion SHALL still file,
and a job whose `steps` list is missing or unreadable SHALL be treated as red, so the rule fails closed
instead of passing on a document it cannot read.

The issue SHALL be placed in the lowest-numbered open milestone whose title is a version
(`MAJOR.MINOR.PATCH`), read from the GitHub API so the rule works on a GitHub runner from repository
state alone. A release closes its milestone, so the next lowest open version becomes the target without
a code change, and a `backlog` or any other non-version milestone SHALL never be selected. When no
version milestone is open, the card SHALL still be filed and the reporter SHALL say it filed the card
without a milestone.

The reporter SHALL add `area/tui` or `area/core` from the failing test's project only when the project,
its namespace and the failing test decide it: a test in the `Lunate.Tui` project is `area/tui`, a test
in the `Lunate.Coding` project is `area/tui` when its test source names the `Lunate.Tui` namespace and
`area/core` otherwise, and a card whose red job names no failing test, names a test in another project,
or names failing tests that disagree SHALL carry neither area label instead of a guess. The reporter
SHALL pass an area label only when the repository's label set has it, and SHALL file the card unlabelled
when it does not.

#### Scenario: A red push run files a card naming the job

- **GIVEN** a `push` run on `main` in which `verify (macos-latest, 1/2)` failed
- **WHEN** the reporter runs
- **THEN** it opens a `kind/bug` issue whose title names `verify (macos-latest, 1/2)` and whose body carries the run link

#### Scenario: A red nightly files a card naming the job

- **GIVEN** a nightly run in which `verify (windows-latest, 2/4)` failed
- **WHEN** the reporter runs
- **THEN** it opens a `kind/bug` issue whose title names `verify (windows-latest, 2/4)` and whose body carries the run link

#### Scenario: A job no runner ever acquired files nothing (#922)

- **GIVEN** a `push` run on `main` whose `perf (macos-latest)` job concluded `cancelled` with an empty `steps` list, because the hosted runner was never acquired after multiple attempts
- **WHEN** the reporter runs
- **THEN** it files no card and posts no comment for that job, and the run's log names the job and the run link

#### Scenario: A run whose only red jobs never started files nothing

- **GIVEN** a push run in which every red job concluded `cancelled` with an empty `steps` list
- **WHEN** the reporter runs
- **THEN** it creates and comments on no issue, and the run's log says the jobs never got a runner and that a rerun is the remedy

#### Scenario: A cancellation that started stays red

- **GIVEN** a nightly run whose `verify (macos-latest, 2/2)` job concluded `cancelled` after reaching its first step, and a push run whose `perf (macos-latest)` job concluded `cancelled` with no `steps` field at all
- **WHEN** the reporter runs on each
- **THEN** both file a card, because neither is evidence of a job that never started

#### Scenario: A real failure beside an unstarted job still files

- **GIVEN** a push run in which `verify (macos-latest, 1/2)` concluded `cancelled` with an empty `steps` list and `verify (windows-latest, 2/4)` failed
- **WHEN** the reporter runs
- **THEN** it files exactly one card, naming `verify (windows-latest, 2/4)`

#### Scenario: The card takes the lowest open version milestone

- **GIVEN** the open milestones `0.6.0`, `0.5.0` and `backlog`
- **WHEN** the reporter files a card
- **THEN** the card is placed in `0.5.0`, and after a release closes `0.5.0` the next card takes `0.6.0`

#### Scenario: A card is filed without a milestone when no version is open

- **GIVEN** only `backlog` is open
- **WHEN** the reporter files a card
- **THEN** the card is filed without a milestone and the run says so, rather than landing in `backlog`

#### Scenario: The area label follows the failing test's project

- **GIVEN** a red verify job whose log names `Lunate.Coding.Tests.InteractiveSessionThemeFileTests.An_unknown_token_warns_and_the_built_in_theme_stands`
- **WHEN** the reporter files the card
- **THEN** the card carries `kind/bug` and `area/tui`, and a card for a coding test whose source names no `Lunate.Tui` namespace carries `area/core`

#### Scenario: No area label when the project does not decide it

- **GIVEN** a red job that names no failing test, names a test in another project, or names failing tests that disagree
- **WHEN** the reporter files the card
- **THEN** the card carries `kind/bug` and no area label

#### Scenario: An open card is reused instead of duplicated

- **GIVEN** an open issue whose title already names the failing job
- **WHEN** the next red run's reporter runs
- **THEN** it comments on that issue and creates no second one

#### Scenario: A green nightly files nothing

- **GIVEN** a run in which every job succeeded
- **WHEN** the reporter runs, on a push or the nightly
- **THEN** it creates and comments on no issue
