## Context

A changelog fragment is written where it lives: `changes/<pull-request>.md`. Its author links to the
rest of the tree from that folder, so an ADR is `../adr/0043-contract-unions.md` and a document is
`../docs/extensions/README.md`. The assembler lifts the bullet into `CHANGELOG.md` at the repository
root, where the same text resolves one level above the repository. `scripts/docs-check` rejects such
a link, and on 0.5.0 three CI jobs failed on the three links PR #921's fragment carried (#966).

The assembler already owns the notion the fix needs — `display()` turns an absolute path into a
root-relative posix string for its diagnostics — but it never applied any notion to the link
destinations inside an entry.

## Goals / Non-Goals

Goals:

- An assembled entry carries every link relative to the repository root, so `scripts/docs-check`
  passes on `CHANGELOG.md` without a hand edit.
- A fragment whose link leaves the repository fails the fragment author's own pull request, naming
  the fragment, the link and the path it resolves to.
- Nothing else about the assembly changes: no mode, no output shape, no released history.

Non-goals:

- Checking that a link's target exists. A fragment may link to a document its own pull request adds;
  `scripts/docs-check` already owns dead links and redirect-covered moves.
- Following symlinks to prove a root-relative path does not leave the repository through one.
  `scripts/docs-check` resolves links with `Path.resolve()` and catches that case; the assembler works
  on the text, so its diagnostic is the same on every machine.
- Rewriting prose a person wrote in `CHANGELOG.md` outside the generated region, or released history.

## Decisions

**Rewrite at the fragment parse, not at the assembly.** `parse_fragment` already turns a fragment's
lines into entries and collects per-fragment diagnostics; the rewrite and the escaping-link refusal
belong beside the other fragment rules (unknown heading, missing reference, text outside a bullet).
Putting them there gives `--check`, `--write`, `--check --tag` and `--release` the same behaviour for
free, because each assembles through `load_fragments`, and it keeps the refusal a *fragment* finding —
the author's own pull request — rather than a changelog finding.

**Rewrite every relative destination, not only the ones that escape `changes/`.** A rule that fires
only on `../` would leave a fragment linking `README.md` assembling to a root-relative `README.md`,
which points at the wrong file silently. Resolving against the fragment's directory and re-emitting
from the root is one rule that is right for both cases, and the card asks for it.

**What counts as a link.** The grammar mirrors `scripts/docs_check/constants.py`: inline links and
images, plus reference-style definitions, with link text inside an inline code span ignored, so the
assembler and the checker agree about what a link is. Anchors (`#x`), root paths (`/x`), schemes
(`https:`, `mailto:`, `tel:`, `data:`) are carried as written: each already resolves from any
directory. The rewrite keeps the `#anchor` tail and `<…>` bracketing of the destination.

**A diagnostic that names three things.** The refusal prints the fragment's path, the link as written
and the path it resolves to *relative to the repository root* (`../docs/dev/guide.md`), which is the
form a reader can act on and the same on every machine — no absolute checkout path leaks into CI logs.

**A `--self-test` mode, plus the Python suite.** `scripts/changelog-guard` shows the pattern the
repository uses for a small script's pure cases: a `--self-test` mode with no filesystem, run by the
gate. The assembler's end-to-end behaviour stays in `tests/scripts/test_changelog_assemble.py`, which
runs the script against temporary trees; the self-test adds the pure cases for the rewrite and the
refusal, and `scripts/verify.sh` runs it beside the suite.

## Flow

```
changes/1234.md            scripts/changelog-assemble                 CHANGELOG.md
+------------------+       +--------------------------------+        +---------------------+
| ### Fixed        |       | parse_fragment                 |        | ## [Unreleased]     |
| - see [ADR][a]   |  ---> |   rewrite_links(entry,         | --->   | ### Fixed           |
|   ../adr/x.md    |       |     where, "changes")          |        | - see [ADR](adr/x.md)|
+------------------+       |   - resolve against "changes/" |        +---------------------+
                           |   - emit relative to the root  |
                           |   - refuse outside the root    |
                           +--------------------------------+
```

The boundary on the left is the fragment author; the one on the right is `scripts/docs-check`, which
reads `CHANGELOG.md` at the root and only ever sees root-relative destinations.

## Risks / Trade-offs

- [A fragment links to a path only it can see, e.g. one it deletes later] -> The assembler never
  checks existence, so this cannot regress here; `scripts/docs-check` reports the dead link at the
  same pull request.
- [The rewrite changes an entry a release has already assembled] -> Impossible: the rewrite happens
  when a fragment is read, and released history is never read from fragments.
- [A link inside inline code is rewritten by mistake] -> The inline-code spans are computed first and
  a destination inside one is skipped; the self-test pins a code span carrying a link.

## Migration Plan

None. The change is forward-only: the next assembly of a fragment with relative links emits
root-relative links, and existing `CHANGELOG.md` text keeps its bytes. A release that already shipped
a wrong link keeps it — `CHANGELOG.md` is a record — and no fragment is edited by this change.

## Open Questions

None. No in-force ADR constrains this decision; it is a change-local fix to one script.
