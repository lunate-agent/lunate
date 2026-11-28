# ADR Review Manifest

- Status: completed
- Review date: 2026-11-28

## Review Summary

ADR review completed for this change. The change is evaluation tooling and pinned run data
(`eval/swebench/`, `scripts/eval/`, `tests/scripts/`): it defines the sample, the three arms and the
per-instance measurements a benchmark run reports. It introduces no product architecture, no public
contract and no boundary between Lunate's projects, so no new repository-level ADR was written.

## In-Force ADRs Reviewed

- `adr/0003-loop-own-vs-maf-harness.md` — the loop runs tools, which is why a
  `semantic_search` call is a `tool_call_start` event the summary can count and not a step.
- `adr/0015-session-format.md` — the session format is pinned; this change writes no session and
  changes no golden file.
- `adr/0027-provider-store-and-model-profiles.md` — usages are capability-bearing roles, which is
  what `usages.embeddings` in the generated home relies on.
- `adr/0040-hook-based-extensibility-on-observables.md` — `--json` and the extension wire share one
  projection, which is why the trajectory's event fields are the documented ones.
- `adr/0041-net11-target-frameworks.md` — no target framework moves; no C# code changes.
- `adr/0043-contract-unions.md` — the closed `AgentEvent` set stays closed; the harness reads only
  documented kinds.
- `adr/0045-decision-model-package.md` — the decision arm's package ownership is untouched; the
  harness keeps the arm shape it already supported.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
