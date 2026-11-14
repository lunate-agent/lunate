# ADR Review Manifest

- Status: completed
- Review date: 2026-11-14

## Review Summary

ADR review completed for the merge-queue tree checks. No new repository-level ADR was created: the
change adds one job to the event-sized gate that ADR-0036 already accepted, and follows that ADR's own
maintenance contract (add the job to `ci.yml` and to the `ci-ok` event table, and pin both in the
self-test). It does not replace the gate's topology, its release gate or its budgets.

`adr/` currently carries two `0036-` primaries (`0036-ci-gate-sized-by-event.md` and
`0036-tool-child-credential-shape-strip.md`), so `check-adr-numbers.py` fails on this tree until #848
renumbers one of them. That is the defect this change makes the merge queue reject, and it is why a new
numbered addendum was deliberately not written here: ADR-0036 is expected to become 0037, and writing
0037 in this change would reintroduce the duplicate. The refine-not-replace judgement is recorded in
`design.md` under **Decisions**; if the maintainers want it as a durable addendum, it is written after
#848 frees the number.

## In-Force ADRs Reviewed

- ADR-0036 — the CI gate is sized by the event. Refined, not replaced: its decision 3 skips the
  pull request's jobs in the queue and says a prose-only batch runs the plan only; this change adds the
  tree checks to every batch, because a batch of two prose pull requests is exactly where a duplicate
  ADR number appears. Its decision 5 (the event table in `ci.yml` and `scripts/ci-ok.sh`, pinned by the
  self-test) is the mechanism used.
- ADR-0001 — self-contained single-file releases held to budgets. Unaffected; no release, publish or
  budget step changes.
- ADR-0005 and its addendum — startup budgets and the macOS floor rule. Unaffected; the perf leg and its
  budget are untouched.
- ADR-0009 — memory budgets. Unaffected; no memory-gated step changes.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
