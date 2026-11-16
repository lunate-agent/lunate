## ADDED Requirements

### Requirement: The contract's closed sets are closed in the compiler

The hook-result families that head a closed set — `ProjectTrustResult`, `InputReceivedResult`,
`RunStartingResult`, `MessageCompletedResult`, `ToolCallingResult`, `TurnEndedResult` and
`CompactingResult` — SHALL be declared `closed` records implementing `IHookResult`. Their case
records SHALL stay the nested sealed records they are, so an extension author's construction surface
(`new ToolCallingResult.Block(reason)`) and its matching surface (`is ToolCallingResult.Block`) are
unchanged, and the host's dispatch switch SHALL be checked for exhaustiveness. `ContextBuildingResult`
and `ToolResultReadyResult` are single-shape results and stay plain records.

The contract SHALL NOT use a C# `union` for these families. A union wrapper's `Value` is `object`,
which erases the `IHookResult` marker and the families' value equality, turns `default` into a
no-case value, and cannot be deserialized by `System.Text.Json` without a custom type classifier —
while this specification requires every hook payload and result DTO to round-trip through
`System.Text.Json`. [`adr/0042-contract-closed-sets.md`](../../../adr/0042-contract-closed-sets.md)
records the measurements and the shape rule: a family whose cases are otherwise unrelated types with
no shared base and no shared state is union-shaped; every family on the contract today has a base
that carries its marker, its state or its case names, so it is `closed`-shaped.

Adding a case to a closed family SHALL be a contract change: it moves the API version and carries a
version note for extension authors in `docs/extensions/`, because an extension's exhaustive switch
is what the closed set buys.

#### Scenario: Every result family is closed in the compiler

- **GIVEN** the contract assembly
- **WHEN** the hook-result families are inspected
- **THEN** each carries the compiler's closed-type marker, each implements `IHookResult`, and every
  case is a sealed record nested in its family

#### Scenario: An extension's construction surface is unchanged

- **GIVEN** an extension handler that returns `new ToolCallingResult.Block("held")`
- **WHEN** it compiles against the closed family
- **THEN** it compiles unchanged and the host matches the case as before

#### Scenario: A result DTO still round-trips through JSON

- **GIVEN** a result of any declared family
- **WHEN** it is written and read through the hook wire
- **THEN** the hand-written codec round-trips it, because the family is a record and not a union
  wrapper that `System.Text.Json` cannot classify
