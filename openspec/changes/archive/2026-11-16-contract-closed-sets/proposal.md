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

- `AgentEvent` and `ExtensionEvent` become `closed` records. The set of event kinds becomes a
  compile-time fact: a type outside `Lunate.Contracts` cannot add a kind, and a `switch` over an
  event is checked for exhaustiveness (CS8509 instead of a silent fall-through).
- The seven hook-result families — `ProjectTrustResult`, `InputReceivedResult`, `RunStartingResult`,
  `MessageCompletedResult`, `ToolCallingResult`, `TurnEndedResult`, `CompactingResult` — become
  `closed` records for the same reason. `ContextBuildingResult` and `ToolResultReadyResult` are
  single-shape results and stay as they are.
- **No `union` type is introduced on the public contract**, and the decision, its measurements and
  the shape rule are recorded as [ADR-0042](../../../adr/0042-contract-closed-sets.md). A union
  wrapper was measured and rejected for these families: its `Value` is `object`, so it erases the
  base record (the stamped `SessionId`/`ParentRunId`/`Source` on an event, the `IHookResult` marker
  on a result), loses the record's value equality, cannot be deserialized by `System.Text.Json`
  without a custom classifier — and the extensions spec requires every payload and result DTO to
  round-trip through `System.Text.Json` — and buys no exhaustiveness that `closed` does not already
  give.
- **BREAKING** for an extension author: an extension can no longer declare a subclass of
  `AgentEvent`, `ExtensionEvent` or a hook-result family. Nothing in `src/`, `extensions/`,
  `samples/` or the template does this; the one place in the repository that did is a test whose
  subject is the guarantee itself.
- **Version note for extension authors** in `docs/extensions/README.md` and in the release notes:
  the event and hook-result sets are closed, exhaustive matching is the intended way to read them,
  and adding a case is a contract change that carries its own note and API version.
- The C# 15 inventory ([`docs/dev/csharp-15.md`](../../../docs/dev/csharp-15.md)) and the
  architecture guide stop calling `AgentEvent` the union candidate and record what was decided and
  measured instead.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `agent-events`: the closed event set is enforced by the compiler (`closed`), a kind outside the
  contract is impossible, and a consumer's switch is exhaustive by construction.
- `extensions`: the contract's closed sets (events and hook-result families) are closed in the
  compiler, and a change to the set is a contract change with a version note for extension authors.

## Impact

- Code: `src/Lunate.Contracts/AgentEvent.cs` (two type declarations),
  `src/Lunate.Extensibility.Abstractions/Hooks/HookResults.cs` (seven type declarations), and the
  one test that declared an out-of-contract event kind
  (`tests/Lunate.Agent.Tests/AgentEventIdentityTests.cs`).
- Public API: no member moves. `closed` is implicitly abstract and the analyzer reports the same
  entries as for the `abstract` record it replaces (checked by building `Lunate.Contracts` and
  `Lunate.Extensibility.Abstractions` with the PublicApiAnalyzers gate on). `ExtensionApi.Current`
  stays `1.16.0`: that version is unpublished and this change rides it.
- Tests: a new `ContractClosedSetTests` asserts the compiler marker on every family and that every
  case is sealed; the unknown-kind scenario is re-expressed on a declared kind so the "a consumer
  ignores an event kind it does not handle" requirement keeps its test.
- Docs: `adr/0042-contract-closed-sets.md`, `docs/extensions/README.md`, `docs/dev/csharp-15.md`,
  `docs/dev/guide.md`, the delta specs, and `changes/<pr>.md`.
- No new NuGet packages. No behaviour change in the loop, the wire or the JSON event stream.

## Intent decisions

`grill-me` was run in self-answer mode; each question is answered from the card, the specs and the
code.

1. **Do the unions go public as `union` types?** No — the contract gets `closed` records, and that
   is the decision this card was created to take. Measured on SDK 11.0.100: a union wrapper's `Value`
   is `object`, so the family's base record is erased and the base is exactly what carries the
   family's meaning here (the stamped run/session fields on an event, the `IHookResult` marker on a
   result); `System.Text.Json` cannot deserialize a union without a custom classifier while the
   extensions spec requires every result DTO to round-trip; and `closed` gives the same
   compiler-checked case set with no shape change. ADR-0042 records the measurements.
2. **Why not a union for `AgentEvent` if #899 calls it "the motivating case"?** Because the inventory
   was written before the shape was measured. `AgentEvent` carries `SessionId`, `ParentRunId` and
   `Source` as base-record init properties stamped centrally by the event channel, plus an
   intermediate `ExtensionEvent` base that the mappers and the tool-only sink filter on. A union
   wrapper has neither, so it would push three properties into 26 cases and delete the
   classification. The inventory is corrected rather than followed.
3. **Does every family become `closed`, or only some?** All nine types that head a closed set: the two
   event bases and the seven hook-result families. `ContextBuildingResult` and
   `ToolResultReadyResult` are single-shape records and have nothing to close.
4. **Is this a breaking contract change?** Yes, for an extension that derives from one of these types;
   it no longer compiles. Nothing in this repository outside one test does it, and the sets were
   documented as closed/union-ready anyway, so the change publishes the rule that was already
   intended. The version note states it for extension authors.
5. **Does the API version move?** No. `ExtensionApi.Current` is `1.16.0` and unpublished; this change
   rides that minor (ADR-0040 already announced the hooks minor). A later case addition is what the
   note reserves for the next version.
6. **Does the public API file change?** It was checked, not assumed: the projects build with
   RS0016/RS0017 as errors and no entry moves, because `closed` is implicitly abstract and
   PublicApiAnalyzers reports the same entries as for `abstract`. The answer is recorded in the
   design and the release note rather than left implicit.
7. **Does anything rely on the current open set?** One test declares `UnrecognizedEvent :
   ExtensionEvent` to prove a consumer ignores an unknown kind. The requirement it tests (consumers
   ignore kinds they do not handle) stays; the test is re-expressed on a declared kind plus a `_`
   arm, because an out-of-contract kind is exactly what the change removes.
8. **Is a union still allowed on the contract later?** Yes, by the same rule: a family whose cases are
   otherwise unrelated types with no shared base and no shared state is union-shaped. Nothing on the
   contract surface is today.
