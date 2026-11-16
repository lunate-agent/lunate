## 1. The framework declaration

- [x] 1.1 `Directory.Build.props`: `TargetFramework` `net11.0`, `LangVersion` `15.0` with the reason
      (one compiler for the repository, and the `net10.0` contract asset gets C# 15 too).
- [x] 1.2 Drop the two per-project `LangVersion` lines (`Lunate.Coding`, `Lunate.Tui`) that opted in
      ahead of the move.
- [x] 1.3 Multi-target `net10.0;net11.0` in `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`,
      `Lunate.Agent`, `Lunate.Ai`, `Lunate.Ai.Decisions` and `Mermaid.Terminal`, each clearing the
      inherited `TargetFramework` (MSBuild's cross-targeting condition) with the comment saying why.
- [x] 1.4 `Lunate.Extensibility.Testing` packs `lib/$(TargetFramework)` instead of `lib/net10.0`.
- [x] 1.5 Extension template: the extension project targets `net10.0;net11.0` and its test project
      `net11.0` (the test kit carries the tool's assemblies).
- [x] 1.6 Build the solution in Release on the pinned SDK: every project compiles both declared assets
      with no warning, and `dotnet pack` of a contract package carries `lib/net10.0` and `lib/net11.0`.

## 2. The framework folder, derived instead of spelled

- [x] 2.1 `scripts/lib.sh` grows `target_framework` (read from `Directory.Build.props`); `verify.sh`,
      `gate-tests.sh`, `test-parallel.sh`, `eval.sh` and the media scripts use it.
- [x] 2.2 `scripts/verify.ps1` reads the same value from `Directory.Build.props` for its provider-asset
      check.
- [x] 2.3 `Lunate.TestSupport.BuildOutput.TargetFramework` (the build output folder's own name) replaces
      every literal framework in the fixtures that name a sibling build's output.
- [x] 2.4 `CompileFixture` and `SingleFileLoopFixture` locate a reference pack by the host's own
      `SingleFileTarget.Current().Tfm` instead of `ref/net10.0`, so a fixture and the lock agree.
- [x] 2.5 `BuildOutputTests` pins the derived value against the running assembly's
      `TargetFrameworkAttribute`.

## 3. Toolchain

- [x] 3.1 `Microsoft.CodeAnalysis.CSharp.Workspaces` and `.Workspaces.MSBuild` `5.9.0` → `5.12.0`.
- [x] 3.2 `Microsoft.Build.Framework` `17.11.48` → `18.7.1` (required by the workspaces package).
- [x] 3.3 `NuGet.*` `7.9.0` → `8.0.0` in the restore engine and the two test projects that pin them.
- [x] 3.4 CSharpier stays `1.4.0` (the C# 15 pin from #901); `dotnet csharpier check .` is clean.

## 4. Tests

- [x] 4.1 `dotnet build lunate.slnx -c Release` with zero warnings and zero errors.
- [x] 4.2 Extension compile and restore: `Lunate.Extensibility.Compile.Tests` (33 passed, 1 skipped)
      and `Lunate.Extensibility.Restore.Tests` (164 passed) on the new Roslyn and NuGet.
- [x] 4.3 Extension floor: `Lunate.Extensibility.Testing.Tests` (28 passed), including the copied
      template end-to-end test that builds the `net10.0` asset and loads it in a `net11.0` host.
- [x] 4.4 Roslyn backend on `5.12.0`: `Lunate.Roslyn.Tests` (216 passed).
- [x] 4.5 Single-file extension families and the template: `Lunate.Coding.Tests` filtered to
      `SingleFileLoop*`, `BuildOutputTests` and `ExtensionTemplateTests` (14 passed).

## 5. Startup budget

- [x] 5.1 Publish the `osx-arm64` single-file binary from this branch, and a `net10.0` baseline from
      `origin/main` with the same SDK, in the same session.
- [x] 5.2 Measure both with `scripts/perf.sh` (three interleaved rounds each) and record floor and
      median beside the previous release's numbers in the ADR and the pull request.
- [x] 5.3 Leave the calibrated budgets and the gated statistic (the floor) unchanged, with the reason.

## 6. Documentation

- [x] 6.1 `docs/dev/guide.md`: the tech-stack sentence, the runtime table row, the union-ready note and
      the `dotnet tool` line.
- [x] 6.2 `CONTRIBUTING.md`: the target framework, the two targeting packs the build needs, the SDK an
      extension author needs, and the built binary's path.
- [x] 6.3 `docs/dev/distribution.md`: the tool package's asset and runtime requirement.
- [x] 6.4 `docs/dev/csharp-15.md`: an outcome section recording what step (b) did and which examples
      the multi-targeting decision retires.
- [x] 6.5 `openspec/config.yaml`: the tech-stack line.
- [x] 6.6 `adr/0040-target-frameworks.md`, with `check-adr-numbers.py` and `check-adr-status.py` green.

## 7. Close-out

- [x] 7.1 `openspec validate --all --strict` green.
- [x] 7.2 `changes/<pr>.md` written under the release's own headings.
- [x] 7.3 `scripts/verify.sh` green locally where the sandbox allows it: build, both test passes,
      publish, startup budget, the tool and install roundtrips, packaging, formatting, docs, public API.
      Container-runtime and Windows/macOS-runner checks are the maintainer's CI legs.
- [x] 7.4 Archive the change as the last commit.

## Open items (not this change)

- Step (c) of #192: the internal union/`closed` refactors (`Lunate.Tui`'s `LiveArea`,
  `TranscriptEntry`) once this target move is in.
- `AgentEvent` and the hook-result unions: their own decision, after the contract packages' target
  framework is revisited.
