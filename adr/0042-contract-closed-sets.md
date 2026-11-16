# 0042 — The contract's closed sets are closed hierarchies, not union wrappers

- Status: accepted — 2026-11-16 (decision taken on #919, the card #192 deferred it to)
- Date: 2026-11-16
- Relates to: ADR-0002 (layering and MEAI model types), ADR-0017 and its addenda (extensibility
  architecture and the contract assembly), ADR-0040 (hook-based extensibility on observables),
  ADR-0041 (everything targets .NET 11)

## Context

Issue #192's inventory ([`docs/dev/csharp-15.md`](../../docs/dev/csharp-15.md)) adopted four C# 15 features
and deferred the two that touch the public contract: *"AgentEvent and hook-result unions come last,
as their own decision."* The retarget (ADR-0041) removed the `net10.0` floor that forbade union and
closed types on the contract surface, so the question became answerable, and #919 is the card it was
deferred to.

Both sets are already specified as closed. [`openspec/specs/agent-events/spec.md`](../../openspec/specs/agent-events/spec.md)
calls `AgentEvent` "a closed, sealed set of records", and the hook catalogue fixes the result of every
interceptor. Neither is closed in the compiler: an assembly outside `Lunate.Contracts` can derive
from `AgentEvent`, `ExtensionEvent` or a hook-result family, so a consumer's `switch` cannot be
exhaustive and a kind the contract never declared can reach it.

Two shapes would close them: the C# 15 `union` (a generated wrapper struct with implicit conversions
from each case and an `object` `Value`) or `closed` (the existing hierarchy with its direct
descendants fixed at compile time). The inventory named `AgentEvent` the motivating case for `union`
before the shape was measured; this record measures it.

## Decision

1. **`AgentEvent` and `ExtensionEvent` are `closed` records, not unions.** They carry state and
   classification on the base: `SessionId`, `ParentRunId` and `Source` are init properties the event
   channel stamps, and `ExtensionEvent` is the intermediate base the `--json`/ACP mappers and the
   tool-only sink filter on. A union wrapper has no base — its `Value` is `object` — so it would push
   three properties into 26 cases, delete the classification, and add a box and a cast to every emit
   and projection. `closed` fixes the case set, keeps the record shape, value equality and the
   stamped state, and makes a consumer's `switch` exhaustive (CS8509, an error in this repository).

2. **The seven hook-result families are `closed` records, not unions.** Same rule, plus a fact that
   disqualifies the wrapper on its own: `System.Text.Json` cannot deserialize a union without a custom
   type classifier, and the extensions specification requires every hook payload and result DTO to
   round-trip through `System.Text.Json`. The families also carry the `IHookResult` marker and a
   private constructor on the base, and their case names are the authoring surface.

3. **The shape rule, so the next family does not need this record.** A contract type is union-shaped
   when its cases are otherwise unrelated types with no shared base and no state on the base. A
   contract type is `closed`-shaped when a base record carries the family's marker, its state or its
   case names. Nothing on the contract surface is union-shaped today.

4. **Adding a case to a closed set is a contract change.** It moves the API version and carries a
   version note for extension authors, because an extension's exhaustive switch is what the closed set
   buys. This change rides `ExtensionApi.Current` `1.16.0`, which is unpublished.

## Consequences

- **A consumer can rely on the set.** `--json`, ACP, the TUI and an extension's observer switch are
  compiled against a complete set; a missing arm is a build error instead of a silent fall-through.
- **No public member moves.** `closed` is implicitly abstract, so PublicApiAnalyzers reports the same
  entries as for the `abstract` record it replaces; `Lunate.Contracts` and
  `Lunate.Extensibility.Abstractions` build with the analyzer's RS0016/RS0017 gate on and no edit.
- **One source break.** An extension that derived from one of these types no longer compiles. Nothing
  in `src/`, `extensions/`, `samples/` or the template does; the one test that did is re-expressed on
  a declared kind. The version note states it.
- **The inventory is corrected.** `docs/dev/csharp-15.md` and the architecture guide stop calling
  `AgentEvent` the union candidate and record what was measured, so the next reader does not repeat
  the assumption.
- **`union` stays available.** A future contract family of unrelated case types takes it — with a
  JSON classifier — and gets an ADR addendum only if it changes the rule above.

## Evidence

Measured on SDK `11.0.100` (`csc` 5.12.0), throwaway projects, `net11.0` + C# 15:

| Probe | Result |
| --- | --- |
| `public union Res(Res.A, Res.B);` reflection | `Res` is a struct implementing `IUnion`; `Value` is `object?`; one constructor per case; no `Equals`, `==` or `GetHashCode` of its own |
| `default(Res).Value` | `null` — a union has a no-case value |
| `JsonSerializer.Serialize(new Res.A("hello"), typeof(Res))` | `{"Text":"hello"}` — serialization works |
| `JsonSerializer.Deserialize<Res>("{\"Text\":\"hello\"}")` | `JsonException`: "JSON value type 'Object' is ambiguous for union type 'Res' because multiple case types can use this value type. Specify a custom type classifier" |
| `public closed record Ev(...)` with nested sealed cases | compiles; `IsClosedTypeAttribute` present; `IsAbstract` true |
| non-exhaustive `switch` over a closed base / over a union | CS8509 for both — the same exhaustiveness gain |
| PublicApiAnalyzers `5.6.0` on a closed record | the same entries as the `abstract` record (no `Value`, no per-case constructor) |
| PublicApiAnalyzers `5.6.0` on a union | five extra entries: the type, `Res()`, one constructor per case, `Value.get -> object?` |

## Alternatives considered

- **`union` for both families.** Rejected: it erases the base each family is built around, loses the
  record's value equality, cannot be JSON-deserialized without a classifier, and gains nothing over
  `closed` for exhaustiveness.
- **`union` for the hook results only, `closed` for the events.** Rejected: the hook-result families
  are the ones for which the JSON round-trip requirement is explicit, so they are the *worse* fit of
  the two.
- **Leave both open and rely on the consumer's `_` arm.** Rejected: it is what the contract has today,
  and it is why the spec's "closed, sealed set" is unenforced prose.
- **Close by convention — a test asserting no derived types.** Rejected: a test catches it after the
  fact on one branch; `closed` catches it at compile time in every consumer.
