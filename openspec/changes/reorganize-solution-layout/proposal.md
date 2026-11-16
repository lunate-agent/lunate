## Why

Every project under `src/` is a flat directory of 8–249 C# files. A reader looking for the MCP
client, the session store or the tool implementations scans a hundred file names, and the test
projects mirror that flatness, so a reviewer cannot tell from a path whether two files belong to the
same feature. Issue #695 gathered thirteen separate layout issues (#539–#551) into one change because
each is a file move, none ships alone, and together they are one structural step for 0.4.

The repository already answers the hard question — what a move may change — twice over:
`src/Lunate.Contracts` declares types in the `Lunate.Agent`, `Lunate.Ai` and
`Lunate.Extensibility.Abstractions` namespaces and carries `TypeForwardedTo` attributes so a
consumer's compiled reference keeps resolving, and `Lunate.Protocols/Acp` and `Lunate.Tui/Platform`
are the only two folders that add a namespace segment. The layout rule this change picks follows the
first: **a folder groups files by feature; a folder never changes a file's namespace.** A move is
therefore a move, not a rename of the public surface, and the extension SDK's namespaces stay put.

## What Changes

- One layout rule, written down in the `repo-quality` spec and enforced by a source-scanning test: a
  file's namespace is its assembly's established namespace, never derived from its folder; and a
  test project mirrors the folder of the project it tests.
- The two folders that broke the rule fold back into their project namespace:
  `Lunate.Protocols.Acp` → `Lunate.Protocols` (its types are still in `PublicAPI.Unshipped.txt`) and
  `Lunate.Tui.Platform` → `Lunate.Tui` (no public type). **BREAKING** for a host that composes
  `IAcpServer` directly; no shipped assembly is affected.
- Each project under `src/` is split into feature folders, one slice per project:
  - `Lunate.Protocols`: `Acp/`, `Mcp/Client/`, `Mcp/Server/`.
  - `Lunate.Coding`: feature folders for the CLI, the interactive session, tools, search, bash, the
    settings store, setup, providers/auth, sessions/export, skills, extensions, MCP, print mode,
    processes and execution environments, decisions, diagnostics and workspace.
  - `Lunate.Agent`: `Loop/`, `Sessions/`, `Decisions/`, `Tools/`.
  - `Lunate.Ai`: `Providers/`, `Families/`, `Catalog/`, `Requests/`, `Storage/`.
  - `Lunate.Ai.Decisions` and `Lunate.Ai.SystemOne`: one folder per concern (client, transport,
    mapping).
  - `Lunate.Contracts`: `Events/`, `Tools/`, `Models/`, `Decisions/`, `Runtime/` — file moves only,
    no public API change.
  - `Lunate.Extensibility.Abstractions`: `Manifest/`, `Hooks/`, `Mcp/`, `Ui/`, `Tools/`,
    `Services/`, `Commands/`, `Models/`, `Decisions/`; the grab-bag files (`ExtensionApi.cs`,
    `Services.cs`, `CommandDescriptor.cs`, `WriteVerification.cs`) split into one file per type
    family.
  - `Lunate.Extensibility` and its friends (`Extensibility.Compile`, `.Remote`, `.Restore`,
    `.Testing`): `Host/`, `Loader/`, `SingleFile/`, `Packages/`, `Hooks/`, `Trust/`, `Settings/`,
    `Skills/`, `Registries/`, `Files/` (and the equivalent per friend).
  - `Lunate.Tui`: `Keys/`, `Console/`, `Theme/`, `Rendering/`, `Testing/`, alongside the existing
    `Interaction/`, `Transcript/`, `ToolBlocks/`, `Markdown/`, `Mermaid/`, `Platform/`.
  - `Lunate.Roslyn`: `Workspace/`, `Symbols/`, `Diagnostics/`, `Outline/`, `References/`, `Rename/`,
    `Backend/`, `Cache/`.
  - `Mermaid.Terminal`: `Flowchart/`, `Sequence/`, `Graph/`, `Rendering/`.
- The decision layer loses its dead plumbing: the per-request validator inside
  `Lunate.Ai.Decisions` duplicates checks the `Lunate.Agent` boundary already performs on every
  request, and the internal question-kind name `Noul` becomes `YesNo`, the name the contract model
  already uses. The full merge of the two decision DTO sets needs a new project reference
  (`Lunate.Ai.Decisions` → `Lunate.Contracts`), which is a dependency decision for the maintainer;
  this change records it as the follow-up instead of taking it.
- Tests mirror `src`: each test project gains the same feature folders as the project it tests, and
  test files (and their supporting doubles) move into the folder of the feature they exercise.
- `docs/dev/architecture.md`, `CONTRIBUTING.md` and the `repo-quality` spec name the rule and the
  paths that exist now.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `repo-quality`: a new requirement pins the folder/namespace rule (a folder groups files by feature
  and never changes a namespace; a test project mirrors the folders of the project it tests) and a
  source-scanning test enforces it, so the next folder that tries to become a namespace fails the
  fast gate.

## Impact

- `src/**`: file moves (about 800 C# files into feature folders) and the two namespace folds; no
  type, member or signature moves, no project moves, no csproj change, no package change.
- `tests/**`: file moves mirroring `src` (about 950 files); `tests/fixtures/**` and
  `tests/Lunate.Coding.Tests/fixtures/**` stay where they are.
- `Lunate.Ai.Decisions`: one deleted validator file, its one call site, and the `Noul` → `YesNo`
  rename inside the internal model.
- Public API: `Lunate.Protocols.Acp.*` becomes `Lunate.Protocols.*` (entries are in
  `PublicAPI.Unshipped.txt`, so no shipped member changes); `Lunate.Tui.Platform` types are internal.
  No entry is added to or removed from a `PublicAPI.Shipped.txt`.
- Documentation: `docs/dev/architecture.md` (the component map is unaffected; the page gains the
  folder rule), `CONTRIBUTING.md`'s repository map, `docs/dev/README.md` only if a path it names
  moves (no documentation page moves in this change).
- No behaviour change: no golden session fixture, no settings key, no wire shape and no test
  expectation changes; the focused suites named in `tasks.md` prove it.
