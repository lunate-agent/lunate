# ADR-0040: .NET 11 targets, with a `net10.0` floor for the extension contract

Status: accepted — 2026-11-15 (maintainer decision on #192)

## Context

The C# 15 / .NET 11 inventory ([`docs/dev/csharp-15.md`](../../docs/dev/csharp-15.md), #899)
established which features pay for themselves and what each needs at compile and run time. Union
types and closed hierarchies — the two features with the largest payoff for the event hierarchy, the
TUI live area and the Mermaid parser — cannot compile for `net10.0`: the compiler requires the
`net11.0` BCL types `IUnion`, `UnionAttribute` and `IsClosedTypeAttribute`, and a `net10.0` target
fails with CS0518/CS0656. Collection-expression arguments and labeled `break`/`continue` need only the
SDK 11 compiler.

The maintainer's decision on #192 (2026-11-15) is the target-framework split this ADR records:

- the self-contained application and the `dotnet tool` move to `net11.0`;
- the published extension-facing packages multi-target `net10.0;net11.0`, so an extension built
  against the previous LTS keeps compiling and loading;
- dropping `net10.0` from a published contract package is a breaking change that needs its own ADR
  and a deprecation window, never a silent bump.

The in-force ADRs reviewed for this change are ADR-0001 (no AOT, self-contained single-file
releases), ADR-0002 (project graph and Microsoft.Extensions.AI model types), ADR-0005 and its
addendum (startup budgets), ADR-0008 (release publish flags), ADR-0009 (memory budgets), ADR-0017
and its addenda (extensibility, extension packages and trust), ADR-0023 (first-party extensions),
ADR-0026 (Roslyn built-in boundary), ADR-0037 (CI gate sized by event) and ADR-0039
(Mermaid.Terminal). None of them fixes a target framework; the budgets they define still apply and
are re-measured on the new runtime by #902.

## Decision

1. **The app and the tool target `net11.0`.** `Directory.Build.props` sets
   `TargetFramework=net11.0`; every first-party project that is not a published extension-facing
   package builds for `net11.0` and may use C# 15. The self-contained release binaries bundle the
   `net11.0` runtime, so users still install nothing.

2. **The `dotnet tool` package requires the .NET 11 runtime.** It is framework-dependent, so its one
   asset is `tools/net11.0`. The release note and the distribution documentation state the
   requirement. Carrying both `tools/net10.0` and `tools/net11.0` was verified possible in the
   inventory but is not chosen: it would force the whole tool graph to build for `net10.0`, and the
   card accepts the runtime requirement instead.

3. **The published extension contract multi-targets `net10.0;net11.0`.** This applies to
   `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`, and the `Lunate.Ai` /
   `Lunate.Ai.Decisions` assemblies `Lunate.Agent` carries. Both assets are packed into the same
   package, and their public API is identical: no union, closed or otherwise `net11.0`-only type may
   appear on the contract surface while `net10.0` remains a target. The analyzer runs per target
   framework against the same `PublicAPI.*.txt`, so a divergence fails the build.

4. **The extension compile target follows the running host.** `SingleFileTarget.Current()` derives
   `net{runtime.Major}.{runtime.Minor}` from the process runtime, so extension compilation and its
   lock move to `net11.0` with the host without a second setting. An extension compiled for
   `net10.0` still loads because it binds to the host-owned contract assemblies and their public
   surface is unchanged.

5. **The test kit is `net11.0` only.** `Lunate.Extensibility.Testing` references the tool graph, which
   is `net11.0`; it does not multi-target. A template copy keeps a `net10.0` extension project as
   the floor proof, while its test project targets `net11.0`.

## Consequences

- Step (c) of #192 (union and closed types, internal first and the public contract last) is unblocked
  for every internal project. The contract surface stays `net10.0`-compatible until that floor is
  deliberately dropped by a later ADR.
- The build gets a second target for five projects. That is the price of the compatibility floor and
  is bounded to the contract/harness packages.
- The tool's runtime floor rises: an existing `dotnet tool` user on .NET 10 must install the .NET 11
  runtime or switch to a self-contained installer. The release note says so.
- The startup and memory budgets are re-measured on .NET 11 by #902; the per-runner CI values are
  re-calibrated from the post-merge/nightly runs under ADR-0005/ADR-0009, not guessed here.
- No new NuGet package and no public API change follow from this decision.
