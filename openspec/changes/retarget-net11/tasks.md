## 1. Retarget the build

- [x] 1.1 Set `Directory.Build.props` to `net11.0` and multi-target the five extension-facing projects (`Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`, `Lunate.Ai`, `Lunate.Ai.Decisions`) to `net10.0;net11.0`.
- [x] 1.2 Point the preview kit's and the agent package's `lib/` content at `$(TargetFramework)` instead of a hard-coded `net10.0`.
- [x] 1.3 Update the live build-output paths (`scripts/verify.sh`, `scripts/verify.ps1`, `scripts/gate-tests.sh`, `scripts/test-parallel.sh`, `scripts/eval.sh`, the media/render scripts) to `net11.0`.

## 2. Tests and fixtures (red first)

- [x] 2.1 Make the reference-pack fixtures locate `ref/<current TFM>` instead of `ref/net10.0`, and derive the `TestMcpServer` path from the runtime TFM.
- [x] 2.2 Move `TemplateExtension.Tests` to `net11.0` while `TemplateExtension` stays `net10.0`, and update the copy targets to read the extension's own TFM.
- [x] 2.3 Extend `ExtensionTemplateCopyEndToEndTests` to assert the packed contract package carries both `lib/net10.0` and `lib/net11.0` entries.
- [x] 2.4 Build the touched projects and run the focused test classes (`ExtensionTemplateCopyEndToEndTests`, the compile/restore fixtures, the architecture tests).

## 3. Budgets and evidence

- [x] 3.1 Publish the single-file binary for this RID and run `scripts/perf.sh`; record the startup floor/median.
- [x] 3.2 Attempt the idle-TUI memory gate; the sandbox denies `openpty`/`ps`, so record the limitation and the supplementary peak-RSS measurements of the published binary.
- [x] 3.3 Record both measurements in `design.md` and keep or re-calibrate the local budget per the measurement.

## 4. Documentation and release note

- [x] 4.1 Update `AGENTS.md`, `openspec/config.yaml`, `CONTRIBUTING.md`, `docs/dev/guide.md`, `docs/dev/csharp-15.md`, `docs/extensions/README.md`, `README.md`, `docs/quickstart.md` and `docs/dev/distribution.md` for .NET 11 / C# 15 and the tool's runtime requirement.
- [x] 4.2 Write the changelog fragment `changes/<pull-request-number>.md` under `### Changed`, stating the tool requires .NET 11.

## 5. Finish

- [x] 5.1 Run `dotnet csharpier format .` and the fast gate (Release build, focused tests, `openspec validate retarget-net11 --strict`).
- [x] 5.2 Archive as the last commit: `openspec archive retarget-net11 --yes`, then `openspec validate --all --strict`.
