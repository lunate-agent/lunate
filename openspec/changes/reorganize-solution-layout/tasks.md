## 1. The rule first

- [ ] 1.1 Write the layout rule into the `repo-quality` delta: folders group files by feature, a
      folder never changes a namespace, and a test project mirrors the folders of the project it
      tests.
- [ ] 1.2 Add the `Navigation` and `Layout` sections to `CONTRIBUTING.md`'s repository map and the
      rule to `docs/dev/architecture.md`, so a reader meets the rule before the folders.
- [ ] 1.3 Add `LayoutRuleTests` (red first): no namespace under `src/` ends with a folder segment of
      its own path, and every feature folder under a project is mirrored under its test project. The
      first run fails on `src/Lunate.Protocols/Acp` and `src/Lunate.Tui/Platform`.

## 2. Protocols

- [ ] 2.1 Fold `Lunate.Protocols.Acp` into `Lunate.Protocols` (7 files, `PublicAPI.Unshipped.txt`,
      every `using` in `src/`, `tests/` and `samples/`).
- [ ] 2.2 Move the MCP client files to `Mcp/Client/` and the server host files to `Mcp/Server/`.

## 3. Agent

- [ ] 3.1 Split `Lunate.Agent` into `Loop/`, `Sessions/`, `Decisions/` and `Tools/`.

## 4. Ai

- [ ] 4.1 Split `Lunate.Ai` into `Providers/`, `Families/`, `Catalog/`, `Requests/` and `Storage/`.
- [ ] 4.2 Folder `Lunate.Ai.Decisions` (client, transport, engine) and `Lunate.Ai.SystemOne`
      (client, mapping).

## 5. Contracts and Abstractions

- [ ] 5.1 Organize `Lunate.Contracts` into `Events/`, `Tools/`, `Models/`, `Decisions/`, `Runtime/`
      with no public API change.
- [ ] 5.2 Folder `Lunate.Extensibility.Abstractions` into `Manifest/`, `Hooks/`, `Mcp/`, `Ui/`,
      `Tools/`, `Services/`, `Commands/`, `Models/`, `Decisions/`.
- [ ] 5.3 Split the grab-bag files (`ExtensionApi.cs`, `Services.cs`, `CommandDescriptor.cs`,
      `WriteVerification.cs`) into one file per type, named after the type it declares.

## 6. Extensibility and its friends

- [ ] 6.1 Folder `Lunate.Extensibility` into `Host/`, `Loader/`, `SingleFile/`, `Packages/`,
      `Hooks/`, `Trust/`, `Settings/`, `Skills/`, `Registries/`, `Files/`.
- [ ] 6.2 Folder `Lunate.Extensibility.Remote`, `.Restore` and `.Testing` by concern.

## 7. Tui

- [ ] 7.1 Fold `Lunate.Tui.Platform` into `Lunate.Tui` (no public type).
- [ ] 7.2 Move key input to `Keys/`, the console seams to `Console/`, the theme types to `Theme/`, the
      renderers to `Rendering/`, and the test doubles (`FakeConsoleIO`) to `Testing/`.

## 8. Roslyn and Mermaid

- [ ] 8.1 Folder `Lunate.Roslyn` by feature: `Workspace/`, `Symbols/`, `Diagnostics/`, `Outline/`,
      `References/`, `Rename/`, `Backend/`, `Cache/`.
- [ ] 8.2 Folder `Mermaid.Terminal`: `Flowchart/`, `Sequence/`, `Graph/`, `Rendering/`.

## 9. Coding

- [ ] 9.1 Split `Lunate.Coding` (249 files) into feature folders: `Acp/`, `Bash/`, `Cli/`,
      `Commands/`, `Decisions/`, `Diagnostics/`, `Execution/`, `Export/`, `Extensions/`,
      `Interaction/`, `Mcp/`, `Models/`, `Print/`, `Process/`, `Providers/`, `Search/`,
      `Sessions/`, `Settings/`, `Setup/`, `Skills/`, `SystemPrompt/`, `Tools/`, `Tui/`,
      `Workspace/`.

## 10. Tests mirror src

- [ ] 10.1 Move each test file into the folder of its subject's src feature folder (type→src-file
      index over each test project); support doubles and collection definitions stay at the test
      project root; `fixtures/` keeps its path.
- [ ] 10.2 Build every test project and run the focused suites that cover a moved subject.

## 11. The decision layer's dead plumbing

- [ ] 11.1 Delete `Lunate.Ai.Decisions.DecisionRequestValidator` and its one call: the `Lunate.Agent`
      boundary already validates every request it sends. Proven by a focused test that the boundary
      still rejects an empty question text and a duplicated id.
- [ ] 11.2 Rename the internal `DecisionQuestionKind.Noul` to `YesNo`, the name the contract model
      uses, so the two models name the four kinds the same way.
- [ ] 11.3 Record the follow-up the card's "unify the decision type model" needs: one DTO set instead
      of two requires a new project edge (`Lunate.Ai.Decisions` → `Lunate.Contracts`), which
      `LayeringTests` enumerates and AGENTS.md draws. Not taken here: it is a dependency decision for
      the maintainer.

## 12. The public surface and packaging

- [ ] 12.1 Settle the public surface: no type, member or signature changes; `PublicAPI.Shipped.txt`
      untouched; `PublicAPI.Unshipped.txt` follows the `Acp` fold. No package, assembly or project
      change.
- [ ] 12.2 Prove it: `PublicApiTests`, `LayeringTests`, `TargetFrameworkTests`,
      `ContractTypeForwardingTests`, `PackagedApiGateTests` and the ACP suites stay green.

## 13. Finish

- [ ] 13.1 `dotnet csharpier format .`, the fast gate (Release build, the named suites,
      `openspec validate --all --strict`).
- [ ] 13.2 Write `changes/<pull request>.md` under the release's headings.
- [ ] 13.3 Archive as the last commit: `openspec archive reorganize-solution-layout --yes`.
