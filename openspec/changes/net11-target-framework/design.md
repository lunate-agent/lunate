## Context

The repository pins SDK `11.0.100` (#898) and its C# 15 inventory (`docs/dev/csharp-15.md`, #899) named
four features worth adopting, two of which need the `net11.0` base class library. Every project still
targets `net10.0`. Two constraints frame the design:

- **The extension contract is a promise.** `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`,
  `Lunate.Agent` and `Mermaid.Terminal` are compiled against by third parties; an extension built
  today against `net10.0` must keep loading.
- **The tool's own features want the new runtime.** Union types and closed hierarchies cannot compile
  for `net10.0` at all (CS0518/CS0656), and gating them behind `#if` would reintroduce the code the
  features exist to delete.

The in-force ADRs that constrain this design: ADR-0001 (no AOT; single-file budgets stay), ADR-0002
(layering and the MEAI model types — unchanged), ADR-0005 and its floor addendum (the startup budget
and its statistic), ADR-0008 (release build shape — unchanged), ADR-0017 (extensibility; these are its
contract packages), ADR-0037 (gate sized by event) and ADR-0039 (`Mermaid.Terminal` as a standalone
package). None fixes a target framework.

## Goals / Non-Goals

**Goals**

- Every first-party project compiles for `net11.0` with one language version, so the two BCL-backed
  C# 15 features become available to internal code.
- The extension-facing packages keep a `net10.0` asset with the same public API, and the gate proves
  an extension built against it still runs.
- The `dotnet tool` requirement is explicit: it needs the .NET 11 runtime, and the self-contained
  archives are the answer for a machine without it.
- No script or fixture spells the framework folder any more; moving it again is one edit.

**Non-Goals**

- The union/closed refactors themselves (`Lunate.Tui`'s `LiveArea` and `TranscriptEntry`, the
  `AgentEvent` and hook-result unions). Step (c) of #192, internal code first, and the contract
  unions are their own decision.
- Dropping `net10.0` from the published contract packages. That is a breaking contract change with a
  deprecation window and an `ExtensionApi` decision.
- A `net10.0` asset for the `dotnet tool` package.
- Any NuGet package that was not already referenced.

## Decisions

1. **One `TargetFramework` in `Directory.Build.props`, overridden per-project where needed.** The four
   extension-facing projects plus `Lunate.Ai`/`Lunate.Ai.Decisions` declare
   `TargetFrameworks=net10.0;net11.0` and clear the inherited `TargetFramework`. *Why not put
   `TargetFrameworks` in the props file and override it:* every project would then take the
   cross-targeting path (an outer build per project) for no gain, and the single-target projects are
   the majority. *Why the explicit clearing line:* MSBuild only sets `IsCrossTargetingBuild` when
   `TargetFramework` is empty; without the clear the project builds one framework and `pack` fails
   later with NU5026 — a failure far from its cause, so each file carries the comment. The extension
   template is the one multi-targeted project that publishes nothing: it declares both frameworks to
   prove the floor, so it needs the same clearing line, and the solution build (`lunate.slnx` lists
   it) is what compiles both assets. `ExtensionTemplateTests` pins the clear, because a template that
   inherits `net11.0` still builds green while proving nothing about `net10.0`.

2. **`LangVersion 15.0` at the repository level.** Targeting `net11.0` selects C# 15 anyway; the
   multi-targeted projects need it because they still build a `net10.0` asset. A fixed version (not
   `latest`) keeps the language a reviewed decision rather than a side effect of an SDK bump.

3. **`Lunate.Ai` and `Lunate.Ai.Decisions` multi-target too, though they are not published.** Their
   assemblies are *carried inside* `Lunate.Agent`'s package (`PackOwnAssemblies`), so their assets must
   match the package's: a `net10.0` `Lunate.Agent` cannot reference a `net11.0`-only harness. The
   project graph, not the publish list, decides.

4. **`Lunate.Extensibility.Testing` stays single-target `net11.0`.** The test kit carries the tool's
   assemblies (`Lunate.Extensibility`, `Lunate.Coding`, the TUI), which are `net11.0`-only, so a
   `net10.0` asset is impossible. Its pack path moves from the literal `lib/net10.0` to
   `lib/$(TargetFramework)`; this is why the template's *test* project targets `net11.0` while the
   template's *extension* project targets both.

5. **The tool package keeps one asset and says so.** A second `net10.0` tools asset would have to
   exclude the net11-only code paths, which is exactly the `#if` the inventory warned about. Rejected
   by the maintainer's decision on #192. The release note carries the sentence, and
   `docs/dev/distribution.md`'s asset table says it too.

6. **`Mermaid.Terminal` keeps a `net10.0` asset.** ADR-0039 makes it a standalone package for hosts
   that know nothing about the agent; the same compatibility argument applies, and it costs one
   project file. The consequence — no union or `closed` type in it — is recorded in
   `docs/dev/csharp-15.md` because the inventory proposed it as an example before this decision.

7. **The framework folder is derived, never spelled.** `scripts/lib.sh` exposes `target_framework`
   (read from `Directory.Build.props`), `scripts/verify.ps1` reads the same line, and
   `Lunate.TestSupport.BuildOutput.TargetFramework` serves the test fixtures that name a sibling
   build's output or a reference pack's `ref/<tfm>` folder. Two reference-pack fixtures
   (`CompileFixture`, `SingleFileLoopFixture`) now ask the host's own `SingleFileTarget.Current().Tfm`,
   which is also what the product's restore uses — so a fixture and the lock it asserts agree by
   construction rather than by a copied literal. `BuildOutputTests` pins the derivation against the
   running assembly's `TargetFrameworkAttribute`, because a wrong value sends tests to a folder that
   does not exist and the failure reads like a product bug.

8. **Toolchain bumps are part of the move, not a follow-up.** Roslyn `5.12.0` is the compiler the
   pinned SDK ships and the workspaces package requires `Microsoft.Build.Framework >= 18.7.1`; leaving
   the old versions would compile the flagship against last release's compiler. `NuGet.*` moves to
   `8.0.0` with it, since the SDK's own NuGet is 8-era. CSharpier stays at `1.4.0` — the pin that
   formats C# 15 (from #901) — because a formatter bump is not needed to compile the new syntax.

9. **The startup budget is re-measured, and left as calibrated.** A `net10.0` baseline binary was
   built from `origin/main` with the same SDK and measured against this branch's binary on the same
   machine in the same session: floors 141/125/119 ms (baseline) vs 140/129/146 ms (branch), medians
   170/169/170 vs 208/200/203. The gate reads the floor (ADR-0005 addendum), which is inside the
   noise; the median drift is what the addendum already documents on a contended machine. Rather than
   re-calibrate from a sandbox, the numbers are reported next to the previous release's and the budgets
   stay — a re-calibration is a measurement on the runner that fails, not a guess from another box.

## Risks / Trade-offs

- **An extension author or a host on `net10.0` cannot run the tool package any more.** -> The release
  note and the docs say it; the self-contained archives are unaffected and remain the default install.
- **A contract package could grow a net11-only public type unnoticed.** -> Both assets compile from the
  same sources with the same `PublicAPI.*.txt` files, so a divergence fails the build (and a `union` or
  `closed` modifier on a public type of a multi-targeted project does not compile for `net10.0` at all).
- **The gate compiles the contract projects twice and restores two frameworks' targeting packs.** ->
  Cost, not correctness: the extra asset per package is the price of the compatibility floor, and the
  gate already runs inside its wall-time budget.
- **A stale framework literal survives in a corner the diff does not touch.** -> The two classes of
  places that had them are now derived (`scripts/lib.sh`, `BuildOutput`), and the test fixtures that
  were literally named are the ones the gate exercises first.
- **The startup median drifted ~20% while the floor did not.** -> Reported with both statistics and
  both binaries; if the macOS runner's floor moves above its 300 ms budget, ADR-0005's re-calibration
  procedure opens with that measurement.

## Migration Plan

The change lands as one pull request, in this order: the framework declaration and the multi-targeted
projects; the derived framework folder in scripts and fixtures; the toolchain bumps; the docs, ADR and
OpenSpec change; the changelog fragment; the archive last. No user action is required beyond the
runtime note for `dotnet tool` users. Rollback is `git revert` of the change: the `net10.0` assets the
contract packages already carry are untouched by the move, so a revert is a build-configuration revert,
not a data migration.

## Open Questions

None. The two decisions that could have opened one — dropping `net10.0` from the contract packages, and
a `net10.0` tool asset — are explicitly out of scope per #192's decision and are recorded as such in
ADR-0041 and above.
