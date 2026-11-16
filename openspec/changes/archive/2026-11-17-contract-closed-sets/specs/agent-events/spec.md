## ADDED Requirements

### Requirement: The closed event set is a union

`AgentEvent` SHALL be a C# 15 `union` over the sealed event records, and `ExtensionEvent` SHALL be a
union over the extension half of them, so the set of event kinds is fixed at compile time. No
assembly outside `Lunate.Contracts` SHALL be able to declare an event kind, and a `switch` over an
`AgentEvent` that does not cover the set and carries no `_` arm SHALL fail the build (`CS8509` is an
error in this repository).

The stamped state the channel writes — `SessionId`, `ParentRunId` and `Source` — SHALL stay on the
`AgentEventBase` record the cases derive from, because a union declaration may not carry instance
state; the union SHALL expose it, and its `Kind`, as properties so reading an event does not change.
The event JSON and ACP projections SHALL be unchanged, and the event's record equality SHALL be
preserved. Adding an event kind SHALL be a contract change: it moves the API version and carries a
version note for extension authors, because a consumer's exhaustive switch is what makes the set
useful. [`adr/0043-contract-unions.md`](../../../adr/0043-contract-unions.md) records the shape and
supersedes [`adr/0042-contract-closed-sets.md`](../../../adr/0042-contract-closed-sets.md).

#### Scenario: The event roots are unions over the contract's kinds

- **GIVEN** the contract assembly
- **WHEN** `AgentEvent` and `ExtensionEvent` are inspected
- **THEN** both are unions whose case set is exactly the sealed event records the assembly declares

#### Scenario: A consumer's switch is checked for completeness

- **GIVEN** a `switch` over `AgentEvent` that covers every declared kind
- **WHEN** it is compiled
- **THEN** it compiles without a `_` arm, and a switch that misses a kind and has no `_` arm fails
  the build

#### Scenario: An unknown kind is handled by a declared arm

- **GIVEN** a consumer that handles a subset of the declared kinds
- **WHEN** it processes the stream
- **THEN** a `_` arm ignores the kinds it does not handle and the stream continues, and no kind
  outside the contract exists to reach it

#### Scenario: Reading an event is unchanged

- **GIVEN** an event the channel stamped a session id onto
- **WHEN** a consumer reads `RunId`, `SessionId`, `ParentRunId`, `Source` or `Kind`
- **THEN** it sees the record's values, and two equal records compare equal
