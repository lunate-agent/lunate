# Hook-based extensibility on observables

## Why

Lunate already has the intercepting half of the hook pattern: a typed hook catalogue
(`IHookHandler`, `HookRunner`) where every hook declares observe, transform, replace or block,
ordered by priority, async, with a per-handler timeout and a declared failure policy — the shape Pi
calls `pi.on()` and Claude Code calls `PreToolUse`/`PostToolUse`. What Lunate does not offer is one
place to *watch* a run. An extension observes today only at the handful of hook points that exist
(`ProviderStreamEvent`, `RunSettled`, `SessionStarted`/`SessionEnding`, `ModelChanged`/`ToolsChanged`)
and through the file-change bus, so a logger, a metrics exporter or a UI re-registers a handler per
point and still misses every event that has no hook — text fragments, tool arguments, approvals,
usage, retries.

The stream itself is not missing: `AgentEvent` in `Lunate.Contracts` is a closed, stamped hierarchy
already emitted through `IAgentEvents`, rendered as the documented `--json` line stream
(`PrintEventJson`) and mapped into ACP (`AcpEventMapper`). #897 asks whether exposing it as
`IObservable<T>` is the right contract, which pulls two dependency and protocol questions with it:
BCL `IObservable` or Rx, and whether out-of-process extensions get the same hooks. Those are
architecture decisions (dependencies, protocol, public contract), so this card settles them in an
ADR draft with the comparison that justifies them, and an implementation card builds against it.
No code lands here.

## What Changes

- **A comparison** of Lunate's hook model with Pi's extension events and Claude Code's hooks: where
  each fires, what a handler may change, ordering, failure behaviour, blocking, and how each reaches
  an out-of-process handler.
- **The observing design**: one session-scoped, hot `IObservable<AgentEvent>` per extension context,
  delivered off the run's hot path through a bounded per-subscriber queue, error-isolated per
  subscriber, unable to veto or modify anything. Intercepting remains the existing typed hook
  catalogue — the two kinds share one contract assembly and one manifest, not one mechanism.
- **The decisions**: BCL `IObservable<T>`/`IObserver<T>` only, no Rx in the contract (ADR-0007 keeps
  System.Reactive at the `Lunate.Tui` boundary); an in-process extension may still depend on Rx
  itself, because the contract accepts any `IObservable<T>`; out-of-process parity is the manifest's
  declared hooks for intercepting and observer hooks today, plus a declared, filtered projection of
  the same event stream as wire notifications (protocol version 3) in the follow-up card; `--json`
  and ACP stay rendered projections of the same stream, never a second event model.
- **The ADR draft** (ADR-0040, proposed — maintainer sign-off) as the durable record.
- **No behaviour change**, so no spec delta: the requirements and their tests land with the
  implementation card, written against ADR-0040.

## Capabilities

### New Capabilities

None — this card produces a decision and a design, not behaviour.

### Modified Capabilities

None — no requirement changes; the implementation card adds them.

## Impact

- New: `adr/0040-hook-and-observable-extensibility.md` (proposed draft),
  `openspec/changes/design-hook-extensibility/**`, `changes/<pr>.md`.
- No `src/`, `tests/` or user-doc change. `docs/dev/spec/extensibility.md` gains the observing
  section in the implementation card, together with the tests that pin it.
- Dependencies: none. No new NuGet package; the contract names BCL `IObservable<T>` only.
- Protocol: no wire change here; the design names the protocol-3 extension point and its reason.
- Public API: none in this card. The implementation card bumps the contract as a semver minor.

## Out of scope

- Any `src/` or `tests/` change, a new hook, a new event type, a wire method, and the manifest field
  that a follow-up adds. Those are the implementation card's tasks, and its `tasks.md` is the place
  that keeps them honest.
