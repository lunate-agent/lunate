## Why

`bash scripts/verify.sh --profile docs` fails on `main`: `scripts/no-docs-references.sh` reports
`tests/Lunate.Coding.Tests/TargetFrameworkTests.cs:25` as a test that "reads a docs path from the
repository root". It does not read anything — it names the record directory `docs/dev/spikes` in the
list of paths its project scan does **not** enter — but the guard's shape test cannot tell a named
path from a read path, and the guard runs only in the docs profile, which no code pull request runs.
#910 added that test on a full-profile pull request, so the false positive reached `main` with every
gate green (#929). A guard that reddens `main` on a legitimate line, and is only consulted for prose
pull requests, is either wrong or invisible; both halves have to be fixed.

## What Changes

- **The guard recognises a named docs path that is not read.** A test file may name one docs
  reference, spelled exactly, in a registered allowance: the reference is a value the test compares
  with or excludes, never a path it opens. The allowance is per file **and** per reference, is
  printed on every run, and does not widen anything else: any other docs path in that file, and a
  file/directory read on the allowed line, still fail. `TargetFrameworkTests`' exclusion of the
  spike records is the first entry; the rule's intent — no test reads the docs tree — is unchanged.
- **The guard runs where a docs read can land.** `scripts/no-docs-references.sh` becomes a step of
  the platform checks (`verify.sh --profile full` and `--profile platform`, so the CI `platform`
  job runs it once for every code pull request) and of `scripts/tree-checks.sh` (every push to
  `main` and the merge queue's `tree-checks` job). The docs profile keeps running it. A pull request
  that adds a docs read is now red before review, and a `main` push cannot carry the failure unseen.
- The guard's `--self-test` covers the allowed mention, an unlisted docs path in the same file, and
  a read call on the allowed line; `tests/scripts/test_tree_checks.py` pins the new cheap check so
  the two tree-check triggers cannot drift from it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the per-commit verification matrix runs the docs-tree guard in the platform checks,
  on a pull request as well as on a push to `main`, and in the shared tree-check command list — not
  only in the documentation profile — and the guard accepts a test's named-but-unread docs reference
  while still failing a read of it.

## Impact

- `scripts/no-docs-references.sh`: the mention allowance, the header that documents it, and three
  self-test cases.
- `scripts/verify.sh`: the guard as a platform-check step, and the comment that lists them.
- `scripts/tree-checks.sh` and its header: the guard as a tree check.
- `tests/scripts/test_tree_checks.py`: the pinned command list.
- `CONTRIBUTING.md` and `docs/dev/README.md`: the step lists as they now read.
- No product code, public API, package, ADR or release-workflow change. `TargetFrameworkTests` is not
  edited: its exclusion is legitimate and keeps naming the record location the repository documents.
