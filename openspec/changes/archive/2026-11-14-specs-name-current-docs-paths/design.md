# Design: specs name the current documentation paths

Refs #809, #614, #750, #751, #752. Follow-up to the 0.4.0 documentation slices S2–S4.

## Context

The documentation tree moved and split in S2–S4. Prior OpenSpec changes already repaired the
`repo-quality`, `distribution` and `extensions` requirements (#820, #825), so the only current
requirements that still disagree with the tree are the two the card names: the `ci-pipeline` prose
example still says `docs/guide.md`, and the `tui` key-table requirement still says the table lives in
the guide, although S2 moved it to `docs/keybindings.md`. Both old paths survive as redirect stubs
for one release, so the prose reads plausible while sending a maintainer to a page that no longer
owns the fact.

```text
maintainer
   │ reads
   ▼
openspec/specs/ci-pipeline/spec.md ── names ──▶ docs/guide.md      (stub → docs/dev/guide.md)
openspec/specs/tui/spec.md          ── names ──▶ docs/guide.md      (stub, no key table)
                                              └▶ docs/keybindings.md (owns the generated table)

after this change
openspec/specs/ci-pipeline/spec.md ── names ──▶ docs/dev/guide.md
openspec/specs/tui/spec.md          ── names ──▶ docs/keybindings.md
```

## Goals / Non-Goals

**Goals:**

- Both requirements name the page that owns the fact today.
- The `tui` requirement stops calling the table the guide's, in its title, prose and one scenario.
- Keep every rule, exemption, threshold and scenario outcome byte-for-byte apart from those names.

**Non-Goals:**

- Deleting the redirect stubs or changing `docs/docs.json`; that is the release after 0.4.0.
- Rewriting records — ADRs, spikes, eval write-ups, `CHANGELOG.md` and the signed-off plan — that
  were written with the old paths.
- Adding a checker that greps prose for old paths; the card's one-off grep is the acceptance.

## Decisions

1. **One changed and one renamed-then-changed requirement, no ADDED or REMOVED ones.** The
   requirements already own the facts; only the page names are wrong. `tui` carries a RENAMED entry
   (old title → new title) and a MODIFIED block under the new title, because OpenSpec matches a
   MODIFIED header against the current spec. Both deltas carry every scenario in full, so the
   archive is lossless.
2. **Rename the `tui` requirement, not just its path tokens.** "The guide's key table matches the
   catalog" would still be false after the path fix, and the requirement also refers to "the guide's
   heading" and "a guide whose key heading…". "The keybindings reference table matches the catalog"
   describes the same rule with the page that now owns it; the two-way check, exemptions and failure
   messages keep their meaning, with the diagnostic examples naming `docs/keybindings.md`.
3. **`ci-pipeline` changes one GIVEN path only.** The scenario is an example of a mixed prose/code
   pull request; `docs/dev/guide.md` exercises the same `docs/` prose rule as the old path did, so no
   rule text or expectation changes.
4. **No test change.** The behaviour is already implemented and pinned: `GuideKeyTableTests` reads
   `docs/keybindings.md` (`tests/Lunate.Tui.Tests/GuideKeyTableTests.cs`), and CI profile selection is
   covered by `scripts/ci-plan.sh`'s self-test. This change corrects the source of truth, not the
   implementation.
5. **Archive in this pull request, last commit.** The lane cannot push a proposal to `main` first;
   proposal, delta, design, ADR review, changelog fragment and archive land as commits on this branch,
   with the archive last, as the lane's rules require.
6. **The rework merges `main`, never rewrites the branch.** The review's blocking finding cited the
   proposal-on-`main` discipline; #849 replaced it with the rule that a card is one pull request
   whose archive is the last commit, which this branch already follows. `main` also added the
   fast-gate and merge-group scenarios to the `ci-pipeline` requirement, so the delta copies the
   merged block again and keeps only the one path change. The archive is redone as the last commit
   after the merge.
7. **The ADR renumber is out of scope and is reverted.** An earlier commit on this branch renamed
   `adr/0036-ci-gate-sized-by-event.md` to `0037` to unblock `scripts/check-adr-numbers.py`, which
   reports the duplicate `0036` that `main` carries. The review flagged it as unrelated to this
   card's documentation-path requirements, and it is: the duplicate number is a `main` defect that
   needs its own change, so the rename is reverted here and the branch states the red tree check
   rather than absorbing an unrelated fix.

## Risks / Trade-offs

- [A requirement block is copied whole and a later `main` edit collides at archive] → keep every
  copied byte identical except the page names, and re-read before archiving.
- [Other stale paths remain in specs] → the card's grep over `openspec/specs/` is run before the
  archive; it currently finds only these two files.
- [Renaming the `tui` requirement breaks a reference to its old title] → search the repository for
  the old title before the archive; nothing but the spec and this change name it.

## Migration Plan

Write the delta and design, record the ADR review, run `openspec validate --all --strict`, the one-off
grep, `scripts/docs-check --check` and markdownlint, commit `changes/<pr>.md`, then
`openspec archive specs-name-current-docs-paths --yes` as the final commit.

## Open Questions

None. The in-force ADR-0011 fixes the documentation map and its owner; this change points at that
convention's current paths and does not revisit it.
