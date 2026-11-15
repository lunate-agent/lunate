## Context

The full three-OS matrix runs on a push to `main` and on the nightly, and the nightly already reports a
red run: `nightly-issue.sh` reads the run's jobs from the API, files one `kind/bug` card per red job and
comments on the open card for a job it has already filed. The push has no such path, so #888's macOS
failure on a `main` push had to be noticed by hand.

Two repository facts shape the change. The CI workflow runs on GitHub's runners, which cannot read the
lane's `lane.json`, so every target (milestone, label) has to come from GitHub state or from the
checkout itself. And a single test project is not a single area: `tests/Lunate.Coding.Tests` carries both
`area/tui` material (`InteractiveSessionThemeFileTests`, `LiveSurfaceRepaintTests`) and `area/core`
material, so an area rule that maps the project name alone files TUI failures as core.

## Goals / Non-Goals

**Goals:**

- A red push run on `main` files one deduplicated `kind/bug` card per red job, naming the job and the run.
- The push path is the nightly path: one script, one decision, one title scheme.
- The card targets the lowest open version milestone and an area label only when the failing test's
  project decides it.
- The self-test covers the push case, the milestone lookup and the area label, and fails before the
  change.

**Non-Goals:**

- Gating on the reporter: `ci-ok` stays the only required check and does not need the reporter.
- Reporting a manual `workflow_dispatch` run: a person dispatched it and watches it.
- Creating milestones or labels: a release closes its milestone, the maintainer owns the label set.
- Reading a job log when GitHub has none (a cancelled job, a run in flight): the card is filed without an
  area label.

## Decisions

### One script, one decision, two events

The `red-main-issue` job (formerly `nightly-issue`) runs on `schedule` and `push`, with `always()` and
the same `needs` list, and calls `scripts/nightly-issue.sh` once. The script keeps its name: the card
reuses that path, and a second script would be the duplication the card rules out.

The job is renamed because it is no longer the nightly's alone; `scripts/ci-ok.sh --self-test` pins the
trigger (`github.event_name == 'schedule' || github.event_name == 'push'`) and the job name, so a
regression to nightly-only is caught in the `plan` job.

The job runs on a green push too and files nothing: the script reads the API, and conditional job
filters that try to spell "some leg failed" duplicate what the script already decides. One small Ubuntu
job per main push is the cost.

### The decision moves to a pure helper

`scripts/ci-issue-triage` (Python 3, no network) owns the three decisions, mirroring `scripts/ci-ok-state`:

- `--jobs-json FILE` prints `id<TAB>name` for every red job, reading the jobs endpoint in any of the
  shapes the API returns (an object with `jobs`, a `--paginate --slurp` list of pages, or a bare list).
- `--milestones-json FILE` prints the lowest open version milestone title, or nothing. A version title
  is `MAJOR.MINOR.PATCH`; titles are compared numerically, so `0.10.0` sorts above `0.9.0`.
- `--log-file FILE --source-root DIR` prints one area label or nothing.

`nightly-issue.sh` keeps the `gh` calls: the milestone list, one job log per red job, the duplicate
search, the create or the comment. The milestone is read once per run and only when a job is red, so a
green run calls nothing but the jobs endpoint.

### The title is shared by both events

The title is `CI failed: <job>` for a push and the nightly. A shared title means the nightly's open card
for a leg is the card a later push comments on, instead of a second card for the same failing leg. The
body names the event (`push to main`, `nightly`, or the raw event name) and carries the run link and the
commit.

### The milestone comes from the API, not a constant

`gh api --paginate --slurp repos/<repo>/milestones?state=open` is filtered to version titles and sorted.
`backlog` never matches. A release closes its milestone on GitHub, which removes it from the open list,
so no code change is needed per release. When the list has no version milestone, the card is filed
without `--milestone` and the run logs it: never `backlog`, never no card.

### The area label follows the failing test's project, and abstains

The area comes from the failing test names in the red job's log (`failed <FQN>` lines, the format the
gate's runner already parses in `scripts/verify-test-runner.ps1`):

1. The test's project is the `tests/*` directory whose name prefixes the FQN, longest match first.
2. `Lunate.Tui.Tests` is `area/tui`; `Lunate.Coding.Tests` needs the test class's source file in the
   checkout: the class is the FQN segment before the method, and its file is the `.cs` under that test
   project that declares the class. If the file names the `Lunate.Tui` namespace (a `using Lunate.Tui;`
   or a `Lunate.Tui.<Type>` reference) the label is `area/tui`, otherwise `area/core`.
3. Any other project, a class file that cannot be found, a red job with no failing test name, or failing
   tests whose labels disagree produce no area label.

Step 2 is what the card demands: `InteractiveSessionThemeFileTests` lives in `Lunate.Coding.Tests` but
references `Lunate.Tui.LiveAreaRenderer`, and `LiveSurfaceRepaintTests` imports `Lunate.Tui`; a rule
keyed on the project or the test namespace alone would file them `area/core`.

The label is passed to `gh issue create` as it is. A missing `area/tui`/`area/core` label fails the
filing step loudly instead of silently filing an unlabelled card, which is the visible signal that the
repository's label set is incomplete.

## Risks / Trade-offs

- The area rule reads the checkout at the run's commit, so a renamed test class yields no area label
  rather than a wrong one; the card is still filed.
- The shared title does not match cards filed under the old `Nightly CI failed: ` prefix, so an old open
  nightly card for a leg can be joined by one new card for the same leg. Those cards are transient and
  carry the milestone the release closes.
- A push run's reporter adds one small job to every main push. It is not a required check, so it cannot
  change `ci-ok`'s verdict.

## Migration

None for users. The workflow, the script and the spec change together; the self-tests are the migration
check.

## Open Questions

None. Every ambiguity the grill-me pass raised is answered in the decisions above; the answers are
repeated in the pull request's **Intent decisions** list.
