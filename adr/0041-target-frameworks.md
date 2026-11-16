# ADR-0041: .NET 11 for the tool, a `net10.0` asset for the extension contract

Status: accepted — 2026-11-16

## Context

`global.json` has pinned SDK `11.0.100` since #898, and #899's C# 15 inventory (`docs/dev/csharp-15.md`)
settled what the repository wants from the language and what each feature costs. The compiler-only
features (collection-expression arguments, labeled `break`/`continue`) are already in the code, while
the two that the maintainer's inventory lists first — union types and closed hierarchies — need the
`net11.0` BCL: a `net10.0` target fails with CS0518 (`System.Runtime.CompilerServices.IUnion`) and
CS0656 (`IsClosedTypeAttribute..ctor`). The target framework itself had not moved, so the tool built
`net10.0` with an SDK one release ahead of it and those two features stayed out of reach.

The target framework is not a private implementation detail here. `Lunate.Contracts`,
`Lunate.Extensibility.Abstractions` and the published harness packages are what an extension compiles
against: their assets are a promise to extension authors, and an extension built today against
`net10.0` has to keep loading in the host. Dropping `net10.0` from them would be a breaking contract
change that needs its own deprecation window, not a silent bump. The `dotnet tool` package is the
other end of the problem: it is framework-dependent, so its asset decides which runtime a user needs.

The in-force ADRs reviewed for this change are ADR-0001 (no AOT, single-file budgets), ADR-0002
(layering and the MEAI model types), ADR-0005 (the startup budget and its addendum, the floor rule),
ADR-0008 (release build shape), ADR-0017 (extensibility, whose contract packages are the ones
multi-targeting) and ADR-0039 (`Mermaid.Terminal` as a standalone package). None of them fixes a
target framework; ADR-0005's budgets are the only decision this one has to answer to, and it does so
by re-measuring rather than assuming.

## Decision

1. **One target framework for the product: `net11.0`.** The tool (`Lunate.Coding`), the app, the
   self-contained release archives and every first-party project build `net11.0`, declared once in
   `Directory.Build.props`. The archives carry their runtime, so a user still installs nothing.

2. **`LangVersion` is fixed, once, at `15.0`.** It lives beside the target framework instead of in the
   two projects that adopted C# 15 syntax ahead of the move, so the whole repository compiles with one
   compiler setting and a feature that needs both target frameworks is visible in the diff.

3. **The extension-facing packages multi-target `net10.0;net11.0`.** `Lunate.Contracts`,
   `Lunate.Extensibility.Abstractions`, `Lunate.Agent` (with the `Lunate.Ai` and `Lunate.Ai.Decisions`
   assemblies its package carries) and `Mermaid.Terminal` publish a `net10.0` asset and a `net11.0`
   asset. The two carry the same public API: no union, no closed hierarchy and no other net11-only
   construct appears on their surface while `net10.0` is a target. The public API files are shared by
   both assets, which is what keeps the promise checkable — a divergence fails the build.

4. **`Mermaid.Terminal` keeps its `net10.0` asset for the same reason, not because a host demands it.**
   It is a standalone package with hosts that know nothing about the agent (ADR-0039); the same
   compatibility argument applies to it, and a shared rule is cheaper to hold than a per-package one.

5. **The `dotnet tool` package is the `net11.0` asset alone, and the release note says so.** There is
   no `#if`-gated `net10.0` tool asset: the tool's project graph is the net11 graph, and a second tool
   asset would either fork the code or ship a binary the tool's own features do not compile for.
   `dotnet tool install -g lunate` therefore needs the .NET 11 runtime; the self-contained archives do
   not, and they are the install path for a machine without it.

6. **The extension template builds both assets.** `templates/extension/TemplateExtension` targets
   `net10.0;net11.0` and clears the inherited `TargetFramework`, so MSBuild takes the cross-targeting
   path and the solution build — `lunate.slnx` lists it — compiles both assets on every gate run. The
   copied-template end-to-end test loads the `net10.0` output in a `net11.0` host, so the floor is
   proven by building rather than by a claim in a README. The template's own test project targets
   `net11.0` because the test kit carries the tool's assemblies.

7. **The startup budget is re-measured, not assumed.** Per ADR-0005 and its floor addendum, the
   published binary was measured against a `net10.0` binary built from `origin/main` with the same SDK,
   on the same machine, in the same session: floor 119–149 ms before and 119–149 ms after, median
   169–170 ms before and 200–208 ms after. The budgets and the gated statistic stay as ADR-0005
   calibrated them — the floor, which is what the gate reads, moved inside the measurement noise.

8. **Clearing the inherited `TargetFramework` is part of the multi-target declaration.** MSBuild only
   takes the cross-targeting path when `TargetFramework` is empty, so each multi-targeted project
   clears the repository-wide value before declaring its `TargetFrameworks`. A project that forgets
   it silently builds the first framework alone and fails `pack` with NU5026 — the reason the rule is
   a comment in every file rather than folklore.

## Alternatives considered

- **Keep the product on `net10.0` and gate the two BCL-backed features behind an `#if`.** Rejected:
  it buys nothing — the feature set that motivated the move is exactly the one that needs the newer
  BCL — and it doubles the code paths the C# 15 features were meant to delete.
- **Multi-target the tool as well, `net10.0;net11.0` for the `dotnet tool`.** The shape the inventory
  first considered, and the one that keeps `dotnet tool install` working on a .NET 10 runtime. Rejected
  by the maintainer's decision on #192: the tool package requires .NET 11 and the release note says
  so, because a `net10.0` tool asset cannot carry the features the tool's own code will use.
- **Drop `net10.0` from the extension contract packages.** Rejected outright: every existing
  extension would break, and the contract's compatibility floor is the reason those packages exist as
  packages. It stays a future decision with a deprecation window, not part of this one.
- **Copy the two compilation paths per target instead of guarding the surface.** Rejected: two
  compilations of the same sources with a shared `PublicAPI.*.txt` already fails the build on any
  divergence, which is the property that matters.
- **Re-calibrate the startup budgets upward for the new runtime.** Rejected as the primary answer, in
  line with ADR-0005's addendum: the floor — the gated statistic — did not move materially, and the
  median drift is within what the addendum already documents as runner noise. A future calibration
  reopens it with a measurement, not with this change.

## Consequences

- An extension author needs the .NET 11 SDK to build and test, and may target either `net11.0` or
  `net10.0`; the host runs on .NET 11 and loads both. `CONTRIBUTING.md` and `docs/dev/guide.md` say so.
- A user who installs through the `dotnet tool` needs the .NET 11 runtime; a user who installs an
  archive does not. The release note carries the sentence.
- `union` and `closed` are now available to first-party code, and unavailable to the four
  multi-targeted projects. That boundary is a property of the decision, not an oversight, and the
  internally-scoped refactors (`Lunate.Tui`'s `LiveArea`, `TranscriptEntry`) are the first slice of it.
- The build restores both frameworks' targeting packs and compiles the multi-targeted projects twice,
  so the gate pays for a second asset per contract package. That is the price of the floor.
- Tests and scripts that name a build output folder must not spell its framework: `scripts/lib.sh`'s
  `target_framework` and `Lunate.TestSupport`'s `BuildOutput` derive it, and `BuildOutputTests` pins
  the derivation against the assembly's own `TargetFrameworkAttribute`.
- Dropping `net10.0` from the contract packages later is a separate, breaking decision: it needs its
  own ADR, an `ExtensionApi` major bump and a deprecation window.
