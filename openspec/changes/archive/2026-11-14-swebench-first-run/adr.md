# ADR Review Manifest

- Status: completed
- Review date: 2026-11-13

## Review Summary

ADR review completed for this change. The change adds benchmark tooling and committed run evidence around the existing print-mode contract and the `benchmark-harness` adapter. It introduces no durable architectural commitment: no agent-loop, tool-contract, protocol, session-format, dependency-direction or public-API change. The standalone adapter/orchestrator split follows the existing `eval/` and `scripts/` tooling boundary and is recorded as design decision D2/D7, not as a repository-level ADR.

## In-Force ADRs Reviewed

- ADR-0001 no-AOT single-file budgets
- ADR-0003 loop-own vs MAF harness
- ADR-0011 repo quality tooling
- ADR-0017 extensibility architecture and its addenda
- ADR-0019 Xenoatom terminal UI
- All other repository-level ADRs were considered; none governs benchmark tooling or the published-run evidence.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
