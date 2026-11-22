# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The change schedules existing `ITool` calls inside the
existing loop; it adds no dependency, no public contract member and no new runtime boundary. The
scheduling rule (read-only batches, fixed cap, deterministic event order) is a behaviour decision
recorded in the `agent-loop` spec, not a new durable repository-level architecture decision:
`ToolConcurrency` already fixes the contract, and ADR-0003/ADR-0013 already fix who runs tools and
how events travel. No repository-level ADR is created.

## In-Force ADRs Reviewed

- `adr/0003-loop-own-vs-maf-harness.md` — the loop still runs every tool itself; concurrency happens
  inside that loop, never through `FunctionInvokingChatClient`.
- `adr/0012-tool-contract.md` — tools keep the hand-written schema, risk and result contract; this
  change only consumes the existing `Concurrency` member.
- `adr/0013-agent-loop.md` — the harness surface, ordered event channel and history repair stay as
  specified; this change adds a scheduling path inside the same stream.
- `adr/0017-extensibility-architecture.md` — extension and MCP tools keep their existing approval,
  exposure and event behaviour; they stay on the serial path.
- `adr/0043-contract-unions.md` — no event kind is added, so the closed event set and its switches
  are untouched.

## New Durable ADRs Created

- None — no major durable architectural decision was introduced. The cap and the eligibility list are
  design choices of this change, stated in `design.md`, and can be widened by a later change without
  changing a contract.
