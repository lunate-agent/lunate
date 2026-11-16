# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change. Every ADR under `adr/` was read and its status checked; the
supersession graph (ADR-0029 → 0027, 0031 → 0017 skills addendum, 0032 → 0031, 0033 → 0031) leaves no
live ADR that decides the observing extension surface. One durable ADR is created for this change.

## In-Force ADRs Reviewed

- ADR-0002 — three-layer project graph and MEAI model types: the stream is the existing
  `Lunate.Contracts.AgentEvent`; the design adds no parallel event or message type and `Lunate.Agent`
  gains only a generic `IAgentEvents` seam.
- ADR-0007 — System.Reactive at the TUI boundary: the decision keeps Rx out of the contract and out of
  `Lunate.Agent`, and leaves the TUI's live-area use untouched.
- ADR-0013 — the agent loop and harness surface: the tap is a seam beside the existing emit path, not a
  change to the loop.
- ADR-0017 — extensibility architecture and its addenda (extensions, commands, model registry, packages,
  remote host, skills): the typed hook catalogue, the JSON-serializable boundary, in-process trust and
  the remote host's request/notification split are the baseline this ADR extends; it does not supersede
  any of them.
- ADR-0023 — the first-party extension tier: the design gives the supported tier an observation surface
  without new permissions; no tier rule changes.
- ADR-0039 — the Mermaid renderer library: reviewed for packaging precedent only; unaffected.

## New Durable ADRs Created

- `adr/0040-hook-based-extensibility-on-observables.md` — the two-surface extension contract: typed
  intercepting hooks unchanged, one hot session-scoped BCL `IObservable<AgentEvent>` off the run's hot
  path, a manifest `events` declaration, and out-of-process parity by a declared projection of the same
  stream on protocol version 3.
