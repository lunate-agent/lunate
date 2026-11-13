# ADR Review Manifest

- Status: completed
- Review date: 2026-11-13

## Review Summary

ADR review completed for this change. No major durable architectural decision is introduced: the
change keeps the aggregate gate, the profile/path rules and every non-pull-request event intact, and
replaces only how a pull-request run recovers the state those rules decide on. It adds no product
boundary, public contract, dependency, stored format or release path, so no repository-level ADR is
created.

## In-Force ADRs Reviewed

- `adr/0011-repo-quality-tooling.md` — the committed gate script and its self-test stay the form;
  this change extends `scripts/ci-ok.sh` rather than bypassing it.
- `adr/0005-budget-calibration.md`, `adr/0005-addendum-startup-floor.md` and
  `adr/0009-memory-budgets.md` — the verify matrix, budgets and profile selection are unchanged.
- `adr/0003-loop-own-vs-maf-harness.md`, `adr/0012-tool-contract.md` and
  `adr/0013-agent-loop.md` — no agent, tool or loop behaviour is touched.
- `adr/0008-release-publish-flags.md` — no build or release workflow change.

No supersession is needed; no in-force ADR is contradicted.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
