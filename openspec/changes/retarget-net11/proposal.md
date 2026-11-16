## Why

The C# 15 / .NET 11 inventory in [`docs/dev/csharp-15.md`](../../../docs/dev/csharp-15.md) (step (a) of #192, landed as #899) decided the target framework, and the maintainer's decision of 2026-11-15 settled its shape: **everything moves to `net11.0`, with no multi-targeting.** The first decision on #192 kept a `net10.0` asset beside `net11.0` in the published extension-facing packages; that clause is superseded. There are no external extension authors and one user, so nothing needs a `net10.0`-compatible contract, and the floor cost a second build per package plus the rule that no union or closed type — the two C# 15 features this retarget exists to unlock — may appear on the contract surface. Union types and closed hierarchies cannot compile for `net10.0` at all: the compiler requires the `net11.0` BCL types `IUnion`, `UnionAttribute` and `IsClosedTypeAttribute`.

## What Changes

- `Directory.Build.props` targets `net11.0`, and no project overrides it with `TargetFrameworks`. The tool, the app, the TUI, `Mermaid.Terminal`, the self-contained release binaries, every internal and test project, the extension template and every published package build for `net11.0` with C# 15.
- **BREAKING** (for `dotnet tool` users): the `lunate` tool package is a `net11.0` framework-dependent tool and requires the .NET 11 runtime. The self-contained installers are unchanged and still install no runtime. The release note states the new requirement.
- **BREAKING** (for an extension built elsewhere): `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`, `Lunate.Extensibility.Testing` and the `Lunate.Ai` / `Lunate.Ai.Decisions` assemblies `Lunate.Agent` carries each ship one `lib/net11.0` asset. The extension template's extension project moves to `net11.0` with its test project, so a copy shows what an extension for this host looks like.
- The fixture solutions the gate restores move to `net11.0` with the repository, so an offline restore resolves against the pinned SDK's own targeting packs instead of packs the net10 build no longer pulls; the fixture whose subject is the multi-target shape stays multi-target at `net11.0`.
- Live scripts, docs and the OpenSpec project context name the new framework; the startup and memory budgets are re-measured on .NET 11 after the merge, as the maintainer's step (ADR-0005, ADR-0009), not as this change's evidence.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `repo-foundation`: the clean-build contract moves from .NET 10 to .NET 11, and a new requirement pins one target framework for every project with no multi-targeting.
- `distribution`: the `dotnet tool` package requires the .NET 11 runtime and the release note says so.

## Impact

- Build: `Directory.Build.props`; the five projects that multi-targeted (`Lunate.Agent`, `Lunate.Ai`, `Lunate.Ai.Decisions`, `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`) drop their `TargetFrameworks` override; `src/Lunate.Extensibility.Testing` and `src/Lunate.Agent` pack assets under `lib/$(TargetFramework)`.
- Template and fixtures: `templates/extension/TemplateExtension`, its test project, the fixture solutions under `tests/fixtures/solutions`, `tests/fixtures/verified-edits`, and the inline fixture projects in the compile and restore tests.
- Scripts: `scripts/verify.sh`, `scripts/verify.ps1`, `scripts/gate-tests.sh`, `scripts/test-parallel.sh`, `scripts/eval.sh`, `scripts/mermaid-tape.sh`, the media/render scripts and `scripts/ci-powershell-paths.sh`.
- Tests: the reference-pack fixtures locate the runtime's own `ref/<tfm>`; `TestMcpServer` resolution and build-output paths follow `net11.0`; the template copy test asserts the packed contract package carries a `net11.0` asset and no other; a new test asserts that no project in the repository targets or multi-targets anything else.
- Docs: `AGENTS.md`, `openspec/config.yaml`, `CONTRIBUTING.md`, `README.md`, `docs/quickstart.md`, `docs/dev/guide.md`, `docs/dev/README.md`, `docs/dev/csharp-15.md`, `docs/dev/distribution.md`, `docs/extensions/README.md`, `templates/extension/README.md`, `adr/0041-net11-target-frameworks.md`.
- No new NuGet packages. No public API change: each project has one target, so `PublicAPI.Unshipped.txt` does not move.

## Intent decisions

`grill-me` was run in self-answer mode; each question is answered from #192, the inventory, the specs and the code.

1. **Which frameworks?** One: `net11.0` everywhere. Source: the maintainer's 2026-11-15 decision on #192, which supersedes the earlier multi-target clause; there are no external extension authors.
2. **Does the tool carry both `tools/net10.0` and `tools/net11.0`?** No. The card's acceptance says the tool *requires* .NET 11, so it ships one `tools/net11.0` asset and the release note says so.
3. **Do the published contract packages keep a `net10.0` asset?** No. The superseding decision drops the floor, and with it the rule that the contract surface must stay `net10.0`-compatible. The ADR records that the question returns, as a deliberate breaking decision, before the first external release or the first published library version.
4. **What happens to an extension built for `net10.0`?** It has no contract asset to bind to; an extension for this host rebuilds for `net11.0`. Nothing is published to an external author yet, so nothing breaks outside this repository.
5. **Does the extension template stay `net10.0` as a floor proof?** No — the floor is gone. Both template projects are `net11.0`, which is also what a copy outside the repository needs to build against this host's packages.
6. **Do the fixture solutions move too?** Yes. They restore offline against the machine's global packages folder, which the repository's own build fills with the packs it needs; after the retarget that is `net11.0` only, so a `net10.0` fixture would ask for packs nothing restores any more. The `multi-target` fixture keeps its shape at `net11.0` because multi-target project handling is what it tests.
7. **What does the host compile extensions against now?** The host's own runtime TFM, computed by `SingleFileTarget.Current()` — `net11.0` after this change. No hard-coded `net10.0` remains in the product; tests that need the current TFM derive it instead of pinning it.
8. **Are the budgets re-calibrated here?** No. The card makes the startup and memory re-measurement a maintainer step after the merge, not pull-request evidence; the per-runner CI values are recalibrated from the post-merge and nightly runs (ADR-0005, ADR-0009). The change records that in the ADR rather than moving a budget on local numbers.
9. **Is this an architecture decision needing an ADR?** It is the maintainer's decision, and it supersedes a target-framework clause of the previous one, so it is recorded as [ADR-0041](../../../adr/0041-net11-target-frameworks.md). No budget ADR is amended.
10. **Is a public API change expected?** No. Each project has one target; the analyzer reads the same `PublicAPI.*.txt` and no public member moves.
