## Why

Every user-visible pull request edits the same few lines of `CHANGELOG.md`, so the moment one such
pull request merges, every other open one is `CONFLICTING`. GitHub refuses to enqueue a conflicting
pull request, so it sits outside the merge queue while `main` moves further away; the docs chain
(#774, #775, #776) and the setup-wizard chain (#664, #697, #702, #709) each lost days to it. The
conflict is the file's shape, not a worker's mistake.

## What Changes

- Every pull request that has a release-note line writes it to `changes/<pr-number>.md` and never to
  `CHANGELOG.md`. `changes/README.md` states the rule in five lines.
- `scripts/changelog-assemble` (Python 3 standard library) gains two modes: `--check` compares the
  unreleased section with what the fragments produce, `--write` rewrites that section from them.
  Fragments are ordered by pull-request number, the order the merge queue produces.
- The release pull request runs `--write` and commits the result. Releases are recognised by branch
  `release/<version>` or a title starting `chore(release)`.
- `ci-ok` fails when a pull request other than a release pull request edits `CHANGELOG.md`. The
  fragment-only pull requests pass. The gate names `changes/<pr-number>.md`.
- Migration lands in the same pull request: today's unreleased entries stay where they are and are
  not reordered; fragments describe the work that is still open. The first release consumes the
  fragments and nothing else.
- `CONTRIBUTING.md` gains the fragment rule in the pull-request section, the release runbook gains
  the assembly step, and `docs/README.md` points at the fragment rule. `AGENTS.md` is protected, so
  its exact proposed edit is quoted in `design.md` for maintainer approval instead of applied.
- A fragment is prose, so `scripts/ci-plan.sh` gives a fragment-only pull request the docs profile
  instead of the full matrix.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: adds the per-pull-request changelog fragment, the assembler contract, the release
  pull request's exemption and assembly step, and the aggregate-gate failure when a non-release pull
  request edits `CHANGELOG.md`.

## Impact

`changes/`, `scripts/changelog-assemble`, `scripts/changelog-guard`, `scripts/ci-plan.sh`,
`scripts/ci-ok.sh`, `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `CHANGELOG.md` (its
unreleased section gains the generated marker region), `CONTRIBUTING.md`, `docs/README.md`,
`docs/distribution.md`, `.markdownlint-cli2.yaml`, and the Python test suites under `tests/scripts/`.
No new package, public API, runtime behaviour or store format.
