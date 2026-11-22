# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The change implements ADR-0040, which the maintainer accepted
on #982 before this card started; the card also carries the maintainer's one correction (the
`AgentEvent` sentence names the C# 15 union of ADR-0043 rather than a closed hierarchy). ADR-0040 was
`proposed`, not accepted, when this change began, so setting its status and correcting that sentence
is the maintainer's instruction, not an edit to an accepted record. Every other in-force ADR is
reviewed and unchanged.

## In-Force ADRs Reviewed

- ADR-0002 — Three-layer project graph and Microsoft.Extensions.AI model types (and its decision
  addendum): the observable lives in the contract, `AgentEvent` stays the one message type, and no
  layer edge moves.
- ADR-0007 — System.Reactive confined to the TUI live area: the contract names BCL `IObservable<T>`,
  and `Lunate.Agent` keeps `IAsyncEnumerable<AgentEvent>`.
- ADR-0013 — The agent loop and harness surface: the loop is unchanged; a front end publishes what it
  already iterates.
- ADR-0017 and its addenda — Extensibility architecture, the contract assembly and the remote host:
  one contract assembly, one registration path, the remote host kept at hook-catalogue parity.
- ADR-0041 — Everything targets `net11.0`: no target framework moves.
- ADR-0043 — The contract's closed sets are unions: `Events` is typed with the existing union; no new
  event kind, case or parallel model is introduced.
- ADR-0045 — The decision model's own package: unchanged; only the contract version moves.

## New Durable ADRs Created

- None. ADR-0040 already records this design; this change implements it and adds no new decision.
