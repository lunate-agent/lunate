# ADR Review Manifest

- Status: completed
- Review date: 2026-11-27

## Review Summary

ADR review completed for this change. The change is confined to the gate scripts under `scripts/`, the
verify matrix's shard counts and the test projects: it replaces the positional verify-shard assignment
with a weight-aware one that places whole projects, records the weights a run measures, splits the
process-bound half of the Coding suite into `Lunate.Coding.Process.Tests`, keeps Ubuntu and Windows on
the dispatch's count so the balance bound applies there, and keeps the macOS lane at its two-shard cap
with the card's best-wall-time acceptance. The new test project links existing test files and adds only
`InternalsVisibleTo` entries, so it introduces no product dependency edge, no change to the agent loop,
the tool contract, the protocols, the public API or the session format, and no new long-lived
architectural commitment: the partition rule is a property of the build tooling, expressed as a
`ci-pipeline` requirement, and it can be replaced by another scheduler without touching the product.
No repository-level ADR is created.

## In-Force ADRs Reviewed

- `adr/0003-loop-own-vs-maf-harness.md` — the loop is untouched; the shard scheduler sits outside the
  product and only decides which test modules a CI leg runs.
- `adr/0005-startup-budget.md` — the startup budget is measured once, on shard 1, exactly as before; the
  partition gives that shard a weight for it instead of pretending it is free.
- `adr/0015-session-format.md` — no session entry, schema version or golden fixture changes.
- `adr/0041-net11-target.md` — no target framework or publish shape changes; the split adds one
  `net11.0` test project and every shard still builds the whole solution.
- `adr/0043-contract-unions.md` / `adr/0045-decision-model-package.md` — no contract or package boundary
  is touched, so no public API entry changes.

## New Durable ADRs Created

- None - the partition rule is CI tooling covered by the `ci-pipeline` spec; it is not a durable
  architectural decision about the product.
