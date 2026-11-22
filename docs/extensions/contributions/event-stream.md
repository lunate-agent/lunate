# Event stream (observing)

A hook handler decides something. An observer only watches. `IExtensionContext.Events` is the
observing surface (ADR-0040): one subscription carries every `AgentEvent` of the session — text
fragments, tool arguments and results, approvals, usage, retries, extension events and every child
run — and nothing an observer does can change the run.

```csharp
public sealed class TurnLog(IExtensionLog log) : IObserver<AgentEvent>
{
    public void OnNext(AgentEvent value) => log.Info($"observed {value.Kind.Name} in {value.RunId}");

    public void OnError(Exception error) { }

    public void OnCompleted() => log.Info("the session ended");
}

public sealed class TurnLogFactory : IExtensionFactory
{
    public IExtension Create(IExtensionContext context)
    {
        // Registration only: the host owns the subscription and disposes it with the context.
        context.Events.Subscribe(new TurnLog(context.Log));
        return new TurnLogExtension();
    }
}
```

The type is `System.IObservable<AgentEvent>`, which is BCL, so the contract pulls in no reactive
library. An extension that wants operators references System.Reactive itself, exactly as it would in
any other project.

## The two surfaces do not overlap

| | Hook catalogue | Event stream |
| --- | --- | --- |
| Register with | `context.Register(handler)` | `context.Events.Subscribe(observer)` |
| Purpose | Intercept: observe, transform, replace, block | Observe only |
| Return value | A typed result per hook | None |
| Ordering | Priority, then load order | None across observers; emission order within one |
| Waits for the extension | Yes, with a per-handler timeout | No |
| Failure | The hook's declared policy | That one subscription is dropped |
| Declared in the manifest | `hooks` | `events` |

If a decision has to be made about a run, it belongs in a hook. If work is durable (writing a log,
exporting metrics, drawing a view), it belongs on the stream — and the work itself belongs in a
[background service](background-services.md), because `OnNext` must return quickly.

## Delivery

- **Every mode publishes.** The interactive session, `-p`/`--json` print mode and an ACP serve
  (`lunate --acp`) all feed the same stream, so an observer is written once for every frontend. ACP
  publishes each event before its own protocol filtering, so child-run events and lifecycle markers
  reach the stream even though `session/update` never carries them.
- **One queue per subscription.** The stream is hot and session-scoped. Each subscription has its
  own bounded queue (256 events) drained by its own pump, so a slow observer delays only itself.
- **Dropped, not delayed.** When a queue is full the oldest event is dropped. The first drop logs one
  warning naming the extension, the count keeps rising, and `/extensions` shows
  `events <seen> seen, <dropped> dropped`. A run never waits for a subscriber.
- **Errors are per subscription.** A throw from `OnNext` is caught, logged once naming the
  extension, and drops that subscription. The run and the other subscribers continue. `OnError` is
  never used — a failed run is a `RunError` event.
- **Completion once.** `OnCompleted` fires once per subscription, after the events already queued,
  at session end or when the extension unloads. Disposing the subscription stops it without a
  completion callback.
- **Child runs.** Subagent events arrive on the same subscription with the child's run id and
  `ParentRunId` set; filter on `ParentRunId` when the parent's transcript is all you want.
- **`OnNext` must return quickly.** Durable work belongs in a background service; decisions belong
  in a hook.

## The manifest declaration

An extension declares what it watches, so a surface can show it and an out-of-process host can
project it:

```json
{ "events": ["run_started", "tool_call_result", "usage_updated"] }
```

`["*"]` means every event; it is declared alone. The names are the `--json` event kinds
documented in [`docs/json-events.md`](../../json-events.md) — the same records, the same names.
The declaration is **information, not a permission**: an in-process subscription receives every
event regardless of what the manifest lists, and the declaration is never a trust gate. `/extensions`
shows it as `watches …`; an out-of-process extension's declaration becomes the wire's projection
filter with protocol version 3, which is a follow-up card. A remote manifest that declares `events`
today logs one warning saying the stream does not reach it yet.

A single-file extension declares the same list with `#:event <kind>`, or `#:event *`.

## Constraints

- **In-process only, for now.** The wire projection (protocol version 3, `event/` notifications, the
  proxy for the node-remote sample) is the follow-up card ADR-0040 names. The surfaces stay at
  parity: an observer is one subscription on either side, and neither can intercept.
- **Every event kind.** The stream carries the whole `AgentEvent` union, including `ApprovalRequested`
  and `UsageUpdated`. In-process extensions are fully trusted (ADR-0017), and the same data already
  leaves through hooks and `--json`.
- **No ordering across observers.** Two subscribers have no defined order between them; a decision
  about order is an interceptor hook's business.
- **The file-change bus is separate.** It predates this surface, keeps its handler-shaped
  subscription, and is not migrated (ADR-0040 records that inconsistency).

## Testing an observer

`Lunate.Extensibility.Testing`'s `ExtensionTestHost` publishes its runs to the same stream, so a test
can assert on what the extension observed, not only on what it logged. See
[testing.md](../testing.md).

## Related

- [hooks.md](hooks.md) — the intercepting surface
- [background-services.md](background-services.md) — where durable observer work belongs
- [ADR-0040](../../../adr/0040-hook-based-extensibility-on-observables.md) — the decision
- [`docs/dev/spec/extensibility.md`](../../dev/spec/extensibility.md#observable-event-stream) — the
  normative rules
