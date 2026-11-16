## Why

A red **push run on `main`** files nothing. The nightly files one `kind/bug` card per red job, but that
rule is scoped to `schedule`: `InteractiveSessionThemeFileTests.An_unknown_token_warns_and_the_built_in_theme_stands`
failed on the macOS leg of a `main` push run (#888, run 43699660894) and had to be noticed by hand. A
push to `main` is the first place the full three-OS matrix runs after a merge, so it must report itself
the way the nightly does.

## What Changes

- **A red `push` run on `main` reports itself.** The reporter job runs on a push as well as on the
  nightly, reads the run's jobs from the API, and files one `kind/bug` card per red job naming the
  failing job and the run — or comments on the already-open card for that job. It is the same script
  (`scripts/nightly-issue.sh`) and the same decision as the nightly's, not a second path.
- **The card takes the lowest-numbered open version milestone.** `0.4.0` stops being a constant in the
  script: the milestone is the lowest open milestone whose title is a version (`0.4.0`, `0.5.0`), read
  from the GitHub API so it works on a GitHub runner that cannot read the lane's state. `backlog` is
  never selected, and a release closes its milestone, so the next version becomes the target by itself.
  When no version milestone is open the card is still filed, without one, and the run says so.
- **The area label comes from the failing test's project.** A red job whose log names failing tests is
  labelled `area/tui` or `area/core` from the test's project and namespace: a test in `Lunate.Tui.Tests`
  is `area/tui`; a test in `Lunate.Coding.Tests` is `area/tui` when its test's source names the
  `Lunate.Tui` namespace and `area/core` otherwise. A job that fails without a failing test name (a
  build, format, perf or PowerShell leg), a test in a project outside those two, or failing tests that
  disagree produce **no** area label rather than a guessed one.
- **The reporter job is renamed to `red-main-issue`** because it no longer runs only on the nightly.
- The self-test of `scripts/nightly-issue.sh` covers the push case, the milestone lookup and the area
  label; `scripts/ci-ok.sh --self-test` pins the new trigger and the new job name.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the "Nightly failure reporting" requirement becomes "Main-run failure reporting": a
  red `push` run on `main` and a red nightly both file deduplicated `kind/bug` cards naming the failing
  job and the run, in the lowest-numbered open version milestone, with an area label only when the
  failing test's project and namespace decide it.

## Impact

- `.github/workflows/ci.yml`: the `red-main-issue` job (formerly `nightly-issue`) and its trigger, plus
  the comments that describe the reporter.
- `scripts/nightly-issue.sh`: the event input, the milestone lookup, the per-job log read and the area
  label; its `--self-test` gains the push, milestone and area cases.
- `scripts/ci-issue-triage` (new): the pure decision — red jobs, lowest open version milestone, area
  label from a job log and the checkout — so the filing script keeps only the `gh` calls.
- `scripts/ci-ok.sh --self-test`: the workflow invariant for the reporter's trigger and name.
- `CONTRIBUTING.md` and the CI spec: the rule as it now reads.
- No product code, public API, package or release-workflow change.
