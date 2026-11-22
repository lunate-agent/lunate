## Why

ADR-0040 (accepted 2026-11-22, #982) decides that an extension must be able to watch a run without
being able to change it. Today an extension can only observe at the hook points that happen to be
observers and through the file-change bus; a logger, a metrics exporter or a UI has to register one
handler per point and still cannot see a text fragment, a tool's arguments, an approval request, a
usage update or a retry, because no hook exists for those. The typed hook catalogue stays the
intercepting surface; the run already produces one `AgentEvent` stream that `--json` and ACP render,
and that stream is what an observer wants.

## What Changes

- `IExtensionContext` gains `Events`, an `IObservable<AgentEvent>` (BCL only; no reactive package in
  the contract). Subscribing is registration-only and live for the session; it carries every run,
  every child run (ids intact, `ParentRunId` set) and every extension event.
- Each subscription gets its own bounded queue and pump, so a slow observer delays only itself. On
  overflow the oldest event is dropped, counted, warned once per subscription, and shown by
  `/extensions`. A throwing `OnNext` is caught, logged once naming the extension and drops that
  subscription; the run and the other subscribers continue. `OnCompleted` fires once per subscription
  at session end or when the extension unloads.
- The manifest gains `events`, the `--json` kind vocabulary or `["*"]`, mirrored by a single-file
  `#:event` directive. It is shown by `/extensions` and carried in the trust prompt as information,
  not a permission; in-process delivery is not filtered by it.
- The stream is fed by the front ends that iterate a run (the interactive session, print mode and an
  ACP serve); child-run events arrive through the parent's stream exactly as the hooks and `--json`
  see them, and ACP publishes before its own protocol filtering so even what it does not render
  reaches an observer.
- The contract moves a semver minor: `ExtensionApi.Current` and the contract package version go from
  `1.17.0` to `1.18.0`, with the `PublicAPI.Unshipped.txt` entries the analyzer requires. `1.17.0`
  went to the `todo` event and shipped in `0.6.0`, so this second contract change takes the next
  minor instead of reusing a published number.
- An out-of-process extension keeps the hook catalogue only. The `events` projection over protocol
  version 3, its conformance entry and the node-remote sample are the follow-up card ADR-0040 names;
  a remote manifest that declares `events` today logs one warning instead of silently ignoring it.
- Documentation for extension authors: a contribution page for the stream, the manifest and
  single-file directive rows, the normative extensibility spec, and the contract version note.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `extensions`: a session-scoped observable event stream (`IExtensionContext.Events`) next to the
  unchanged intercepting hook catalogue, with per-subscription bounded queues, drop counting, error
  isolation and completion; the manifest `events` declaration and its single-file `#:event` mirror;
  the `/extensions` counters and the trust-prompt information line; the `1.18.0` contract bump.

## Impact

- Projects: `Lunate.Extensibility.Abstractions` (interface, manifest, API version, PublicAPI),
  `Lunate.Extensibility` (the stream, the loader wiring, the header directive),
  `Lunate.Coding` (publishing from the run loops, `/extensions`, the trust prompt),
  `Lunate.Protocols` (the ACP prompt loop offers each event to the host's stream),
  `Lunate.Extensibility.Testing` (the test host feeds the stream), `Lunate.Contracts` and
  `Lunate.Decisions.Model` (contract version only).
- Packaging: no new package and no new dependency; the contract pair keeps publishing in lockstep.
- Tests: stream unit tests, manifest and header tests, the contract version tests, the `/extensions`
  view tests, a print-mode conformance test with a real installed observer extension.
- Docs and records: `docs/extensions/` (authoring page, contributions index, manifest, single-file,
  contract note), `docs/dev/spec/extensibility.md`, and the accepted
  `adr/0040-hook-based-extensibility-on-observables.md` this change implements.
