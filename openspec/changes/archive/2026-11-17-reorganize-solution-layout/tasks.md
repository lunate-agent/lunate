## 1. The rule first

- [x] 1.1 Write the layout rule into the `repo-quality` delta: folders group files by feature, a
      folder never changes a namespace, and a test project mirrors the folders of the project it
      tests.
- [x] 1.2 Add the `Navigation` and `Layout` sections to `CONTRIBUTING.md`'s repository map and the
      rule to `docs/dev/architecture.md`, so a reader meets the rule before the folders.
- [x] 1.3 Add `LayoutRuleTests` (red first): no namespace under `src/` ends with a folder segment of
      its own path, and every feature folder under a project is mirrored under its test project. The
      first run fails on `src/Lunate.Protocols/Acp` and `src/Lunate.Tui/Platform`.
- [x] 1.4 Move the scan into `LayoutRule` and check the mirror in both directions (red first: it
      fails on `Lunate.Extensibility.Testing/Remote`, `Mermaid.Terminal/Flowchart` and
      `Mermaid.Terminal/Graph`), reject a folder segment that reappears anywhere in a namespace, and
      give each half a regression case on a synthetic repository.

## 2. Protocols

- [x] 2.1 Fold `Lunate.Protocols.Acp` into `Lunate.Protocols` (7 files, `PublicAPI.Unshipped.txt`,
      every `using` in `src/`, `tests/` and `samples/`).
- [x] 2.2 Move the MCP client files to `Mcp/Client/` and the server host files to `Mcp/Server/`.

## 3. Agent

- [x] 3.1 Split `Lunate.Agent` into `Loop/`, `Sessions/`, `Decisions/` and `Tools/`.

## 4. Ai

- [x] 4.1 Split `Lunate.Ai` into `Providers/`, `Families/`, `Catalog/`, `Requests/` and `Storage/`.
- [x] 4.2 Folder `Lunate.Ai.Decisions` (client, transport, engine) and `Lunate.Ai.SystemOne`
      (client, mapping).

## 5. Contracts and Abstractions

- [x] 5.1 Organize `Lunate.Contracts` into `Events/`, `Tools/`, `Models/`, `Decisions/`, `Runtime/`
      with no public API change.
- [x] 5.2 Folder `Lunate.Extensibility.Abstractions` into `Manifest/`, `Hooks/`, `Mcp/`, `Ui/`,
      `Tools/`, `Services/`, `Commands/`, `Models/`, `Decisions/`.
- [x] 5.3 Split the grab-bag files (`ExtensionApi.cs`, `Services.cs`, `CommandDescriptor.cs`,
      `WriteVerification.cs`) into one file per type, named after the type it declares.

## 6. Extensibility and its friends

- [x] 6.1 Folder `Lunate.Extensibility` into `Host/`, `Loader/`, `SingleFile/`, `Packages/`,
      `Hooks/`, `Trust/`, `Settings/`, `Skills/`, `Registries/`, `Files/`.
- [x] 6.2 Folder `Lunate.Extensibility.Remote`, `.Restore` and `.Testing` by concern.

## 7. Tui

- [x] 7.1 Fold `Lunate.Tui.Platform` into `Lunate.Tui` (no public type).
- [x] 7.2 Move key input to `Keys/`, the console seams to `Console/`, the theme types to `Theme/`, the
      renderers to `Rendering/`, and the test doubles (`FakeConsoleIO`) to `Testing/`.

## 8. Roslyn and Mermaid

- [x] 8.1 Folder `Lunate.Roslyn` by feature: `Workspace/`, `Symbols/`, `Diagnostics/`, `Outline/`,
      `References/`, `Rename/`, `Backend/`, `Cache/`.
- [x] 8.2 Folder `Mermaid.Terminal`: `Flowchart/`, `Sequence/`, `Graph/`, `Rendering/`.

## 9. Coding

- [x] 9.1 Split `Lunate.Coding` (249 files) into feature folders: `Acp/`, `Bash/`, `Cli/`,
      `Commands/`, `Decisions/`, `Diagnostics/`, `Execution/`, `Export/`, `Extensions/`,
      `Interaction/`, `Mcp/`, `Models/`, `Print/`, `Process/`, `Providers/`, `Search/`,
      `Sessions/`, `Settings/`, `Setup/`, `Skills/`, `SystemPrompt/`, `Tools/`, `Tui/`,
      `Workspace/`.

## 10. Tests mirror src

- [x] 10.1 Move each test file into the folder of its subject's src feature folder (type→src-file
      index over each test project); support doubles and collection definitions stay at the test
      project root; `fixtures/` keeps its path. The three folders the mirror rule found get their
      tests: the remote wire schema test moves beside its generator, the flowchart parser and the
      graph layout get the unit tests their folders were missing.
- [x] 10.2 Build every test project and run the focused suites that cover a moved subject.

## 11. The decision layer's dead plumbing

- [x] 11.1 Delete `Lunate.Ai.Decisions.DecisionRequestValidator` and its one call: the `Lunate.Agent`
      boundary already validates every request it sends. Proven by a focused test that the boundary
      still rejects an empty question text and a duplicated id.
- [x] 11.2 Rename the internal `DecisionQuestionKind.Noul` to `YesNo`, the name the contract model
      uses, so the two models name the four kinds the same way.
- [x] 11.3 Take the merge with the maintainer's sign-off (ADR-0044): delete
      `Lunate.Contracts/Decisions/DecisionRequest.cs`, make `Lunate.Ai.Decisions`' model the one model
      (public request, question, kinds, answer, box and result), add the `Lunate.Ai.Decisions` →
      `Lunate.Contracts` edge, and keep only `DecisionProbability` in the contract because
      `DecisionEvaluated` publishes it.
- [x] 11.4 Point every consumer at the one model — the engine, the chat adapter, the System One
      transport, the parser and validator, the `decide` tool, the ask-user suggestions, the wizard
      check, the extension session and `IDecisionService` — and delete the conversions between the two
      sets, including the `1.15` answer-constructor compatibility test.
- [x] 11.5 Prove the merge: `Decision_projects_use_the_approved_dependency_edges`,
      `Contracts_does_not_reference_internal_decision_projects`,
      `The_abstractions_service_references_the_contract_and_the_decision_model_only`, the decision
      suites in `Lunate.Agent.Tests` and the System One replay fixtures stay green.

## 12. The public surface and packaging

- [x] 12.1 Settle the public surface: no shipped type, member or signature changes;
      `PublicAPI.Shipped.txt` is untouched; the unshipped entries follow the `Acp` fold and the
      decision-model merge. No assembly rename and no published package id change.
- [x] 12.2 Carry the decision model in the packages that ship it: `Lunate.Extensibility.Abstractions`
      references `Lunate.Ai.Decisions` privates and packs its file in `lib/`, the host-owned list and
      the embedded compile contract name it, and the extension host-assembly rule allows it.
- [x] 12.3 Prove it: `PublicApiTests`, `LayeringTests`, `TargetFrameworkTests`,
      `ContractTypeForwardingTests`, `PackagedApiGateTests`, the template's NuGet-mode build against a
      packed contract feed, and the ACP suites stay green.

## 13. Finish

- [x] 13.1 `dotnet csharpier format .`, the fast gate (Release build, the named suites,
      `openspec validate --all --strict`) — the full suite, `scripts/verify.sh`, is the maintainer's
      gate after the merge.
- [x] 13.2 Write `changes/<pull request>.md` under the release's headings.
- [x] 13.3 Archive as the last commit: `openspec archive reorganize-solution-layout --yes`.
