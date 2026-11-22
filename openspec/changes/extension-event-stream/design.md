## Context

ADR-0040 (accepted 2026-11-22, #982) decides the observable hook surface: `IExtensionContext.Events`
as a BCL `IObservable<AgentEvent>` next to the unchanged typed hook catalogue. The run already
produces that stream — `AgentEventChannel` in `Lunate.Agent` feeds one `IAsyncEnumerable<AgentEvent>`
that every front end iterates — and `--json` and ACP render the same records. The constraint that
shapes the design is the layer graph: `Lunate.Agent` knows nothing about extensions, the hook seam is
a no-op interface the front end maps, and the extension host (`Lunate.Extensibility`, composed by
`Lunate.Coding`) is the only place extension-facing surfaces live.

In force and reviewed: ADR-0002 (the layer graph and MEAI model types), ADR-0007 (System.Reactive is
confined to the TUI live area; the harness keeps `IAsyncEnumerable<AgentEvent>`), ADR-0013 (the
harness surface), ADR-0017 and its addenda (the contract assembly, extensibility principles, the
remote host) and ADR-0043 (the contract's closed sets are unions). None of them moves.

## Goals / Non-Goals

**Goals**

- `IExtensionContext.Events` exists, is BCL-only, and never puts an observer on the run's path.
- A slow or throwing observer cannot slow, break or change a run, and its loss is visible.
- Delivery and completion are per subscription: one pump, one bounded queue, one `OnCompleted`.
- The manifest declares what an extension watches, mirrored by a single-file `#:event` directive,
  and the declaration is information — shown by `/extensions`, carried in the trust prompt.
- The intercepting hooks are untouched: same DTOs, ordering, timeouts and failure policies.
- The contract moves one semver minor with its PublicAPI entries.

**Non-Goals**

- Protocol version 3, the `event/` wire projection, the remote conformance entry and the node-remote
  sample (ADR-0040's follow-up card). A remote manifest that declares `events` gets one warning now.
- No filtering of in-process delivery by the manifest declaration; the declaration is a watch list,
  not a permission or a gate.
- No System.Reactive dependency in the contract, no operator library, no second event model.
- No change to `AgentEvent`, `IAgentEvents`, `--json` or ACP rendering.
- No migration of the file-change bus to the observable shape (ADR-0040 records that inconsistency).

## Decisions

### 1. The loader owns one stream; the front end feeds it

`ExtensionLoader` owns a `SessionEventStream` (public in `Lunate.Extensibility`, like
`FileChangeBus`), created in its constructor and handed to every `ExtensionContext` it creates. The
front ends that iterate a run call `ExtensionSession.Publish(AgentEvent)` for each event — the
interactive session's run loop, print mode's run loop and an ACP serve's prompt loop — and the test
kit's host does the same.
`ExtensionSession` is the extension host a front end already holds, so no new seam is needed in
`Lunate.Agent` and ADR-0007's "the harness keeps `IAsyncEnumerable<AgentEvent>`" stays literally
true. Alternative: an event-tap option on `AgentHarnessOptions`; rejected because it would put an
extension-shaped concern into the harness for no gain, and because the front ends already have the
extension host in hand.

Child runs need no extra work: `SubagentRunner` forwards child events onto the parent's channel with
`ParentRunId`, so the parent's loop publishes them like any other event. ACP composes the same
extension host (`AcpMode` composes an `ExtensionSession` for the whole serve), so it publishes too:
`AcpMode` hands the host's `Publish` to `LibAcpServer`, whose prompt loop offers every event it
receives before its own filtering — child-run events and lifecycle markers included, which
`session/update` never carries. Only the out-of-process surface stays with the wire follow-up.

### 2. One bounded queue and one pump per subscription

`Subscribe` returns a disposable subscription that owns a `Queue<AgentEvent>` under a lock, a
`SemaphoreSlim` wake-up and a pump task. `Publish` only enqueues; it never awaits, never calls
`OnNext` on the caller's thread and never throws. Capacity is 256 events per subscription, pinned as
one constant: large enough that a subscriber doing ordinary buffered work keeps up with a streamed
turn, small enough that a stuck observer cannot grow session memory. On overflow the oldest event is
dequeued and dropped, a per-subscription counter increments, and the first drop logs once naming the
extension and pointing at `/extensions`. Alternative: an unbounded channel (rejected — a stuck
observer grows without limit), a `Channel` with `DropOldest` (rejected — the drop count is not
observable, and the count is the point of the trade), and one shared pump (rejected — a slow
subscriber would delay the others).

### 3. Errors and completion are per subscription

The pump wraps `OnNext` in a catch-all: any exception logs once naming the extension, stops that
pump and drops the subscription. `OnError` is never used — a failed run is already a `RunError`
event — so a subscriber only ever sees `OnNext` and `OnCompleted`. Completion sets a flag and wakes
the pump; the pump drains what is already queued and then calls `OnCompleted` once. `CompleteAsync`
awaits the pumps with the caller's token and a bounded cap so session end cannot hang on a stuck
callback. Unload and a failed load complete that extension's subscriptions before the context is
marked disposed; session end completes all of them before the contexts are disposed, so a
completion callback can still log.

### 4. The manifest declaration is information

`ExtensionManifest.Events` is a string list parsed like the other declaration arrays: non-empty
strings, no duplicates, and `*` only when it is the sole value. The vocabulary is the `--json` kind
list, which the parser does not duplicate: an unknown name simply never matches on the wire and is
shown as written, so there is one vocabulary (`docs/json-events.md`) and no drift. In-process
delivery is not filtered by the declaration (ADR-0040: information, not a permission); `/extensions`
shows `<kinds>` or `*` and the subscription counters, and the trust question carries the watch list
as a detail line when declared. The single-file header gains `#:event` because the manifest-mirror
test makes a declaration list without a directive a failure — a single file must be able to declare
what a package can.

### 5. Remote extensions keep the hook catalogue only

`RemoteExtension` registers proxies through the same `ExtensionContext`, but the child process can
only observe over the wire, and the `event/` projection is protocol version 3 — the follow-up card.
This change logs one warning when a remote manifest declares `events`, so the declaration is not
silently ignored, and documents that the projection, the handshake refusal for a protocol-2 host and
the per-event caps land with that card. No protocol message, version or sample changes here.

### 6. Contract version and PublicAPI

`ExtensionApi.Current` and `Directory.Build.props` `ContractApiVersion` move from `1.17.0` to
`1.18.0` together, so the contract assemblies' version and the manifest gate stay one number. The
observable is BCL, so no package reference changes. The analyzer-required entries go to
`PublicAPI.Unshipped.txt`: `IExtensionContext.Events` and `ExtensionManifest.Events`.

`1.17.0` is taken: the `todo` event moved the contract to it in the same minor and `0.6.0`
published it, and a NuGet version cannot be republished. An extension built against this tree
would otherwise declare `apiVersion` `1.17.0` while the published `1.17.0` has no
`IExtensionContext.Events`, so the change takes the next minor — the note in
`docs/extensions/README.md` is under `1.18.0` for the same reason.

## Risks / Trade-offs

- **A subscriber that stops reading loses events** → deliberate (ADR-0040): the drop is counted,
  warned once and visible in `/extensions`, and the run keeps its pace.
- **A subscription's pump is a task per subscriber** → bounded by the number of extensions that
  subscribe (opt-in: no subscription, no queue, no task), which is the cost ADR-0040 accepted.
- **The declaration and in-process delivery can disagree** (a watch list wider or narrower than what
  the extension actually does) → accepted: the declaration is information for in-process extensions
  and becomes a projection filter only on the wire.
- **Handing a mutable event record to many observers** → `AgentEvent` is a union of immutable
  records; `ToolCallResult.Details` is already carried by `--json`, and in-process extensions are
  fully trusted (ADR-0017, ADR-0040).
- **Publishing from the front end misses a run** a front end forgets to publish → the two run loops
  are the only places a harness is iterated in production, and the conformance test drives one end to
  end; a future front end that composes the host must publish the same way (documented on
  `ExtensionSession.Publish`).

## Migration Plan

No stored data, no wire and no configuration migrate. An extension that never subscribes behaves
exactly as before; one that declares `events` gains a `/extensions` clause and a trust-prompt line.
Rollback is the revert of one change: the manifest field is unknown-field-ignored by older hosts, so
a manifest written for this change still parses on an older host.

## Open Questions

None. The wire follow-up (protocol version 3, `event/` notifications, the refusal path for a
protocol-2 host, the node-remote sample and the per-event caps) is ADR-0040's own follow-up card and
is explicitly out of scope here.
