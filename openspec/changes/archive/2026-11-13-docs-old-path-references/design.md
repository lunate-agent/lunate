# Design — the last references to the pre-move document paths

## Shape

The S4 move is done: `docs/spec/`, `docs/spikes/` and `docs/eval/` no longer exist and the live
links point at `docs/dev/`. What is left is the two places where a current document still names an
old path, and the checker allowance that let that survive review.

```text
openspec/specs/extensions/spec.md      docs/spec/, docs/eval/   → docs/dev/...
scripts/docs_check/constants.py        old prefixes exempt      → removed
scripts/docs_check/self_test.py        no case for old paths    → two cases pin it
adr/, docs/dev/spikes/, docs/dev/eval/, CHANGELOG.md, the plan   → unchanged records
```

## Decisions

- **Records keep their paths.** AGENTS.md's records rule covers ADRs, spike files, eval write-ups and
  the changelog, and the plan page's old-path table is part of the signed-off architecture. Rewriting
  any of them would edit history to match today, so the grep that checks the exit criterion excludes
  exactly those trees.
- **A spec is current, not a record.** `openspec/specs/extensions/` tells a maintainer where the
  contract and the migration baseline live *now*; a path that the repository no longer has is a
  broken instruction. Both requirements change one path each and nothing else.
- **The checker must not enumerate the old paths.** `RECORD_PREFIXES` and `MAINTAINER_PREFIXES` were
  written while `docs/spec/`, `docs/spikes/` and `docs/eval/` existed. Left in place they are an
  in-repo reference to the old paths whose only effect is to hide the regression the first S4 pass
  actually shipped — the S-7 and S-8 spikes stayed behind and two pages linked into a directory that
  did not exist. The `dev/` prefix already covers every maintainer page and record in its new home,
  so the old entries go and `docs/media/` stays.
- **Two self-test cases, not a new check mode.** The checker's rule is unchanged; only its prefix
  list is. The self-test gains one case per removed record prefix, and the existing "maintainer
  rename without a stub" case already proves a `docs/dev/` page needs no navigation entry.
- **No new assertion over prose.** A checker that greps every page for old paths would have to
  distinguish records from instructions and would re-derive the records rule in code. The self-test
  plus the one-off repository grep are enough for this slice; a general rule belongs in S1's scope.

## Risks

- Dropping `spec/` from `MAINTAINER_PREFIXES` makes any future `docs/spec/` page a user page that
  must be navigated or stubbed. That is the intent: the path is not coming back.
- The delta carries the two requirement texts whole, so an edit landing in them between now and the
  archive would collide. The archive is the last commit, and `openspec validate --all --strict`
  runs before it.
