## MODIFIED Requirements

### Requirement: Main-run failure reporting

A red `push` run on `main` and a red nightly `schedule` run SHALL each report themselves so a failure
cannot sit unnoticed: the CI workflow SHALL run the same reporter for both events, and it SHALL read the
run's jobs and, for every job whose conclusion is red, open a `kind/bug` issue naming the failing job
and the run link, or comment on the already-open issue for that job instead of filing a duplicate. The
reporter SHALL key the duplicate check on the job's title, so a week of red runs for the same failing
job is one open card. A green run SHALL file nothing.

The issue SHALL be placed in the lowest-numbered open milestone whose title is a version
(`MAJOR.MINOR.PATCH`), read from the GitHub API so the rule works on a GitHub runner from repository
state alone. A release closes its milestone, so the next lowest open version becomes the target without
a code change, and a `backlog` or any other non-version milestone SHALL never be selected. When no
version milestone is open, the card SHALL still be filed and the reporter SHALL say it filed the card
without a milestone.

A run whose red jobs never reached a machine SHALL be a capacity red, not a code red: GitHub cancels a
job when it never acquires a hosted runner for it, so nothing the repository builds or tests has run and
no step exists to triage. A job SHALL count as never acquired only when all three hold — its conclusion is
`cancelled`, its `runner_id` is `0` and it has no steps — because each alone is ambiguous: a job cancelled
by a superseding run also has no steps, and a started job can still report `runner_id: 0`. The two jobs
whose result is derived from the others' — the `ci-ok` aggregate and this reporter — SHALL be allowed
beside the abandoned jobs, because they fail exactly because a leg did not succeed. One red job that ran
and is neither of those SHALL make the run an ordinary red.

The reporter SHALL NOT open a card for a capacity red while the retry budget is unspent: it SHALL record
the abandoned jobs and what it saw in the run's job summary, SHALL comment on an already-open card for such
a job when one exists rather than opening a second, and SHALL fail, because the retry reruns only failed
jobs and the reporter has to be one of them to report the retry's outcome. The pipeline SHALL retry a
capacity red's failed jobs once, from a workflow triggered when the run completes, because a run cannot
rerun itself while it is in progress, and the retry SHALL be limited to a `push` to `main` and the nightly,
the events that run the full matrix and that this requirement reports on. The budget SHALL be one retry: a
run at its second attempt SHALL NOT be retried again, so a runner shortage costs one extra attempt rather
than a loop. A capacity red that survives the retry SHALL be filed as the same deduplicated card, and the
card's body SHALL state that no hosted runner was acquired and that the red is not a code failure. A red
job that ran SHALL NEVER be retried this way, so no failure that executed is retried away.

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

#### Scenario: A leg that never acquired a runner is retried, not reported

- **GIVEN** a `push` run on `main` in which `verify (macos-latest, 1/2)` and `perf (macos-latest)` were cancelled before any step started, and every job that ran succeeded
- **WHEN** the reporter runs at the run's first attempt
- **THEN** it opens no card, names the abandoned legs in the run's job summary, and the run's failed jobs are retried once

#### Scenario: A capacity red that survives the retry is filed with its cause

- **GIVEN** the retry of that run in which the same legs were cancelled before any step started
- **WHEN** the reporter runs at the second attempt
- **THEN** it files the `CI failed: verify (macos-latest, 1/2)` card, and the card's body says that no hosted runner was acquired and that the red is not a code failure

#### Scenario: A job that ran is never retried away

- **GIVEN** a red run in which at least one red job started a step and failed
- **WHEN** the reporter and the retry decision run
- **THEN** the run is not retried and the card is filed as before
