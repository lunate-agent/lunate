# ADR Review Manifest

- Status: completed
- Review date: 2026-11-07

## Review Summary

ADR review completed for this change. No new repository-level ADR was created: the change reverses one decision inside an existing addendum, and the maintainer's work order on PR #641 names that addendum as the record to correct.

## In-Force ADRs Reviewed

- ADR-0003 (loop owns tools) — unchanged, respected: no loop change.
- ADR-0031 (addendum, skills index budget) — its budget decisions (soft 2%, hard 5%, `skills.indexBudget`, `AGENTS.md` protection) stay in force; its Decision 1 description half remains superseded.
- ADR-0032 (addendum, skill description spec limit) — amended in place. Its Decision 2 recorded the over-limit description as an *invalid skill*; this change records load-truncate-warn instead, because the maintainer's work order superseded that.

## New Durable ADRs Created

- None — no new architecture, dependency or contract is introduced.

## Deviation from the ADR default

The ADR skills default to superseding a wrong in-force decision with a new ADR rather than rewriting it. The work order on PR #641 asks explicitly to update ADR-0032 so it "says exactly that, it currently records the invalid-skill semantics, which is now wrong", and ADR-0032 is an addendum whose only two-decisions subject is the description limit. The decision was amended in place and its Context now records what it said before, so the history stays readable; a superseding ADR would have left an accepted file asserting the opposite of the in-force set.
