## Why

The lane's pull-request readiness check lives in `~/.hermes/scripts/lunate-pr-ready.py`: it is
outside the repository, invisible on a pull request and unowned by the change it protects. This
week's review rounds were spent on findings a machine can name — an unarchived OpenSpec change, a
missing `changes/<pr-number>.md`, a hand-edited `openspec/specs/` entry, an acceptance check with no
answer — each of which costs a reviewer a round-trip to report. A committed job makes the same
findings visible in the branch before a reviewer is spent, and it is tested with the cases that
actually happened.

## What Changes

- New `.github/workflows/pre-ready.yml` runs one job, `pre-ready`, on every pull request (opened,
  synchronize, reopened, ready-for-review and body edits). It is advisory in this change: it is not
  added to the ruleset's required checks and not added to `ci-ok`, so a red run cannot block a merge
  until a maintainer turns it on after it has run green once.
- New `scripts/pre-ready.py` (Python 3 standard library) performs four checks and prints one line
  per check on success. A failure names the check, the offending path or issue, and what to do.
  1. **The archive is the last commit.** A pull request that adds or modifies
     `openspec/changes/<name>/` must end with the archive: the change directory is gone at `HEAD`,
     the final commit deletes it, and the final commit adds
     `openspec/changes/archive/<YYYY-MM-DD>-<name>/`.
  2. **A changelog fragment exists.** A non-release pull request adds `changes/<pr-number>.md` and
     never edits `CHANGELOG.md`; the release pull request is exempt, and the rule is inert until
     `changes/README.md` exists on the base branch.
  3. **No hand-edited spec.** A change under `openspec/specs/` must ride with an archive under
     `openspec/changes/archive/` in the same pull request, the rule `scripts/check-spec-archive.py`
     already enforces on `main`.
  4. **Every acceptance check is answered by number.** Each list item under the linked issue's
     `Acceptance` heading has an answer in the pull request body or a comment, marked `met` with a
     `file:line` or a test name; an unanswered check, a `not met` answer, or a `met` answer without
     evidence fails.
- `tests/scripts/test_pre_ready.py` proves the green case and each of the four real findings, plus
  the release exemption, the inert fragment rule, answers in a comment, and the cross-check against
  `scripts/check-spec-archive.py`.
- `scripts/verify.sh` runs the new suite in the docs and full profiles. `CONTRIBUTING.md` documents
  the job, the four checks and the advisory phase. `ci-pipeline` gains the requirement.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: adds the `pre-ready` pull-request job and its four mechanical checks, and records
  that it is advisory until a maintainer adds it to the required checks after a green run.

## Impact

`.github/workflows/pre-ready.yml`, `scripts/pre-ready.py`, `tests/scripts/test_pre_ready.py`,
`scripts/verify.sh`, `CONTRIBUTING.md`, `changes/<pr-number>.md`, and the `ci-pipeline` spec. No
product code, public API, dependency, session format or release path changes.
