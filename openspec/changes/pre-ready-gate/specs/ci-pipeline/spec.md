# ci-pipeline — pre-ready pull-request checks

## ADDED Requirements

### Requirement: Pre-ready pull-request checks

The repository SHALL provide a committed pull-request job named `pre-ready` and a committed
decision script it runs, so the mechanical readiness findings are visible in the branch before a
reviewer is spent instead of living in a lane script outside the repository. The job SHALL run on
every pull request, including a draft and a body edit, and SHALL report on the pull request's head
commit rather than on the synthetic merge commit. The script SHALL use only the Python 3 standard
library and SHALL carry tests for every check's green and red case. The job SHALL be advisory in the
change that introduces it: it SHALL NOT be added to the repository ruleset's required checks and
SHALL NOT be a required job of the `ci-ok` aggregate, so it can run green on a real pull request
before a maintainer makes it required.

A failure SHALL name the check that failed, the offending path or issue, and the action that fixes
it; the job SHALL NOT fail with a bare exit code. The checks are:

1. **The archive is the last commit.** A pull request that adds or modifies
   `openspec/changes/<name>/` SHALL be archived in its final commit: no path under
   `openspec/changes/<name>/` SHALL exist at `HEAD`, the final commit SHALL delete or rename out of
   that directory, and the final commit SHALL add or rename into
   `openspec/changes/archive/<YYYY-MM-DD>-<name>/`.
2. **A changelog fragment exists.** A pull request that is not the release pull request SHALL add
   `changes/<pull-request-number>.md` and SHALL NOT edit `CHANGELOG.md`. The release pull request —
   branch `release/<version>` or title starting `chore(release)` — SHALL be exempt. The fragment
   rule SHALL be inert while `changes/README.md` does not exist on the base branch.
3. **No hand-edited spec.** A pull request whose changed paths include `openspec/specs/**` SHALL also
   include a path under `openspec/changes/archive/`; a spec change without an archive beside it SHALL
   fail.
4. **Every acceptance check is answered by number.** The job SHALL read the issues the pull request
   links in its title or body and, for each list item under an `Acceptance` heading in a linked
   issue, SHALL require an answer in the pull request body or a comment. The answer SHALL start with
   the check's number, SHALL say `met`, and SHALL carry evidence — a `file:line` or a test name. An
   unanswered check, an answer that says `not met`, or a `met` answer without evidence SHALL fail,
   and the failure SHALL name the issue and the check's number.

#### Scenario: A pull request that satisfies all four checks passes

- **GIVEN** a pull request whose change is archived in its final commit, which adds
  `changes/1234.md`, which changes no spec without an archive, and whose body answers every
  acceptance check of the linked issue with `met` and a `file:line` or a test name
- **WHEN** the `pre-ready` job runs
- **THEN** the script exits 0 and prints one line per check

#### Scenario: An unarchived change fails

- **GIVEN** a pull request that adds or modifies `openspec/changes/example/` and leaves the
  directory there at `HEAD`
- **WHEN** the `pre-ready` job runs
- **THEN** it fails, names the change directory, and says to run
  `openspec archive example --yes` as the final commit

#### Scenario: An archive that is not the final commit fails

- **GIVEN** a pull request that archived its change in an earlier commit and added a later commit
- **WHEN** the `pre-ready` job runs
- **THEN** it fails and says the archive must be the final commit

#### Scenario: A missing changelog fragment fails

- **GIVEN** a pull request that is not the release pull request and does not add
  `changes/1234.md`
- **WHEN** the `pre-ready` job runs
- **THEN** it fails, names `changes/1234.md`, and says to write the release-note line there

#### Scenario: Editing the changelog directly fails

- **GIVEN** a pull request that is not the release pull request and whose changed paths include
  `CHANGELOG.md`
- **WHEN** the `pre-ready` job runs
- **THEN** it fails and says only the release pull request assembles the changelog

#### Scenario: A hand-edited spec fails

- **GIVEN** a pull request that changes `openspec/specs/ci-pipeline/spec.md` and adds no path under
  `openspec/changes/archive/`
- **WHEN** the `pre-ready` job runs
- **THEN** it fails, names the spec path, and says to archive the change in the same pull request

#### Scenario: An unanswered acceptance check fails

- **GIVEN** a linked issue with four acceptance checks and a pull request body that answers only
  checks 1 to 3
- **WHEN** the `pre-ready` job runs
- **THEN** it fails, names check 4 and the issue, and says to answer it in the body or final comment

#### Scenario: Acceptance answers in a comment pass

- **GIVEN** a pull request body with no answers and a comment that answers every acceptance check
  with `met` and evidence
- **WHEN** the `pre-ready` job runs
- **THEN** the acceptance check passes

#### Scenario: An acceptance check that is not met fails

- **GIVEN** a linked issue whose acceptance check 2 is answered `not met`
- **WHEN** the `pre-ready` job runs
- **THEN** it fails and names check 2, so a not-ready pull request cannot report green

#### Scenario: The release pull request needs no fragment

- **GIVEN** a pull request on `release/0.4.0` that edits `CHANGELOG.md` and consumes fragments
- **WHEN** the `pre-ready` job runs
- **THEN** the changelog-fragment check passes

#### Scenario: The fragment rule is inert before the directory lands

- **GIVEN** a pull request whose base branch has no `changes/README.md`
- **WHEN** the `pre-ready` job runs
- **THEN** the changelog-fragment check reports itself inert instead of failing the pull request
  that introduces the directory

#### Scenario: The job is not a required check yet

- **GIVEN** the change that introduces the `pre-ready` job
- **WHEN** its workflow and `ci-ok`'s needs are read
- **THEN** `pre-ready` is its own job and appears in neither the ruleset nor `ci-ok`'s required jobs,
  so a red advisory run does not block a merge before a maintainer enables it
