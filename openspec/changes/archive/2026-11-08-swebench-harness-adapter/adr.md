# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change. The adapter is tooling around the existing print-mode contract; it introduces no durable architectural commitment. It does not touch the agent loop, tool contracts, protocol boundaries, session format, dependency direction or public API. The choice of a standalone script over a `lunate` subcommand follows the existing `eval/` and `scripts/` tooling boundary and is recorded as design decision D1.

## In-Force ADRs Reviewed

- ADR-0001 no-AOT single-file budgets
- ADR-0003 loop-own vs MAF harness
- ADR-0011 repo quality tooling
- ADR-0017 extensibility architecture and its addenda
- ADR-0019 Xenoatom terminal UI
- All other repository-level ADRs in `adr/` were considered; none governs benchmark tooling.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
