# ADR Review Manifest

- Status: completed
- Review date: 2026-11-25

## Review Summary

ADR review completed for this change. The change carries a maintainer decision already made on
#1024 — the local `web_search` tool keeps its place and gains a backend seam, provider-native search
is a separate layer — into one durable record. That is an architecture decision about the tool layer
and the approval path, so it is written as a new repository-level ADR rather than left in
`design.md`. No in-force ADR is amended or superseded: the seam stays inside the tool contract, the
loop, the approval matrix and the layering the existing records fix.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the seam lives in `Lunate.Coding`; no new project,
  package or parallel message type.
- `adr/0003-loop-own-vs-maf-harness.md` — the tool loop still runs tools and owns approval; a
  search backend is a tool implementation detail, not a loop change.
- `adr/0012-tool-contract.md` — `web_search` keeps `ITool`, its hand-written schema, `ToolRisk` and
  `ToolResult`; only the transport behind `ExecuteAsync` becomes selectable.
- `adr/0015-session-format.md` — no session entry, schema or usage value changes.
- `adr/0027-provider-store-model-profiles-and-usages.md` — the credential reference form and its
  resolution are reused; no profile, provider or usage entry is added for a backend.
- `adr/0035-tool-environment-pass-through.md` and `adr/0036-tool-child-credential-shape-strip.md` —
  a configured backend's `env:` reference joins the existing strip list; the pass-through and shape
  rules are unchanged.
- `adr/0043-contract-unions.md` — no event kind, hook result or contract surface moves.
- `adr/0045-decision-model-package.md` — no decision-model or package boundary changes.

## New Durable ADRs Created

- [`adr/0046-web-search-layers.md`](../../../adr/0046-web-search-layers.md) — web search has two
  layers: the local `web_search` tool with a user-scoped backend seam (DuckDuckGo default, SearXNG,
  Brave, Tavily; credential references resolved like provider credentials) and provider-native search
  as a separate, opt-in layer; the approval trade-off between them is recorded there.
