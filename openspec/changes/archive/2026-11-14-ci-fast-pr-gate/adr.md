# ADR Review Manifest

- Status: completed
- Review date: 2026-11-14

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- ADR-0001 — self-contained single-file releases with CI budgets
- ADR-0005 and its addendum — startup budget calibration and the macOS floor rule
- ADR-0009 — memory budget gate
- ADR-0024 — bash guard (the Linux verify leg installs bubblewrap)
- ADR-0035 — tool child environment pass-through (not affected)

No in-force ADR fixes the CI event topology; this change records it as ADR-0036.

## New Durable ADRs Created

- `adr/0036-ci-gate-sized-by-event.md` — the per-event gate: Ubuntu fast gate on a pull request, Ubuntu test
  gate in the merge queue, and the full matrix, PowerShell twin, perf and CodeQL on `main` and the nightly.
