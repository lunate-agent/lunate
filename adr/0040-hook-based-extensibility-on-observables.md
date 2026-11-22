# 0040 — Hook-based extensibility: typed intercepting hooks plus one observable event stream

- Status: accepted — 2026-11-22 (maintainer sign-off on #982)
- Date: 2026-11-15
- Relates to: ADR-0017 (extensibility architecture and its addenda), ADR-0002 (layering and MEAI model
  types), ADR-0007 (System.Reactive confined to the TUI live area), ADR-0013 (the agent loop and harness
  surface)

## Context

ADR-0017 gave Lunate one extension mechanism: a semver'd contract assembly with a typed hook catalogue.
Every catalogue hook declares observe, transform, replace or block; `HookRunner` orders handlers by
priority and load order, awaits each with a per-handler timeout and applies a declared failure policy.
The agent loop never depends on the extension system — it calls no-op seam points that an adapter maps
into hook DTOs — and an out-of-process extension gets the same catalogue over JSON-RPC (ADR-0017
addendum, remote host).

That covers *intercepting*. It does not cover *watching*. An extension can observe only at the hook
points that happen to be observers (`ProviderStreamEvent`, `SessionStarted`/`SessionEnding`,
`RunSettled`, `ModelChanged`, `ToolsChanged`) and through the file-change bus. A logger, a metrics
exporter or a UI must register one handler per point and still cannot see a text fragment, a tool's
arguments, an approval request, a usage update or a retry, because no hook exists for those.

The stream those consumers want already exists. `AgentEvent` (`Lunate.Contracts`) is a C# 15 union over
its case records (ADR-0043), the case records carrying `RunId`, `SessionId`, `ParentRunId` and `Source`,
emitted through `IAgentEvents`; the `--json` mode renders each as one documented JSON line and ACP maps
the same records.
So the question is not how to produce a stream but which contract exposes it — and that question drags
two dependency and protocol decisions with it (BCL `IObservable` or System.Reactive; whether
out-of-process extensions get the same surface), which is why it needs a record rather than a card-local
choice.

Two reference models set the bar. Pi exposes ~40 typed events through `pi.on()` in an in-process
TypeScript extension; handlers are awaited in load order, notify or transform, `tool_call` may mutate
input in place or return `{ block, reason }`, and a handler failure is reported with `tool_call` failing
safe. Claude Code exposes a settings-driven catalogue of 30+ events whose handlers are external
commands, HTTP endpoints, MCP tools, prompts or agents, matched by pattern, fed JSON on stdin, answering
through exit codes (2 blocks) or JSON (`permissionDecision`, precedence `deny > defer > ask > allow`),
run in parallel with per-hook timeouts. Both are mostly *interception* catalogues with an observation
side effect; neither is a plain observable over a typed event stream.

In force and reviewed: ADR-0002 (the layer graph and MEAI model types — the stream type
`AgentEvent` is a `Lunate.Contracts` type, and no parallel message type may be introduced),
ADR-0007 (System.Reactive is confined to the `Lunate.Tui` live area; `Lunate.Agent` keeps
`IAsyncEnumerable<AgentEvent>`), ADR-0013 (the harness surface), ADR-0017 and its addenda (the contract
assembly, extensibility principles, the remote host). None of them decides the observing surface; none
is superseded here.

## Decision

1. **Two extension surfaces, one contract assembly.** Intercepting stays the typed hook catalogue:
   `IHookHandler` subclasses, typed payload/result DTOs, `HookRunner` ordering, timeouts and failure
   policies. Observing becomes `IExtensionContext.Events`, an `IObservable<AgentEvent>`. They are not
   one mechanism: an observer has no result, no ordering and no place on the run's path, and modelling
   observation as hooks would need a payload DTO and a wire round-trip per event type. Both live in
   `Lunate.Extensibility.Abstractions`, both are declared in the manifest, both are reached through the
   same `IExtensionContext` registration path.

2. **BCL `IObservable<T>` only; Rx is never a dependency of the contract.** The contract names
   `System.IObservable<AgentEvent>`, which is BCL, so it adds no package and pins no reactive library.
   An extension that wants operators takes System.Reactive itself; `Lunate.Tui` keeps its Rx dependency
   at the live-area boundary, as ADR-0007 requires. A pull-shaped `IAsyncEnumerable` was rejected for
   the extension surface because the harness has one reader by design and a puller would either block
   the loop or buffer it without a bound.

3. **The stream is hot, session-scoped and off the run's hot path.** One subscription per extension,
   live for the session, carrying every run, every child run (ids intact, `ParentRunId` set) and every
   extension event. Events reach each subscriber through its own bounded queue drained by its own pump,
   so a slow subscriber delays only itself; on overflow the oldest event is dropped, counted, warned
   once and shown by `/extensions`. `OnNext` must return quickly: durable work belongs in a background
   service and work that must decide something belongs in an interceptor hook. Subscribing is
   registration-only and is disposed with the extension's context.

4. **Error isolation and completion are per subscription.** A throw from any observer callback is
   caught, logged once naming the extension, and drops that subscription; the run and the other
   subscribers are unaffected. `OnError` is never the failure channel for a run (a failed run is a
   `RunError` event); `OnCompleted` fires once, at session end or on unload. There is no priority over
   observers; ordering that matters is an interceptor hook's business.

5. **The manifest declares what an extension watches, and parity is a projection.** The manifest gains
   `events` — the `--json` kind vocabulary, or `["*"]` — shown by `/extensions` and carried in the trust
   prompt as information, not a permission. Out-of-process extensions already have the hook catalogue at
   parity (interceptors as requests, observers as notifications, one implementation, the same failure
   policies). The event stream reaches them as a declared projection over protocol version 3: `event/`
   notifications carrying the same JSON line object `--json` writes, never awaited, same bounded drop,
   and an `events` declaration against a protocol-2 host is refused at the handshake rather than ignored.
   `--json` and ACP remain rendered projections of the same `AgentEvent` records; there is no second
   event model, and an observer never writes to stdout.

## Consequences

- **An extension can watch a run with one subscription.** A logger, a metrics exporter or a UI stops
  re-registering a handler per hook and sees events that have no hook. The cost is opt-in: no
  subscription, no queue, no thread.
- **Interception keeps its guarantees.** Ordering, timeouts, failure policies and "the user approves the
  final arguments" are unchanged; nothing about this ADR moves an interceptor off the run's path.
- **The contract moves a semver minor.** `IExtensionContext.Events`, the manifest `events` field and the
  API version bump land together with the PublicAPI entries and the existing parity test. Extensions
  that never subscribe behave exactly as before.
- **A slow observer loses events instead of slowing the run.** That is a deliberate trade against Pi
  (awaited, stream-delaying handlers) and Claude Code (a process per hook); the drop is visible, not
  silent.
- **There is a follow-up wire card.** Protocol version 3, its conformance entry, the `node-remote`
  sample and the per-event caps are not part of #897; the decision here binds them, and the refusal path
  for a protocol-2 host must be implemented with them.
- **The file-change bus keeps its handler-shaped subscription.** It predates this decision and is not
  migrated; the inconsistency is accepted and recorded rather than hidden.
- **In-process trust is unchanged.** The stream exposes tool arguments and outputs that hooks and
  `--json` already expose; in-process extensions are fully trusted and a process boundary is runtime
  isolation, not a permission boundary (ADR-0017).

## Alternatives considered

- **Express the stream as hooks** (`AgentObserved`-style payload per event type): rejected — a payload
  DTO, a wire entry and a conformance case per event type, modelling something with no result and no
  ordering, and it would put observers on the awaited dispatch path.
- **`System.Reactive` in the contract**: rejected — it forces every extension author to pin Rx, drags it
  into the apphost and startup budget, and contradicts ADR-0007's confinement. An extension may still
  use Rx privately, because any Rx observable is an `IObservable<T>`.
- **Host-only observables, no remote stream**: rejected — the manifest would promise parity the wire
  cannot deliver; a remote extension could not implement a logger that an in-process one can.
- **Awaiting observers** (Pi's model): rejected — `ProviderStreamEvent` is the awaited, budgeted
  observation point; awaiting every frame of the stream would let an extension pace the model.
- **Unbounded queues**: rejected — an extension that stops reading would grow session memory without
  limit.
- **Not declaring `events` in the manifest**: rejected — a remote filter needs a declaration, and
  `/extensions` should be able to show what an extension watches, as it already does for hooks.
