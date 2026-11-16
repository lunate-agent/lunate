# Design: feature folders that never change the public surface

## Context

`src/` holds 15 projects and about 800 C# files; `tests/` mirrors that flatness with about 950 files.
The 0.3 review asked for feature folders, and thirteen issues (#539–#551) each named one project. The
files are already named after the type family they hold (`AgentHarness.Retries.cs`,
`SettingsStore.Project.cs`, `Mcp*`), so the missing layer is the directory: a reader has to read file
names to find a feature, and a reviewer cannot see from a path whether two files belong together.

The question the change has to settle first is what a move is allowed to change. The code answers it:
`src/Lunate.Contracts` declares `Lunate.Agent`, `Lunate.Ai` and `Lunate.Extensibility.Abstractions`
types and `src/Lunate.Agent` / `src/Lunate.Ai` / `src/Lunate.Extensibility.Abstractions` forward them
with `TypeForwardedTo`, so a compiled consumer keeps binding after the assemblies were split. The
namespace is the compatibility identity; the assembly and the folder are not. Extensions are compiled
by their authors against `Lunate.Extensibility.Abstractions`, so a namespace rename would break a
third-party extension for a directory tidy-up.

```mermaid
flowchart TB
    subgraph before["before: one flat folder per project"]
        A1["src/Lunate.Protocols/<br/>7 Mcp* files + Acp/*"]
        A2["src/Lunate.Agent/<br/>75 files"]
    end
    subgraph after["after: folders group, namespaces do not move"]
        B1["src/Lunate.Protocols/Mcp/Client/<br/>namespace Lunate.Protocols"]
        B2["src/Lunate.Protocols/Mcp/Server/<br/>namespace Lunate.Protocols"]
        B3["src/Lunate.Protocols/Acp/<br/>namespace Lunate.Protocols"]
        B4["src/Lunate.Agent/Loop/<br/>namespace Lunate.Agent"]
        B5["src/Lunate.Agent/Sessions/<br/>namespace Lunate.Agent"]
    end
    before --> after
```

## Goals / Non-Goals

**Goals**

- One written rule and one test that fails when a folder tries to become a namespace.
- Every project under `src/` split into feature folders; every test project mirrors them.
- No public surface change beyond folding the two folder-derived namespaces back.
- No behaviour change: same tests, same golden fixtures, same wire shapes.

**Non-Goals**

- No project moves, no csproj change, no assembly rename, no package change.
- No type rename and no member change.
- Not the full merge of the two decision DTO sets: that needs a new project reference (below).

## Decisions

### The rule: a folder groups, a namespace identifies

A file's namespace is the assembly's established namespace. A folder never adds, removes or changes a
segment. This is the rule the tree already follows everywhere except two folders, and it is the only
rule that can hold in `Lunate.Contracts`, which must keep declaring `Lunate.Agent` and `Lunate.Ai`
types for its type forwarders.

Rejected: **namespace = project namespace + relative folder**. It would rewrite every `using` in the
solution, break the type-forwarder scheme in `Lunate.Contracts` (the forwarded types would have to
change namespace, and the whole point of forwarding is that they do not), and break third-party
extensions compiled against `Lunate.Extensibility.Abstractions`. It would also make the layout rule
unmergeable with "organize `Lunate.Contracts` without changing its public API".

Rejected: **address-based namespaces with an allow-list for the forwarded types**. The allow-list is
the exception that makes the rule unauditable, and the next split would add a row.

### The two folder-derived namespaces fold back

`Lunate.Protocols/Acp/*.cs` declares `Lunate.Protocols.Acp` and `Lunate.Tui/Platform/*.cs` declares
`Lunate.Tui.Platform`. Both are exceptions the rule cannot tolerate. `IAcpServer`,
`AcpSessionContext` and the rest sit in `PublicAPI.Unshipped.txt` — no shipped assembly declares them
— so folding them into `Lunate.Protocols` changes an unreleased surface only; the `Lunate.Tui.Platform`
types are internal. The `PublicAPI.Unshipped.txt` entries are rewritten in the same commit, and every
`using Lunate.Protocols.Acp` in `src/`, `tests/` and `samples/` follows.

Trade-off: a host that already composes `IAcpServer` re-compiles. Nothing published depends on it,
and the 0.4 release is the only consumer.

### Feature folders are named after the capability, not the type suffix

Folders use the capability vocabulary the specs already use (`Decisions`, `Sessions`, `Tools`,
`Extensions`, `Packages`, `Skills`, `Trust`, `Acp`, `Mcp`) and stay singular PascalCase, matching the
folders the tree already has (`Acp`, `Platform`, `Transcript`, `ToolBlocks`). A project's own
sub-projects keep their own folder sets; `Lunate.Extensibility.Compile` is one file pair and gets no
folder.

### Files stay put where a path is part of a contract

Embedded assets (`settings-schema.json`, `system-prompt.md`, `keys.json`, `models.json`,
`providers.json`) keep their location next to the csproj, because the csproj names the path
(`EmbeddedResource Include="system-prompt.md"`) and a script or test reads the file. Only `.cs` files
move. `tests/fixtures/**` and `tests/Lunate.Coding.Tests/fixtures/**` keep their paths, because golden
sessions and fixtures are compared by path.

### Tests mirror their subject's folder

Each test file goes to the folder of the src feature it exercises, chosen from the types it names: the
test project's file list is walked against a type→src-file index, and the folder of the src file that
declares most of the types the test mentions wins. A test that names no src type (a support double, a
collection definition, an `AssemblyInfo.cs`) stays at the test project root, which is the substrate
every folder shares. `tests/Lunate.TestSupport`, `tests/tools` and `tests/TestExtensions` are support
projects with no single subject and get no folders.

Trade-off: a test that drives a cross-feature flow is filed under its subject type, which is the one
rule that is stable enough to review. The mirrored path is checked in the same source scan: a test
project has a folder only when the src project has it.

### The layout rule is enforced by a source-scanning test

`LayoutRuleTests` (in `tests/Lunate.Coding.Tests`, beside `LayeringTests`, `TargetFrameworkTests` and
the other repository-hygiene scans) walks `src/` and asserts two things: no declared namespace ends
with a folder segment of its own path, and every folder under a project is mirrored by the same folder
under the test project when one exists. It reads text, not Roslyn, like its siblings, so it adds no
dependency and stays fast.

### The decision layer: delete the duplicated validator, record the merge

`Lunate.Ai.Decisions.DecisionRequestValidator.Validate` re-checks what `Lunate.Agent`'s
`DecisionRequestValidator.Prepare` and `DecisionEngine` already checked on every request (a state, at
least one question, non-empty `Instructions`, unique answer sets), and every production call reaches
it through `DecisionEngine`; it is a second pass over the same object, and the second model's kind
name `Noul` for the contract's `YesNo` is the only reason the mapping switch reads oddly. This change
deletes the second validator, keeps the single validation at the boundary, and renames the internal
kind to `YesNo` so both models name the four kinds the same way.

The full merge — one `DecisionRequest`/`DecisionQuestion`/`DecisionAnswer` set instead of two — needs
`Lunate.Ai.Decisions` to reference `Lunate.Contracts`, a new project edge in the layer graph that
`LayeringTests` enumerates and AGENTS.md draws. That is a dependency decision for the maintainer, so
this change leaves the two DTO sets in place, names the follow-up in `tasks.md`, and does not add an
edge.

## Risks / Trade-offs

- **A path-sensitive test breaks on a moved file** → the source scans walk the tree generically; the
  only literal `src/` paths are the csproj references, which do not move, and the embedded-asset
  includes, whose files do not move. The focused suites in `tasks.md` include every scan.
- **A test lands in the wrong feature folder** → the folder is reviewable from the path; the rule that
  a test sits with its subject is a convention, and a misplacement costs a later move, not a build.
- **The two namespace folds touch a public surface** → `Acp` types are unshipped and the `Tui.Platform`
  types are internal; the API-analyzer files are updated in the same commit and the focused suites
  include `PublicApiTests` and the ACP tests.
- **The diff is large** → it is moves: `git status` shows renames, and no file's content changes
  except the namespace fold and the decision-layer deletion, which carry their own tests.

## Migration Plan

One change, one pull request, one commit per slice. Rollback is `git revert` of the moves. No data,
settings, wire format or session fixture is affected, so no migration is needed.

## Open Questions

The full decision-model merge needs the maintainer's sign-off on one project edge
(`Lunate.Ai.Decisions` → `Lunate.Contracts`, or moving the DTOs down into `Lunate.Ai.Decisions` and
forwarding them from `Lunate.Contracts`). Recorded in `tasks.md` as the follow-up; not taken here.
