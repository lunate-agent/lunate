## Why

The C# 15 / .NET 11 inventory in [`docs/dev/csharp-15.md`](../../../docs/dev/csharp-15.md) (step (a) of #192, landed as #899) decided the target frameworks: the self-contained app and the `dotnet tool` move to `net11.0`, while the extension contract packages keep a `net10.0` asset beside `net11.0`. Union types and closed hierarchies — two of the four C# 15 features the repository wants — cannot compile for `net10.0`, and step (c) of #192 is blocked until the runtime and the packages are retargeted.

## What Changes

- **BREAKING** (for `dotnet tool` users): the `lunate` tool package is now a `net11.0` framework-dependent tool and requires the .NET 11 runtime. The self-contained installers are unchanged and still install no runtime. The release note states the new requirement.
- `Directory.Build.props` targets `net11.0`, so every first-party project (the app, the TUI, Mermaid.Terminal, the harness internals, the test projects) builds and runs on .NET 11 with C# 15.
- The published extension-facing packages multi-target `net10.0;net11.0`: `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`, and `Lunate.Ai` / `Lunate.Ai.Decisions` (the assemblies `Lunate.Agent` carries). Their public API is identical on both assets; no union or closed type goes on the contract surface while `net10.0` remains a target.
- `Lunate.Extensibility.Testing` stays `net11.0`: it references the tool graph, which is `net11.0` only. The extension template keeps a `net10.0` extension project as the `net10.0` floor proof; its test project moves to `net11.0` because the test kit is `net11.0`.
- Live scripts, docs and the OpenSpec project context name the new frameworks; startup and memory budgets are re-measured on .NET 11.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `repo-foundation`: the clean-build contract moves from .NET 10 to .NET 11; a new requirement pins the published target-framework split and the re-measurement of budgets when the runtime changes.
- `distribution`: the `dotnet tool` package requires the .NET 11 runtime and the release note says so.

## Impact

- Build: `Directory.Build.props`, `.github/workflows` budget comments, `scripts/verify.sh`, `scripts/verify.ps1`, `scripts/gate-tests.sh`, `scripts/test-parallel.sh`, `scripts/eval.sh`, `scripts/mermaid-tape.sh`, `scripts/render-*.sh`.
- Projects: `src/Lunate.Contracts`, `src/Lunate.Extensibility.Abstractions`, `src/Lunate.Agent`, `src/Lunate.Ai`, `src/Lunate.Ai.Decisions` multi-target; `src/Lunate.Coding` (tool) and `src/Lunate.Extensibility.Testing` stay `net11.0`; the template and the extension-copy end-to-end test move their test project to `net11.0`.
- Tests: the reference-pack fixtures locate the runtime's own `ref/<tfm>`; `TestMcpServer` resolution and build-output paths follow `net11.0`; the template copy test asserts both contract assets are packed.
- Docs: `AGENTS.md`, `openspec/config.yaml`, `CONTRIBUTING.md`, `docs/dev/guide.md`, `docs/dev/csharp-15.md`, `docs/extensions/README.md`, `README.md`, `docs/quickstart.md`, `docs/dev/distribution.md`.
- No new NuGet packages. No public API change: the analyzer must report the same surface on both assets.

## Intent decisions

`grill-me` was run in self-answer mode; each question is answered from #192, the inventory, the specs and the code.

1. **Which frameworks?** App and tool `net11.0`; contract/harness packages `net10.0;net11.0`; internals `net11.0`. Source: #192's target-framework decision in `csharp-15.md`.
2. **Does the tool carry both `tools/net10.0` and `tools/net11.0`?** No. The card's acceptance says the tool *requires* .NET 11, so it ships one `net11.0` asset and the release note says so. Carrying a `net10.0` tool asset would also require the whole tool graph on `net10.0`, which the decision explicitly does not do.
3. **Which projects multi-target?** Only the published extension-facing ones and the assemblies `Lunate.Agent` carries: `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`, `Lunate.Ai`, `Lunate.Ai.Decisions`. `Lunate.Extensibility.Testing` cannot: it references `Lunate.Coding`, which is `net11.0` only.
4. **Does dropping `net10.0` from a published contract package happen here?** No. Dropping it would be a breaking change needing an ADR and a deprecation window; the net10 asset stays as the compatibility floor.
5. **Does the extension template stay `net10.0`?** The extension project does, because it is the floor proof. Its test project moves to `net11.0` because the test kit is `net11.0`; the copy test builds both and reads the extension output from its own TFM.
6. **What does the host compile extensions against now?** The host's own runtime TFM, computed by `SingleFileTarget.Current()` — `net11.0` after this change. No hard-coded `net10.0` remains in the product; tests that need the current TFM derive it instead of pinning it.
7. **Are the budgets re-calibrated now?** They are re-measured on this machine on the .NET 11 publish. Local default and CI values stay unless the measurement moves past their headroom; CI per-runner re-calibration is a post-merge/nightly maintainer step because only those runners can produce its evidence.
8. **Is this an architecture decision needing an ADR?** No new ADR: #192 already decided the target frameworks, and ADR-0005/ADR-0009 own budget calibration. The change records the decision and its measurements in its design.
9. **Is a public API change expected?** No. The public surface must be identical on both assets; `PublicAPI.Unshipped.txt` should not move.
