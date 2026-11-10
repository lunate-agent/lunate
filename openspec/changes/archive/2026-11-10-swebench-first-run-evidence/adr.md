# ADR Review Manifest

- Status: completed
- Review date: 2026-11-10

## Review Summary

ADR review completed for this change. The change completes benchmark-run evidence and fixes a
tactical spend-accounting rule; it introduces no durable architectural commitment.

## In-Force ADRs Reviewed

- ADR-0001 — No Native AOT; self-contained single-file releases held to budgets
- ADR-0002 — Three-layer project graph and Microsoft.Extensions.AI model types
- ADR-0003 — Own loop on `IChatClient`
- ADR-0004 — Windows terminal support
- ADR-0005 — Startup budgets (and the startup-floor addendum)
- ADR-0006 — In-process Roslyn
- ADR-0007 — TUI live-area concurrency
- ADR-0008 — Release build shape
- ADR-0009 — Memory budgets
- ADR-0010 — Anthropic adapter
- ADR-0011 — Repository quality tooling
- ADR-0012 — Tool contract (and the context-admission addendum)
- ADR-0013 — The agent loop and harness surface
- ADR-0014 — C# tools behind a backend interface
- ADR-0015 — Session format pinned by golden files
- ADR-0016 — Workspace boundary for file tools
- ADR-0017 — Extensibility architecture (and the commands, extensions, model-registry, packages,
  remote-host and skills addenda)
- ADR-0018 — Session schema 2 (and the branching/images addenda)
- ADR-0019 — XenoAtom.Terminal.UI not adopted
- ADR-0020 — ACP SDK choice
- ADR-0021 — Per-run workspaces and per-workspace services
- ADR-0022 — Generated model catalog, provider presets and API-key auth
- ADR-0023 — First-party extension tier
- ADR-0024 — Bash guard
- ADR-0025 — Verified edits
- ADR-0026 — Roslyn built-in composition boundary
- ADR-0027 — User-scoped provider profiles, usages and capability facts
- ADR-0028 — Decision layer is a general primitive
- ADR-0029 — A provider is a credential plus one base URL per API family
- ADR-0030 — Native decisions are a provider family
- ADR-0031 — Skills index budget scales with the context window
- ADR-0032 — Skill descriptions carry whole to the spec limit
- ADR-0033 — Unknown context window disables skills index budgets
- ADR-0034 — POSIX child descriptor isolation
- ADR-0034 — Tool child environment pass-through

The two ADR-0034 files predate this change (both are on `origin/main`); this change does not touch
them and renumbering is a separate change.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.
