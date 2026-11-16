# 0043 — The contract's closed sets are C# 15 unions

- Status: accepted — 2026-11-16 (decision taken on #919; supersedes
  [ADR-0042](0042-contract-closed-sets.md))
- Date: 2026-11-16
- Relates to: ADR-0002 (layering and MEAI model types), ADR-0017 and its addenda (extensibility
  architecture and the contract assembly), ADR-0040 (hook-based extensibility on observables),
  ADR-0041 (everything targets .NET 11)

## Context

Issue #192's inventory ([`docs/dev/csharp-15.md`](../../docs/dev/csharp-15.md)) adopted four C# 15
features and deferred the two that touch the public contract: *"AgentEvent and hook-result unions
come last, as their own decision."* The retarget (ADR-0041) removed the `net10.0` floor that forbade
union and closed types on the contract surface, so #919 is the card the decision was deferred to.

[ADR-0042](0042-contract-closed-sets.md) first answered it with `closed` hierarchies. The card's
decision supersedes that answer: the contract's sets are unions, because there are no external
extensions yet — the cheapest moment to fix the shape — and because a union is the shape the
contract's authoring surface already looks like (`new ToolCallingResult.Block("held")` produces a
case, the host matches the set). ADR-0042's measurements stand and are reused here; only the choice
between the two shapes changes.

The sets:

- `Lunate.Agent.AgentEvent` — 27 sealed event records, plus `ExtensionEvent`, the extension half
  (14 of them).
- The seven hook-result families in `Lunate.Extensibility.Abstractions` — `ProjectTrustResult`,
  `InputReceivedResult`, `RunStartingResult`, `MessageCompletedResult`, `ToolCallingResult`,
  `TurnEndedResult`, `CompactingResult`.

## Decision

1. **`AgentEvent` and `ExtensionEvent` are unions over the event records.** Every sealed event record
   derives from `AgentEventBase` (or `ExtensionEventBase`), which keeps the state the contract stamps
   centrally — `RunId`, `SessionId`, `ParentRunId`, `Source` — and the union declares the other 27
   kinds as its cases. The union exposes `RunId`, `SessionId`, `ParentRunId`, `Source` and `Kind` as
   computed properties so reading an event is unchanged, `Equals`/`GetHashCode`/`==` are value
   based (a record's equality), and `ToString()` is the record's, so a log line or a fixture
   snapshot is unchanged.

2. **The seven hook-result families are unions over their nested cases.** The case names stay the
   authoring surface (`ProjectTrustResult.Deny`, `ToolCallingResult.Block`), each case keeps the
   `IHookResult` marker, and the union implements it too, so a handler signature
   (`ValueTask<ToolCallingResult>`) and `new ToolCallingResult.Block("held")` compile as before.

3. **The wire does not move.** A union's `Value` is `object`, so a naive `JsonSerializer` call would
   either write the wrapper or need a type classifier. The contract keeps its projections: the
   `--json` serializer (`PrintEventJson`) and the hook `outcome` codec (`HookResultCodec`) already
   build the JSON from the case, so the discriminator and field names are byte-identical and the
   golden session fixtures pass unchanged. A union is never handed to `System.Text.Json` for
   deserialization; the hook protocol's hand-written codec stays the one reader of a result.

4. **Reading a union keeps the repository's consumer idiom.** `AgentEventExtensions.OfType<T>` projects
   the union to its record line, because `Enumerable.OfType<T>` over a union never matches; `Kind`
   replaces `GetType()`; value equality replaces reference identity. This is documented for extension
   authors in [`docs/extensions/README.md`](../../docs/extensions/README.md).

5. **Adding a case is a contract change.** It moves the API version and carries a version note for
   extension authors; this change rides `ExtensionApi.Current` `1.16.0`, which is unpublished.

## Consequences

- **A consumer can rely on the set.** `--json`, ACP, the TUI and an extension's switch are compiled
  against a complete set: a missing arm is a build error, not a silent fall-through.
- **The public API file moves.** A union's public surface is the wrapper, one constructor per case,
  `Value`, and the members above, so `PublicAPI.Unshipped.txt` gains those entries and marks the
  removed `abstract record` members; both contract projects build with the analyzer gate green.
- **One source break.** An extension cannot derive from `AgentEvent`, `ExtensionEvent` or a
  hook-result family (nothing could usefully, since the sets are declared closed), and code that used
  `GetType()`/`ReferenceEquals` on an event moves to `Kind`/value equality. The version note states
  both.
- **The record bases stay.** A union has no state, so `AgentEventBase`/`ExtensionEventBase` carry the
  stamped fields, and the event channel and the subagent runner stamp through the record and convert
  once. The alternative — moving the four fields into all 27 cases — is what ADR-0042 measured and
  rejected, and it is still rejected.
- **ADR-0042 is superseded, not deleted.** Its union-versus-closed measurements are the evidence this
  record leans on; both link to each other.

## Alternatives considered

- **`closed` hierarchies (ADR-0042).** Rejected by the card's decision: the contract's sets are the
  unions #192 deferred, and an extension author's surface is the case, not a base class.
- **A union with the stamped state on the union.** Rejected: the language forbids instance fields and
  auto-properties in a union declaration (CS9373), so the state has to live on the record base.
- **A `JsonConverter` for the union.** Rejected as unnecessary: no union value is serialized as a
  union on any wire, because both protocols project the case themselves and both fixtures and
  conformance tests prove it.
- **Leaving the sets open.** Rejected: it is the state before this card, and it is why "a closed,
  sealed set of records" in the spec was unenforced prose.

## Evidence

Measured on SDK `11.0.100` (`csc` 5.12.0), `net11.0` + C# 15:

| Probe | Result |
| --- | --- |
| `public union Ev(A, B)` over records with a shared base | compiles; `IUnion`, `Value: object`, one constructor per case |
| instance field or auto-property in a union body | CS9373 — a union carries no state |
| `e is CaseType`, `switch (e) { CaseType => … }` | work on the union; a non-exhaustive switch is CS8509 |
| `events.OfType<Case>()` over `IEnumerable<Ev>` | never matches; an `OfType<T>(IEnumerable<Ev>)` extension shadows it and matches |
| `Equals`/`==`/`GetHashCode` on the record line | not synthesized for a union; declared on the wrapper, they restore record equality |
| `JsonSerializer.Serialize(unionValue)` | writes the case's fields (no wrapper); deserialization needs a classifier, which no wire uses |
| PublicApiAnalyzers `5.6.0` on a union | five entries per family: the type, one constructor per case, `Value.get` |
