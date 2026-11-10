## 1. Spec

- [x] 1.1 Delta spec for the `ai-layer` Provider API families requirement states that the chat client
      factory refuses a family with no client; `openspec validate remove-dead-code-n1 --strict` passes.

## 2. Tests (red first)

- [x] 2.1 Add `tests/Lunate.Coding.Tests/DeadCodeInventoryTests.cs`: the TUI assembly has exactly one
      `*QuitWindow` type (`QuitWindow`), and the decisions assembly has no
      `DecisionClientServiceExtensions` type. Run red on the base commit.
- [x] 2.2 Add `A_decision_only_family_reaching_the_factory_is_refused_naming_the_family_and_the_wired_set`
      to `tests/Lunate.Ai.Tests/ChatClientFactoryFamilyTests.cs`, driving `ChatClientFactory.Create`
      with `apiFamily: systemone`.

## 3. Implement

- [x] 3.1 Delete `src/Lunate.Tui/Interaction/CtrlCQuitWindow.cs` (`CtrlCQuitWindow`, `CtrlCAction`).
- [x] 3.2 Delete `DecisionClientServiceExtensions.GetService<T>` from
      `src/Lunate.Ai.Decisions/DecisionClient.cs`, keeping `IDecisionClient.GetService(Type)`.
- [x] 3.3 Delete the pre-`switch` family guard in `ChatClientFactory.CreateProviderClient` and move its
      explanatory comment onto the `default` arm, which throws the same `NotImplementedFamily`.
- [x] 3.4 Rewrite the `ProviderAdapter` doc comment: three declared adapter kinds,
      `ProviderApiFamily` selects the client, and `OpenAiResponses`/`Google` have no declaring preset —
      no promise of a future slice.

## 4. Verify

- [x] 4.1 Focused tests green: `Lunate.Coding.Tests` dead-code inventory class, `ChatClientFactoryFamilyTests`,
      `QuitWindowTests`, `InteractiveSessionKeyTests`, plus every class referencing a changed type.
- [x] 4.2 Release build of the touched projects; `dotnet csharpier format .` then `dotnet csharpier check .`.
- [x] 4.3 `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`.
- [x] 4.4 Sync the `ai-layer` delta into `openspec/specs`, then `openspec archive remove-dead-code-n1 --yes`
      as the final commit of the branch.
