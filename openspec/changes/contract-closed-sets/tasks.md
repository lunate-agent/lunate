## 1. The decision

- [ ] 1.1 Write ADR-0042 (the contract's closed sets are `closed` hierarchies, not `union` wrappers) with the measured union/closed evidence and the shape rule for the next family.
- [ ] 1.2 Write the ADR review manifest for the change.
- [ ] 1.3 Record the decision in the proposal's **Intent decisions** list.

## 2. Tests (red first)

- [ ] 2.1 Add `ContractClosedSetTests`: `AgentEvent` and `ExtensionEvent` carry the compiler's closed-type marker, each hook-result family carries it too, every family implements `IHookResult`, and every case is a sealed record nested in (or derived from) its family. Fails before the change.
- [ ] 2.2 Re-express `AgentEventIdentityTests`' unknown-kind case on a declared kind plus a `_` arm, so the "a consumer ignores a kind it does not handle" requirement keeps its test without declaring a kind outside the contract.

## 3. The contract change

- [ ] 3.1 Declare `AgentEvent` and `ExtensionEvent` as `closed` records in `src/Lunate.Contracts/AgentEvent.cs`, keeping the stamped state on the base and the doc comment stating what the closed set guarantees.
- [ ] 3.2 Declare the seven hook-result families as `closed` records in `src/Lunate.Extensibility.Abstractions/Hooks/HookResults.cs`, keeping the nested cases, the marker interface and the private-constructor invariant.
- [ ] 3.3 Build `Lunate.Contracts`, `Lunate.Extensibility.Abstractions` and their consumers with the PublicApiAnalyzers gate on and record the result: no entry moves, or the entries that do.

## 4. Version note and docs

- [ ] 4.1 Add the version note for extension authors to `docs/extensions/README.md`: the event and hook-result sets are closed, matching exhaustively is the intended way to read them, an out-of-contract kind cannot be declared, and adding a case is a contract change with its own note.
- [ ] 4.2 Correct `docs/dev/csharp-15.md` and `docs/dev/guide.md`: `AgentEvent` and the hook-result families are `closed`-shaped, with the measured reason, and `union` remains the shape for unrelated case types.
- [ ] 4.3 Write the release-note fragment `changes/<pr>.md` under `### Changed`, carrying the version note for extension authors.

## 5. Finish

- [ ] 5.1 Run the named tests (`ContractClosedSetTests`, `AgentEventIdentityTests`, the hook/remote codec tests) and `dotnet csharpier format .`.
- [ ] 5.2 `openspec validate --all --strict`, then archive as the last commit: `openspec archive contract-closed-sets --yes`.
