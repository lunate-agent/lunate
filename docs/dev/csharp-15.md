# C# 15 / .NET 11 features worth adopting

Four C# 15 features pay for themselves in first-party code: union types, closed hierarchies,
collection-expression arguments and labeled `break`/`continue`. The first two need `net11.0` to
compile and run; the last two need only the SDK 11 compiler. Everything in the repository therefore
targets `net11.0`, with no multi-targeting.

This is the inventory for step (a) of #192: it records what we want and what it costs. Step (b), the
target-framework change, is applied by #902 ([ADR-0041](../../adr/0041-net11-target-frameworks.md));
the refactor slices are step (c).

## How this was checked

All probes used SDK `11.0.100` (`csc` 5.12.0) in throwaway projects. Each feature was compiled twice,
once with `TargetFramework=net10.0` and `LangVersion=15.0`, once with `net11.0`. CSharpier was run at
`1.3.0` and at `1.4.0` (the pin since #901). The analyzer probe added
`Microsoft.CodeAnalysis.PublicApiAnalyzers` `5.6.0` and the repository's
`PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt` pair.

| Feature | `net10.0` + C# 15 | `net11.0` |
| --- | --- | --- |
| Union types | fails: CS0518 `System.Runtime.CompilerServices.IUnion`, CS0656 `UnionAttribute..ctor` | compiles |
| Closed hierarchies | fails: CS0656 `IsClosedTypeAttribute..ctor` | compiles |
| Collection-expression arguments | compiles and runs | compiles and runs |
| Labeled `break` / `continue` | compiles and runs | compiles and runs |
| Extension indexers (not adopted) | compiles and runs | compiles and runs |
| Non-virtual static interface members (not adopted) | compiles; `netstandard2.0` with C# 14 fails, C# 15 compiles | compiles |
| Memory-safety preview (not adopted) | compiles with `LangVersion=preview` and the feature flag | compiles |

## Union types

**What it improves.** A union turns "one of these records" into one compiler-checked value: implicit
conversions from each case, an exhaustive `switch` with no `default`, and no hand-written abstract
base record or private constructor. The compiler synthesises the wrapper's constructors, `Value`
property and conversions.

**Two real examples.**

- [`LiveArea.cs`](../../src/Lunate.Tui/LiveArea.cs) — `LiveArea.LiveAreaInput` is a private abstract
  record with more than a dozen sealed cases (`KeyInput`, `ResizeInput`, `SpinnerTick`,
  `ApprovalInput`, …). A union removes the abstract base and the private-constructor ceremony, and the
  one dispatch method becomes an exhaustive match over the cases.
- [`SequenceParser.cs`](../../src/Mermaid.Terminal/SequenceParser.cs) — `SequenceStatement` is an
  internal abstract record with `SequenceMessage` and `SequenceNote`; `SequenceDraw` pattern-matches
  it. A union makes the two variants closed and deletes the base record.

The contract's closed sets were the case this inventory deferred, and #919 decided them as
[ADR-0043](../../adr/0043-contract-unions.md) (superseding
[ADR-0042](../../adr/0042-contract-closed-sets.md)): `AgentEvent`/`ExtensionEvent`
([`AgentEvent.cs`](../../src/Lunate.Contracts/AgentEvent.cs)) and the seven hook-result families
([`HookResults.cs`](../../src/Lunate.Extensibility.Abstractions/Hooks/HookResults.cs)) are unions over
their case records. Two union facts shape that code, both measured: a union declaration may not carry
instance fields or auto-properties (CS9373), so the stamped `SessionId`/`ParentRunId`/`Source` stay on
the `AgentEventBase` record and the channel stamps through it before converting once; and a union's
`Value` is `object`, so `Enumerable.OfType<T>` over it never matches — the contract ships an
`OfType<T>` overload and a `Kind` property so the existing consumer idiom keeps working. The wire is
projected from the case by both protocols, never serialized as a union, so no JSON classifier exists
and no fixture moved.

**Runtime / BCL.** Needs `net11.0`. The compiler requires the `net11.0` BCL types
`System.Runtime.CompilerServices.IUnion` and `UnionAttribute`; a `net10.0` target fails with CS0518
and CS0656. This is not a compiler-only feature.

**Analyzer and tooling support.** The build is clean under SDK 11. PublicApiAnalyzers sees the union
type and reports the compiler-synthesised members (`Pet(Cat)`, `Pet(Dog)`, `Pet()`, `Pet.Value`) as
RS0016, so the public API files must gain those entries when a public type becomes a union. CSharpier
`1.3.0` rejects the syntax ("Was not formatted due to syntax errors"); CSharpier `1.4.0` formats
`public union Pet(Cat, Dog);` cleanly.

## Closed hierarchies

**What it improves.** `closed` fixes the direct descendants at compile time, so a `switch` over them
is exhaustive without a `default` arm. Unlike a union, the cases stay real classes: constructors,
properties, inheritance and the existing public shape are preserved. That matters where a wrapper
would change an API or where the base class carries behaviour.

**Two real examples.**

- [`TranscriptEntry.cs`](../../src/Lunate.Tui/Transcript/TranscriptEntry.cs) — `TranscriptEntry` is
  the base of the committed transcript entries. The renderer switches over it in
  `TranscriptViewerRenderer.EntryLines`, and the picker switches over it in `TranscriptViewer.Matches`;
  `closed` makes both switches exhaustive.
- [`RenderResult.cs`](../../src/Mermaid.Terminal/RenderResult.cs) — `MermaidRenderResult` is the
  public result base with `MermaidDiagramResult` and `MermaidFallbackResult`. It has a
  `private protected` constructor and real properties, so a union wrapper is the wrong shape; `closed`
  gives the hosts exhaustive matching while keeping the class contract.

**Runtime / BCL.** Needs `net11.0`. The compiler emits/consumes
`System.Runtime.CompilerServices.IsClosedTypeAttribute`; a `net10.0` target fails with CS0656. A
closed class is implicitly abstract, cannot also be `sealed`, `static` or explicitly `abstract`, and
closure is not transitive: mark intermediate descendants closed too if the exhaustiveness has to
reach further down.

**Analyzer and tooling support.** PublicApiAnalyzers treats the closed type as an ordinary abstract
type; the public API entry is the same shape as before. CSharpier `1.3.0` rejects the `closed`
modifier with a syntax error; `1.4.0` formats it cleanly.

## Collection-expression arguments

**What it improves.** `[with(...)]` passes constructor or factory arguments from inside a collection
expression. It deletes the `new List<T>(capacity)` plus loop and the `new HashSet<T>(comparer)`
constructor, while keeping the element list in one expression.

**Two real examples.**

- [`OutputExcerpt.cs`](../../src/Lunate.Tui/ToolBlocks/OutputExcerpt.cs) — `OutputExcerpt.Whole`
  creates `new List<OutputExcerptLine>(lines.Length)` and adds one mapped element per source line.
  It becomes `[with(capacity: lines.Length), .. lines.Select(static line => new OutputExcerptLine(line, false))]`.
- [`McpServerHost.cs`](../../src/Lunate.Protocols/McpServerHost.cs) — `McpServerHost.ListToolsAsync`
  builds `new HashSet<string>(overrides.Keys, StringComparer.Ordinal)`. It becomes
  `[with(StringComparer.Ordinal), .. overrides.Keys]`.

**Runtime / BCL.** Compiler-only. The probes compiled and ran on `net10.0`; the only requirement is
SDK 11 with `LangVersion=15.0` (targeting `net11.0` selects C# 15 by default). No new BCL type is
involved for the `List<T>`/`HashSet<T>` cases.

**Analyzer and tooling support.** CSharpier `1.3.0` already parses and formats `[with(...)]`; the
checked-in build and the PublicApiAnalyzers probe compile the expression normally, and the syntax
introduces no new symbol for an analyzer to understand.

## Labeled break and continue

**What it improves.** A nested loop can name the enclosing loop it wants to leave or advance, so the
`bool found`/`bool truncated` flag and the second `if (flag) break;` disappear. It replaces the
flag-and-`goto` workarounds the language used to require.

**Two real examples.**

- [`EditMatcher.cs`](../../src/Lunate.Coding/EditMatcher.cs) — `EditMatcher.FindMatches` sets
  `matched = false; break;` inside the pattern loop and then tests `matched` after it. The inner
  mismatch becomes `continue outer;` on the outer window loop, deleting the flag and the inner
  `break`.
- [`SkillScanner.cs`](../../src/Lunate.Coding/SkillScanner.cs) — `SkillScanner.ScanState.Roots`
  sets `truncated = true; break;` in the directory loop and then repeats `if (truncated) break;`
  after it. The second test becomes `break roots;`; the flag stays only because the method still uses
  it after the loop to add the warning.

**Runtime / BCL.** Compiler-only. It compiled and ran on `net10.0` with `LangVersion=15.0`; no BCL
type or runtime behaviour is involved.

**Analyzer and tooling support.** CSharpier `1.3.0` rejects the labeled jump syntax; `1.4.0` formats
`outer:` on its own line before the loop. The .NET 11 SDK documents `IDE0410`, which flags the
boolean-flag and `goto` patterns this replaces; the analyzer/build path accepts the new syntax.

## Not adopted in this cycle

These features are real C# 15 work, but they have no two real call sites in this repository yet.

| Feature | Why not | What would reopen it |
| --- | --- | --- |
| Extension indexers | Every indexed access in `src/` already goes through an array, list or dictionary indexer. The repository declares only two extension methods, both `StringBuilder.Line` helpers ([`DiagnosticBundle.cs`](../../src/Lunate.Coding/DiagnosticBundle.cs), [`SessionHtmlWriter.cs`](../../src/Lunate.Coding/SessionHtmlWriter.cs)), and neither is index-like. | An extension surface or package adapter grows a real index-like extension method. |
| Non-virtual static interface members on older runtimes | No interface in `src/` declares a static member, and every first-party target already runs on `net11.0`, whose runtime supports the existing default-interface implementation rules. The feature only relaxes an older-runtime check. | A contract package starts targeting `netstandard2.0` or another runtime without default interface implementations. |
| Memory-safety preview | It is preview, needs `LangVersion=preview`, `AllowUnsafeBlocks` and the `updated-memory-safety-rules` feature flag. The native code here is P/Invoke and marshalling ([`UnixChildProcess.cs`](../../src/Lunate.Coding/UnixChildProcess.cs), [`SystemConsoleIO.cs`](../../src/Lunate.Tui/SystemConsoleIO.cs)), not pointer-access code with an `unsafe` block to audit. | The feature stabilises, or the native layer starts doing pointer access that needs the new `unsafe`/`safe` contracts. |

## Target frameworks

The recommendation above is applied by #902; read [ADR-0041](../../adr/0041-net11-target-frameworks.md)
for the durable decision and [`guide.md`](guide.md) for the current runtime table.

**Everything targets `net11.0`.** Union types and closed hierarchies — two of the four features we
want — cannot compile for `net10.0`; they need the `net11.0` BCL. `Directory.Build.props` sets
`net11.0` and no project overrides it with `TargetFrameworks`: the tool, the app, the self-contained
release binaries (they carry their runtime, so the user still installs nothing), `Lunate.Tui`,
`Mermaid.Terminal`, every internal and test project, the extension template and every published
package — `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`,
`Lunate.Extensibility.Testing` and the `Lunate.Ai` assemblies the harness carries.

**The `dotnet tool` package requires .NET 11.** It is framework-dependent, so its one asset is
`tools/net11.0`. `PackAsTool` can carry both `tools/net10.0` and `tools/net11.0` (verified with a
throwaway pack), but #192 chose the simpler shape: the tool graph is `net11.0` only and the release
note states the runtime requirement. The self-contained installers are unaffected.

**The public contract has no `net10.0` floor.** There are no external extension authors and one user,
so the `net10.0` asset the first decision kept bought nothing and cost the rule that no union or
closed type may appear on the contract surface. The contract's closed sets are now unions
(#919, [ADR-0043](../../adr/0043-contract-unions.md)); the `Lunate.Tui`/`Mermaid.Terminal`
refactor slices stay their own work. Reintroducing multi-targeting before the first external release
or the first published library version is a deliberate, breaking decision with its own ADR, never a
silent addition inside a feature card.

The startup and memory budgets are re-measured on .NET 11 after the merge (ADR-0005, ADR-0009), as a
maintainer step: the pull request only moves the runtime, and the per-runner CI values come from the
post-merge and nightly runs.

## Tooling steps before the first syntax commit

1. **CSharpier `1.3.0` → `1.4.0` (done, #901).** The old pin rejects union, closed and
   labeled-jump syntax, so `dotnet csharpier check .` would have turned the first refactor commit
   red. The pin is now `1.4.0` (released 2026-11-11), which formats C# 15. `scripts/gate-tests.sh`
   formats one sample of each new construct and fails by exit code when the pinned formatter cannot.
2. **Public API files.** A union adds compiler-synthesised constructors and a `Value` property to the
   public surface; PublicApiAnalyzers `5.6.0` already sees and reports them. Closed hierarchies add no
   new public shape. Update `PublicAPI.Unshipped.txt` in the same change as the type.
3. **`dotnet format`.** SDK 11's formatter ships the C# 15 Roslyn. It could not be exercised in the
   worker sandbox: the MSBuild build host cannot create its named pipe there (`SocketException 13`),
   which is an environment limit, not a syntax failure. #902 ran it on a normal machine as part of
   step (b)'s gate; the analyzer and style checks stay in the verify gate for step (c).

## Related

- [`guide.md`](guide.md) — the plan, runtime table and the union-ready note.
- [`../extensions/README.md`](../extensions/README.md) — what an extension compiles against.
- [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md) — the SDK, build and verify gate.
- [`../../AGENTS.md`](../../AGENTS.md) — the language, framework and public-API rules.
