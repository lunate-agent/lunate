# ADR Review Manifest

- Status: completed
- Review date: 2026-11-14

## Review Summary

This change keeps the published-run evidence inside the repository and makes the orchestrator write
its working tree there. It changes a benchmark tool's default output directory and the location of
committed run data; it introduces no agent-loop, tool-contract, protocol, session-format,
dependency-direction or public-API commitment. The decision is recorded as design decisions D1-D4
in `design.md`, not as a repository-level ADR.

## In-Force ADRs Reviewed

- ADR-0001 no-AOT single-file budgets
- ADR-0003 loop-own vs MAF harness
- ADR-0011 repo quality tooling
- ADR-0017 extensibility architecture and its addenda
- ADR-0019 Xenoatom terminal UI
- All other repository-level ADRs were considered; none governs where benchmark evidence is
  committed.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
