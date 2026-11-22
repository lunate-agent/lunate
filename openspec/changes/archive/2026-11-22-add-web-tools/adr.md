# ADR Review Manifest

- Status: completed
- Review date: 2026-11-23

## Review Summary

ADR review completed for this change. The new tools add two `ITool` implementations inside the
existing `Lunate.Coding` container and reuse the existing approval, truncation and event boundaries.
No durable architectural pattern, dependency edge, protocol or contract changes, so no new
repository-level ADR is created. The search backend and the in-repo HTML converter are tool-level
decisions recorded in `design.md`; replacing either does not move a boundary this repository's ADRs
protect.

## In-Force ADRs Reviewed

- ADR-0002 — three-layer project graph and Microsoft.Extensions.AI model types: the tools stay in
  `Lunate.Coding`, use `ITool`/`ToolResult` and add no model types.
- ADR-0003 — the loop stays ours: the tools are invoked by `AgentHarness`, never by
  `FunctionInvokingChatClient`.
- ADR-0012 — tool contract and declaration adapter: hand-written `ParametersSchema`, `ToolRisk`,
  instructure error results and output bounded at the loop boundary.
- ADR-0013 — agent loop and harness surface: approval happens before `ExecuteAsync` and a decline is
  an instructing error result.
- ADR-0016 — workspace boundary for file tools: the web tools read no workspace path and add no path
  resolution.
- ADR-0041 — everything targets `net11.0`, no multi-targeting: the new files target the repository
  framework.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced.
