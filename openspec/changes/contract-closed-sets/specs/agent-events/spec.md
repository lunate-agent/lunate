## ADDED Requirements

### Requirement: The closed event set is enforced by the compiler

`AgentEvent` and `ExtensionEvent` SHALL be declared `closed` records, so the set of event kinds is
fixed at compile time. No assembly outside `Lunate.Contracts` SHALL be able to declare an event kind,
and a `switch` over an `AgentEvent` that does not cover the set and carries no `_` arm SHALL fail the
build (`CS8509` is an error in this repository). `ExtensionEvent` SHALL be closed as well, because
closure is not transitive: closing the root fixes its direct descendants, not theirs.

The base record SHALL keep the state the channel stamps — `SessionId`, `ParentRunId` and `Source` —
and the event JSON and ACP projections SHALL be unchanged. Adding an event kind SHALL be a contract
change: it moves the API version and carries a version note for extension authors, because a
consumer's exhaustive switch is what makes the set useful.

The contract SHALL NOT use a C# `union` for this family. [`adr/0042-contract-closed-sets.md`](../../../adr/0042-contract-closed-sets.md)
records why: a union's `Value` is `object`, so a wrapper erases the base that carries the stamped
fields and the `ExtensionEvent` classification, and adds a box and a cast to every emit and
projection while buying no exhaustiveness that `closed` does not give.

#### Scenario: The event roots are closed in the compiler

- **GIVEN** the contract assembly
- **WHEN** `AgentEvent` and `ExtensionEvent` are inspected
- **THEN** both carry the compiler's closed-type marker and every concrete event type is sealed

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
