## ADDED Requirements

### Requirement: The contract's closed sets are unions

The hook-result families that head a closed set — `ProjectTrustResult`, `InputReceivedResult`,
`RunStartingResult`, `MessageCompletedResult`, `ToolCallingResult`, `TurnEndedResult` and
`CompactingResult` — SHALL be C# 15 `union`s implementing `IHookResult` over their case records. The
cases SHALL stay the nested sealed records they are, each carrying the `IHookResult` marker, so an
extension author's construction surface (`new ToolCallingResult.Block(reason)`) and its matching
surface (`is ToolCallingResult.Block`) are unchanged, and the host's dispatch switch SHALL be checked
for exhaustiveness. `ContextBuildingResult` and `ToolResultReadyResult` are single-shape results and
stay plain records.

A union value SHALL NOT be serialized as a union on any wire: the remote protocol's `outcome` codec
SHALL keep writing the case, so the hook JSON stays byte-identical, and no JSON type classifier SHALL
be needed. The wire-conformance inventory SHALL cover each union family as well as each case.
[`adr/0043-contract-unions.md`](../../../adr/0043-contract-unions.md) records the decision and
supersedes [`adr/0042-contract-closed-sets.md`](../../../adr/0042-contract-closed-sets.md).

#### Scenario: A handler returns a case and the host matches the union

- **GIVEN** an extension whose tool-calling handler refuses a call
- **WHEN** it answers with `new ToolCallingResult.Block("held")`
- **THEN** the host's `switch` matches `ToolCallingResult.Block` and reads the reason

#### Scenario: A hook result family is a union over its cases

- **GIVEN** the contract assembly
- **WHEN** a hook-result family is inspected
- **THEN** it is a union whose case set is exactly the nested case records it declares, and every
  case implements `IHookResult`

#### Scenario: The hook wire is unchanged

- **GIVEN** a result of any declared case
- **WHEN** it is encoded and decoded over the remote protocol
- **THEN** the JSON is the `outcome` object with the case's fields, exactly as before
