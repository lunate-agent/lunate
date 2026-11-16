## Why

SDK 11 has been pinned since #898 and #899's inventory (`docs/dev/csharp-15.md`) settled what the
repository wants from C# 15 and what each feature costs — but the projects still target `net10.0`.
Two of the four wanted features, union types and closed hierarchies, need the `net11.0` BCL (CS0518
for `IUnion`, CS0656 for `IsClosedTypeAttribute`), so they stay out of reach while the target stays
put, and the tool is built by an SDK one release ahead of the runtime it ships. The target framework
is also a promise here: `Lunate.Contracts`, `Lunate.Extensibility.Abstractions` and the published
harness packages are what an extension compiles against, and dropping `net10.0` from them would break
every existing extension.

## What Changes

- The tool, the app, the self-contained archives and every first-party project target `net11.0`,
  declared once in `Directory.Build.props`, with `LangVersion` fixed at `15.0` there instead of in the
  two projects that adopted C# 15 syntax early.
- `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent` (with the `Lunate.Ai` and
  `Lunate.Ai.Decisions` assemblies its package carries) and `Mermaid.Terminal` multi-target
  `net10.0;net11.0`. Both assets carry the same public API: no union or closed type on that surface
  while `net10.0` is a target.
- **BREAKING for `dotnet tool` users only:** the `lunate` package is the `net11.0` tools asset, so
  `dotnet tool install -g lunate` needs the .NET 11 runtime. The self-contained release archives do
  not, and they stay the install path for a machine without it. The release note says so; no `#if`
  net10 tool asset exists.
- The extension template builds `net10.0;net11.0`, so the compatibility floor is proven by the gate
  on every run; the copied-template end-to-end test loads the `net10.0` output in a `net11.0` host.
- Toolchain moves with the SDK: Roslyn `5.9.0` → `5.12.0` (the compiler the SDK ships),
  `Microsoft.Build.Framework` → `18.7.1`, `NuGet.*` `7.9.0` → `8.0.0`. CSharpier stays at `1.4.0`,
  which already formats C# 15.
- Tests and scripts stop spelling the framework folder: `scripts/lib.sh` grows `target_framework`
  (read from `Directory.Build.props`), `scripts/verify.ps1` reads the same value, and
  `Lunate.TestSupport` grows `BuildOutput.TargetFramework` for the fixtures that locate a sibling
  build's output or a reference pack's `ref/<tfm>` folder.
- The startup budget is re-measured against a `net10.0` binary built from `origin/main` with the same
  SDK, on the same machine, in the same session.
- `adr/0041-target-frameworks.md` records the split and why the contract packages keep `net10.0`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `repo-foundation`: the clean-build contract names .NET 11 and C# 15, and a new requirement fixes
  which projects may target more than one framework.
- `extensions`: a new requirement states that the extension-facing packages keep a `net10.0` asset
  with the same public API as the `net11.0` one, and that the template proves it by building both.
- `distribution`: the `dotnet tool` requirement states that the tool package is the `net11.0` asset
  and needs the .NET 11 runtime, while the archives stay self-contained.

## Impact

- `Directory.Build.props`, `src/*/*.csproj`, `templates/extension/**`, `tests/**` fixtures and test
  support, `scripts/*.sh`, `scripts/*.ps1`, `openspec/config.yaml`.
- Package shape: `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent` and
  `Mermaid.Terminal` `.nupkg` files gain a `lib/net11.0` asset beside `lib/net10.0`; `lunate.<v>.nupkg`
  moves from `tools/net10.0` to `tools/net11.0`; `Lunate.Extensibility.Testing` packs `lib/net11.0`.
- No public API change, no session-format change, no dependency added (five packages bumped).
- `adr/0041-target-frameworks.md`, `docs/dev/guide.md`, `docs/dev/csharp-15.md`,
  `docs/dev/distribution.md`, `CONTRIBUTING.md`.
