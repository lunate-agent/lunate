# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The change fixes discovery's reading of a listing it already
accepts and the wizard's narration of that failure; it does not change the provider model, the API
family set, a contract, a dependency or a protocol. The relevant in-force decision, ADR-0029, already
fixes the family set and says the model carries its family. Honouring the server's recommendation
only inside that set and skipping what it cannot speak is an application of ADR-0029, not a new
architectural commitment. No repository-level ADR is created or superseded.

## In-Force ADRs Reviewed

- ADR-0002 — Three-layer project graph and Microsoft.Extensions.AI model types
- ADR-0003 — The agent loop runs tools
- ADR-0027 — Provider store and credential handling
- ADR-0029 — A provider is a credential plus one base URL per API family; the model carries the family
- ADR-0037 — The CI gate is sized by the event
- ADR-0041 — Everything targets `net11.0` with no multi-targeting
- ADR-0044 — The decision model's assembly ownership

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced.

## Superseded ADRs

_None._

## ADR Addenda

_None._
