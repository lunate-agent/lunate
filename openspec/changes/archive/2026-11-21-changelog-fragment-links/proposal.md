## Why

`scripts/changelog-assemble` copies markdown links out of the fragments verbatim. A fragment lives
at `changes/<pull-request>.md` and is written from that folder's perspective, so a link to an ADR
reads `../adr/0043-contract-unions.md`; assembled into `CHANGELOG.md` at the repository root the same
text points one level above the repository and fails `scripts/docs-check`. On 0.5.0 (PR #966) three
jobs — `platform`, `verify (ubuntu-latest, 1/4)` and `docs-region` — failed on exactly those three
links, and the release could not be approved until the file was edited by hand. Every release
assembles fragments written in `changes/`, so the next fragment that links outside its folder repeats
the failure.

## What Changes

- The assembly resolves every relative link destination in an entry against the fragment's own
  directory and emits it relative to the repository root: `../adr/x.md` in `changes/1234.md` becomes
  `adr/x.md`, and `README.md` in `changes/1234.md` becomes `changes/README.md`. Anchors
  (`#fragment`), root paths (`/adr/x.md`), URLs (`https:`, `mailto:`, …) and link text inside inline
  code are left as written, because they already resolve from any directory.
- A link whose destination resolves outside the repository from `changes/` is refused at assembly
  time, naming the fragment, the link and the path it resolves to. The refusal runs in the fragment
  parse, so `--check`, `--write` and `--check --tag` refuse alike and write nothing; the fragment's
  author finds out in their own pull request instead of the release.
- `scripts/changelog-assemble` gains a `--self-test` mode, like `scripts/changelog-guard`: cases over
  the rewrite and the refusal that need no filesystem, run by the verify profile next to the
  assembler's Python suite.
- `changes/README.md` states that a fragment's relative links are rewritten for the root and that a
  link leaving the repository is refused.

No mode, option or output of a successful run changes for a fragment that links only inside the
repository; released history in `CHANGELOG.md` is untouched, and no fragment is edited by this change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the *Changelog assembly* requirement gains the root-relative link rewrite and its
  named refusal, and the assembler gains a `--self-test` mode that proves both.

## Impact

`scripts/changelog-assemble`, `tests/scripts/test_changelog_assemble.py`, `scripts/verify.sh`,
`changes/README.md`, this change's own fragment and the `ci-pipeline` spec delta. No `src/` or C#
test project is touched, no dependency is added, and `scripts/docs-check` is not changed: the
assembled changelog stops producing the links it rejects instead.
