## 1. The decision

- [x] 1.1 Write ADR-0043 (the contract's closed sets are C# 15 unions) with the shape, the stamped-state base, the consumer idiom and the wire rule.
- [x] 1.2 Mark ADR-0042 superseded and link both ways; write the ADR review manifest for the change.
- [x] 1.3 Record the decision in the proposal's **Intent decisions** list.

## 2. Tests (red first)

- [x] 2.1 Add `ContractUnionTests`: every family is a union over the contract's cases, the event union covers every sealed record, the extension union is the extension half, an event reports its `Kind` and carries the stamped state, and a union result matches its case.
- [x] 2.2 Re-express the event tests on the union: the kind set, the sealed-record count, the extension filter and value equality, so the "a consumer ignores a kind it does not handle" requirement keeps its test without declaring a kind outside the contract.

## 3. The contract change

- [x] 3.1 Declare `AgentEvent` and `ExtensionEvent` as unions over the event records in `src/Lunate.Contracts/AgentEvent.cs`, with `AgentEventBase`/`ExtensionEventBase` carrying the stamped state, `Kind`, record equality and the `OfType<T>` overload.
- [x] 3.2 Stamp through the record base in `AgentEventChannel` and `SubagentRunner`, and filter the tool sink on `ExtensionEventBase`.
- [x] 3.3 Declare the seven hook-result families as unions over their nested case records in `src/Lunate.Extensibility.Abstractions/Hooks/HookResults.cs`, each case keeping `IHookResult`.
- [x] 3.4 Keep the wire a projection of the case: no union reaches `System.Text.Json`, and `RemoteConformance` gains one union-typed sample per family.
- [x] 3.5 Build the contract projects and their consumers with the PublicApiAnalyzers gate on and update `PublicAPI.Unshipped.txt` for every affected project.

## 4. Version note and docs

- [x] 4.1 Rewrite the version note in `docs/extensions/README.md` for extension authors: the sets are unions, exhaustive matching is the intended way to read them, the wire is unchanged, adding a case is a contract change with its own note.
- [x] 4.2 Correct `docs/dev/csharp-15.md` and `docs/dev/guide.md`: the contract's sets are unions, the stamped state stays on the record base, and the `OfType<T>`/`Kind` facts are recorded.
- [x] 4.3 Write the release-note fragment `changes/921.md` under `### Changed`, carrying the version note for extension authors.

## 5. Finish

- [x] 5.1 Run the named tests (`ContractUnionTests`, `AgentEventTests`, `AgentEventIdentityTests`, `AgentEventChannelTests`, `EventSequenceValidatorTests`, the hook-runner semantics and remote conformance tests) and `dotnet csharpier format .`.
- [x] 5.2 `openspec validate --all --strict`, then archive as the last commit: `openspec archive contract-closed-sets --yes`.
