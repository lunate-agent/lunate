## 1. Retarget the build

- [x] 1.1 Set `Directory.Build.props` to `net11.0` and remove the `net10.0;net11.0` override from the five projects that multi-targeted (`Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`, `Lunate.Ai`, `Lunate.Ai.Decisions`).
- [x] 1.2 Keep the preview kit's and the agent package's `lib/` content on `$(TargetFramework)`, which now resolves to the one `net11.0` asset.
- [x] 1.3 Update the live build-output paths (`scripts/verify.sh`, `scripts/verify.ps1`, `scripts/gate-tests.sh`, `scripts/test-parallel.sh`, `scripts/eval.sh`, the media/render scripts, `scripts/ci-powershell-paths.sh`) to `net11.0`.

## 2. Tests and fixtures (red first)

- [x] 2.1 Add `TargetFrameworkTests`: no project in the repository declares `TargetFrameworks`, every declared `TargetFramework` is `net11.0`, and `Directory.Build.props` hands `net11.0` to everything (fails on the multi-target branch first).
- [x] 2.2 Make the reference-pack fixtures locate `ref/<current TFM>` instead of `ref/net10.0`, and derive the `TestMcpServer` path from the runtime TFM.
- [x] 2.3 Move `TemplateExtension` and `TemplateExtension.Tests` to `net11.0` and pack the template's output at the one target-framework path.
- [x] 2.4 Move the fixture solutions and the inline fixture projects to `net11.0` so the offline restores resolve against the pinned SDK's own targeting packs; keep the `multi-target` fixture's shape at `net11.0`.
- [x] 2.5 Extend `ExtensionTemplateCopyEndToEndTests` to assert the packed contract package carries a `lib/net11.0` asset and no other.
- [x] 2.6 Build the touched projects and run the focused test classes (`TargetFrameworkTests`, `ExtensionTemplateCopyEndToEndTests`, the compile/restore fixtures, the verified-write tests).

## 3. Documentation and release note

- [x] 3.1 Update `AGENTS.md`, `openspec/config.yaml`, `CONTRIBUTING.md`, `README.md`, `docs/quickstart.md`, `docs/dev/guide.md`, `docs/dev/README.md`, `docs/dev/csharp-15.md`, `docs/dev/distribution.md`, `docs/extensions/README.md` and `templates/extension/README.md` for .NET 11 / C# 15, the single target framework and the tool's runtime requirement.
- [x] 3.2 Write ADR-0041 (superseding the multi-target clause of #192's first decision) and update the change's ADR manifest.
- [x] 3.3 Write the changelog fragment `changes/910.md` under `### Changed`, stating that the tool requires .NET 11 and that nothing multi-targets.

## 4. Finish

- [x] 4.1 Run `dotnet csharpier format .` and the fast gate (Release build, focused tests, `openspec validate --all --strict`).
- [x] 4.2 Archive as the last commit: `openspec archive retarget-net11 --yes`.

## 5. Rework after review (fixture and Purpose)

- [x] 5.1 Commit no project file that declares two frameworks: `tests/fixtures/verified-edits/multi-target/MultiTarget.csproj` is single-target `net11.0`, and `ChecksOnlyWorkspaceLoadedTfm` writes `net11.0;net11.0-windows` into the temporary copy before the restore.
- [x] 5.2 Widen the target-framework test to the acceptance's full scope: it walks every project under the repository root with only `eval/tasks/` and `docs/dev/spikes/` exempt, and a new case proves a multi-targeting fixture under `tests/fixtures/` fails it.
- [x] 5.3 Update the delta, the proposal, the design and ADR-0041 for the run-time multi-target input, and state the `repo-foundation` Purpose correction; OpenSpec has no lifecycle operation for an existing spec's Purpose, so the one Purpose line is updated in place as `openspec archive`'s warning instructs.
- [x] 5.4 Re-run the named tests, `openspec validate --all --strict`, and re-archive as the last commit.
