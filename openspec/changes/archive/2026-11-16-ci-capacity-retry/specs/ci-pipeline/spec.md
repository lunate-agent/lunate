## MODIFIED Requirements

### Requirement: Main-run failure reporting

A red `push` run on `main` and a red nightly `schedule` run SHALL each report themselves so a failure
cannot sit unnoticed: the CI workflow SHALL run the same reporter for both events, and it SHALL read the
run's jobs and, for every job whose conclusion is red, open a `kind/bug` issue naming the failing job
and the run link, or comment on the already-open issue for that job instead of filing a duplicate. The
reporter SHALL key the duplicate check on the job's title, so a week of red runs for the same failing
job is one open card. A green run SHALL file nothing.

A job that never started is not a red job of the repository's build or tests: a job whose conclusion is
`cancelled`, whose `runner_id` is `0` and whose `steps` list is present and empty was never acquired by a
runner, so the reporter SHALL treat it as not red, file no card and post no comment for it on the
strength of this requirement, and SHALL name it, with the run link, in the run's own log and job summary,
so the red run is still visible even when another job of the same run files. All three conditions SHALL
hold, because each alone is ambiguous: a job cancelled by a superseding run also has no steps, and a
started job can still report `runner_id: 0`. The rule SHALL NOT cover a job that ran and did not pass: a
`cancelled` job that reached at least one step, a `failure`, a `timed_out` and every other red conclusion
SHALL still file, and a job whose `steps` list or `runner_id` is missing or unreadable SHALL be treated as
red, so the rule fails closed instead of passing on a document it cannot read.

A run whose red jobs never reached a machine SHALL be a capacity red, not a code red: GitHub cancels a
job when it never acquires a hosted runner for it, so nothing the repository builds or tests has run and
no step exists to triage. The two jobs whose result is derived from the others' — the `ci-ok` aggregate
and this reporter — SHALL be allowed beside the abandoned jobs, because they fail exactly because a leg
did not succeed; they SHALL NOT be named as abandoned jobs themselves, and no card and no comment SHALL
be produced for them as part of a capacity red. One red job that ran and is neither of those SHALL make
the run an ordinary red.

The reporter SHALL NOT open a card for a capacity red, in any attempt: it SHALL record the abandoned jobs
and what it saw in the run's job summary, SHALL fail while the retry budget is unspent, because the retry
reruns only failed jobs and the reporter has to be one of them to observe the retry, and SHALL succeed at
the budget with the summary naming the rerun as the remedy. The pipeline SHALL retry a capacity red's
failed jobs once, from a workflow triggered when the run completes, because a run cannot rerun itself
while it is in progress, and the retry SHALL be limited to a `push` to `main` and the nightly, the events
that run the full matrix and that this requirement reports on. The budget SHALL be one retry: a run at
its second attempt SHALL NOT be retried again, so a runner shortage costs one extra attempt rather than a
loop. A red job that ran SHALL NEVER be retried this way, so no failure that executed is retried away.

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

- **GIVEN** a `push` run on `main` whose `perf (macos-latest)` job concluded `cancelled` with `runner_id: 0` and an empty `steps` list, because the hosted runner was never acquired after multiple attempts
- **WHEN** the reporter runs
- **THEN** it files no card and posts no comment for that job, and the run's log names the job and the run link

#### Scenario: A run whose only red jobs never started files nothing

- **GIVEN** a push run in which every red job concluded `cancelled` with `runner_id: 0` and an empty `steps` list
- **WHEN** the reporter runs
- **THEN** it creates and comments on no issue, and the run's log says the jobs never got a runner and that a rerun is the remedy

#### Scenario: A cancellation that started stays red

- **GIVEN** a nightly run whose `verify (macos-latest, 2/2)` job concluded `cancelled` after reaching its first step, and a push run whose `perf (macos-latest)` job concluded `cancelled` with no `steps` field at all
- **WHEN** the reporter runs on each
- **THEN** both file a card, because neither is evidence of a job that never started

#### Scenario: A cancellation that held a runner stays red

- **GIVEN** a job concluded `cancelled` with an empty `steps` list and `runner_id: 7`, as a superseding run leaves it, and a job concluded `cancelled` with an empty `steps` list and no `runner_id` at all
- **WHEN** the reporter runs on each
- **THEN** both file a card, because an empty step list alone is not evidence that no runner was acquired

#### Scenario: A real failure beside an unstarted job still files

- **GIVEN** a push run in which `verify (macos-latest, 1/2)` concluded `cancelled` with `runner_id: 0` and an empty `steps` list and `verify (windows-latest, 2/4)` failed
- **WHEN** the reporter runs
- **THEN** it files exactly one card, naming `verify (windows-latest, 2/4)`, and the run's log names `verify (macos-latest, 1/2)` together with the run link

#### Scenario: A leg that never acquired a runner is retried, not reported

- **GIVEN** a `push` run on `main` in which `verify (macos-latest, 1/2)` and `perf (macos-latest)` were cancelled before any step started, and every job that ran succeeded
- **WHEN** the reporter runs at the run's first attempt
- **THEN** it opens no card, names the abandoned legs in the run's job summary, and the run's failed jobs are retried once

#### Scenario: A capacity red that survives the retry names the rerun and files nothing

- **GIVEN** the retry of that run in which the same legs were cancelled before any step started
- **WHEN** the reporter runs at the second attempt
- **THEN** it opens and comments on no card, and the run's job summary says the retry is spent and that a rerun is the remedy

#### Scenario: The derived jobs are never named as abandoned legs

- **GIVEN** a capacity red in which `ci-ok` and `red-main-issue` are red because the abandoned legs did not succeed
- **WHEN** the reporter runs
- **THEN** the run's job summary names neither job, and neither `CI failed: ci-ok` nor `CI failed: red-main-issue` is filed

#### Scenario: A leg that never started is named beside the card a run files

- **GIVEN** a red run in which `verify (macos-latest, 1/2)` was cancelled before any step started and `verify (ubuntu-latest, 2/4)` started a step and failed
- **WHEN** the reporter runs
- **THEN** it files the card for `verify (ubuntu-latest, 2/4)` and the run's job summary names `verify (macos-latest, 1/2)` with the run link

#### Scenario: A job that ran is never retried away

- **GIVEN** a red run in which at least one red job started a step and failed
- **WHEN** the reporter and the retry decision run
- **THEN** the run is not retried and that job's card is filed as before

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
