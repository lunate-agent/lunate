# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change. The run-limit surface follows the accepted loop and tool
contracts (ADR-0003, ADR-0013) and adds enforcement inside the loop, not a second loop or a
parallel message type. No existing ADR is superseded.

## In-Force ADRs Reviewed

- `adr/0003-loop-own-vs-maf-harness.md` — the loop owns tool execution and the seams between
  calls, which is where run limits belong.
- `adr/0013-agent-loop.md` — turn semantics and the `StepLimitReached` + `RunFinished(step_limit)`
  shape the new limits mirror.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced. The cost/limit semantics are
  recorded in this change's design and specs.
