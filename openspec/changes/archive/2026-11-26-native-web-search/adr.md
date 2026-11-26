# ADR Review Manifest

- Status: completed
- Review date: 2026-11-26

## Review Summary

ADR review completed for this change.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — model types stay MEAI types; the native search uses
  `HostedWebSearchTool` rather than a parallel marker.
- `adr/0003-loop-own-vs-maf-harness.md` — the loop owns local tool execution; a provider-native
  search is not a local tool call and stays inside the chat request.
- `adr/0012-tool-contract.md` — the local `web_search` tool's contract, risk and declaration shape
  are unchanged; only its per-request declaration is suppressed when the native layer is active.
- `adr/0027-provider-store-model-profiles-and-usages.md` — the condition reads the model's open
  capability feature set; no new usage is introduced.
- `adr/0046-web-search-layers.md` — the decision this change implements: provider-native search is
  the second layer, off unless the user opts in, separate from the local backend seam.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced. ADR-0046 already records the
  two-layer decision and the opt-in rule; this change implements it.
