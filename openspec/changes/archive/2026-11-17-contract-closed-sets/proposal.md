## Why

#192 deferred the contract's union work to its own card: *"AgentEvent and hook-result unions come
last, as their own decision"*. The retarget ([ADR-0041](../../../adr/0041-net11-target-frameworks.md))
removed the `net10.0` floor that forbade union and closed types on the contract surface, so the
question is open — and the contract already promises a closed set that nothing in the compiler
enforces. [`openspec/specs/agent-events/spec.md`](../../specs/agent-events/spec.md) says `AgentEvent`
is "a closed, sealed set of records", and the hook-result families have the same intent, but today
any downstream assembly can derive from `AgentEvent`, `ExtensionEvent` or a hook-result family. A
consumer's `switch` therefore cannot be exhaustive, and an event or result the contract never
declared can reach it at run time.

## What Changes

- `AgentEvent` and `ExtensionEvent` become C# 15 `union`s over the sealed event records. The set of
  event kinds becomes a compile-time fact: a type outside `Lunate.Contracts` cannot add a kind, and a
  `switch` over an event is checked for exhaustiveness (CS8509 instead of a silent fall-through).
- The seven hook-result families — `ProjectTrustResult`, `InputReceivedResult`, `RunStartingResult`,
  `MessageCompletedResult`, `ToolCallingResult`, `TurnEndedResult`, `CompactingResult` — become
  `union`s over their nested case records for the same reason. `ContextBuildingResult` and
  `ToolResultReadyResult` are single-shape results and stay as they are.
- The stamped state (`SessionId`, `ParentRunId`, `Source`) stays on the `AgentEventBase` record the
  cases derive from, because a union declaration may not carry instance state; the union exposes it
  and `Kind`, keeps the record's value equality and `ToString`, and ships an `OfType<T>` overload so
  the repository's consumer idiom keeps working.
- **The wire does not move**: the `--json` serializer and the hook `outcome` codec project the case,
  so the discriminator and field names are identical, the golden session fixtures pass unchanged, and
  no JSON type classifier is needed. The hook conformance inventory gains one sample per union
  family.
- **BREAKING** for an extension author: the event type is a struct, so compare by value and read
  `Kind` where `GetType()` was used; nothing outside the contract can add a kind or an outcome.
  Every type name, case name, constructor and marker is unchanged.
- **Version note for extension authors** in `docs/extensions/README.md` and in the release notes:
  the event and hook-result sets are unions, matching them exhaustively is the intended shape, and
  adding a case is a contract change that carries its own note and API version.
- The decision is recorded as [ADR-0043](../../../adr/0043-contract-unions.md), which supersedes
  [ADR-0042](../../../adr/0042-contract-closed-sets.md) and links both ways; the C# 15 inventory and
  the architecture guide say what the code now does.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `agent-events`: the closed event set is a union over the event records, a kind outside the
  contract is impossible, and a consumer's switch is exhaustive by construction.
- `extensions`: the contract's closed sets (events and hook-result families) are unions, their wire
  is unchanged, and a change to the set is a contract change with a version note for extension
  authors.

## Impact

- Code: `src/Lunate.Contracts/AgentEvent.cs` (record bases, two unions, `OfType<T>` helper),
  `src/Lunate.Extensibility.Abstractions/Hooks/HookResults.cs` (seven unions), the event channel and
  the subagent runner (stamping through the record base), `ExtensionOnlyEventSink`, and the
  `GetType()` call sites that moved to `Kind`.
- Public API: the contract's event types and hook-result families change shape, so
  `PublicAPI.Unshipped.txt` moves for `Lunate.Contracts`, `Lunate.Extensibility.Abstractions` and the
  forwarding projects; the analyzer gate is green. `ExtensionApi.Current` stays `1.16.0`: that
  version is unpublished and this change rides it.
- Tests: `ContractUnionTests` asserts every family is a union over the contract's cases; the event
  tests assert the union's kind set, stamped state, value equality and `OfType<T>`; the hook
  conformance inventory covers each union family.
- Docs: `adr/0043-contract-unions.md`, `adr/0042-contract-closed-sets.md` (superseded),
  `docs/extensions/README.md`, `docs/dev/csharp-15.md`, `docs/dev/guide.md`, the delta specs, and
  `changes/921.md`.
- No new NuGet packages. No behaviour change in the loop, the wire or the JSON event stream.

## Intent decisions

`grill-me` was run in self-answer mode; each question is answered from the card, the specs and the
code.

1. **Do the unions go public as `union` types?** Yes — the card's decision. `AgentEvent`,
   `ExtensionEvent` and the seven hook-result families are unions over their case records, and
   ADR-0043 records the shape; ADR-0042's `closed` choice is superseded, not rewritten.
2. **Where does the stamped state go, now that a union has no state?** On `AgentEventBase`, the
   record the cases already derive from. A union declaration may not carry instance fields or
   auto-properties (CS9373), so the channel and the subagent runner stamp `with` on the record and
   convert to the union once; the union exposes the fields as computed properties.
3. **How do the readers keep working?** `OfType<T>` over a union never matches, so the contract ships
   an `OfType<T>(IEnumerable<AgentEvent>)` overload; `Kind` replaces `GetType()`; the union declares
   record-based `Equals`/`GetHashCode`/`==` and delegates `ToString` to the case, so log lines and
   fixture snapshots do not move.
4. **Does the wire stay byte-identical?** Yes. No union is serialized as a union on any wire: the
   `--json` serializer and the hook `outcome` codec build the JSON from the case, the golden session
   fixtures pass unchanged, and the hook conformance round-trip covers every case and every family.
5. **Is this a breaking contract change?** Yes for code that derived from a set or used
   `GetType()`/reference identity on an event; the version note states it. Construction and matching
   remain source-compatible.
6. **Does the API version move?** No. `ExtensionApi.Current` is `1.16.0` and unpublished; this change
   rides that minor. A later case addition is what the note reserves for the next version.
7. **Does the public API file change?** Yes — a union exposes the wrapper, one constructor per case
   and `Value` — and both contract projects build with RS0016/RS0017 as errors after the entries are
   updated.
8. **Does anything rely on the open set?** The one test that declared an out-of-contract event kind is
   re-expressed on a declared kind, because an out-of-contract kind is exactly what the change
   removes.
