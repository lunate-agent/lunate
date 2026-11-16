# ADR-0041: everything targets .NET 11, with no multi-targeting

Status: accepted — 2026-11-15 (maintainer decision on #192)

## Context

The C# 15 / .NET 11 inventory ([`docs/dev/csharp-15.md`](../../docs/dev/csharp-15.md), #899)
established which features pay for themselves and what each needs at compile and run time. Union
types and closed hierarchies — the two features with the largest payoff for the event hierarchy, the
TUI live area and the Mermaid parser — cannot compile for `net10.0`: the compiler requires the
`net11.0` BCL types `IUnion`, `UnionAttribute` and `IsClosedTypeAttribute`, and a `net10.0` target
fails with CS0518/CS0656. Collection-expression arguments and labeled `break`/`continue` need only the
SDK 11 compiler.

The first target-framework decision on #192 (2026-11-15) split the repository: the app and the tool
on `net11.0`, the published extension-facing packages multi-targeting `net10.0;net11.0` so an
extension built against the previous LTS kept compiling and loading. The maintainer superseded its
multi-target clause on the same day. There are no external extension authors and one user, so
nothing needs a `net10.0` asset; the only thing the floor bought was a second asset per package and a
build that had to keep the contract surface free of the `net11.0` types the retarget exists to
unlock.

The in-force ADRs reviewed for this decision are ADR-0001 (no AOT, self-contained single-file
releases), ADR-0002 (project graph and Microsoft.Extensions.AI model types), ADR-0005 and its
addendum (startup budgets), ADR-0008 (release publish flags), ADR-0009 (memory budgets), ADR-0017
and its addenda (extensibility, extension packages and trust), ADR-0023 (first-party extensions),
ADR-0026 (Roslyn built-in boundary), ADR-0037 (CI gate sized by event), ADR-0039
(Mermaid.Terminal) and ADR-0040 (hook-based extensibility). None of them fixes a target framework;
the budgets they define still apply, and #902 re-measures the startup and memory numbers on the new
runtime as a post-merge maintainer step rather than with the retarget's pull-request evidence.

## Decision

1. **Everything in the repository targets `net11.0`.** `Directory.Build.props` sets
   `TargetFramework=net11.0` and no project overrides it with `TargetFrameworks`. That covers the
   tool, the app, the TUI, `Mermaid.Terminal`, the self-contained release binaries, every internal
   project, the test projects, the extension template and the published harness and contract
   packages: `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Agent`,
   `Lunate.Extensibility.Testing`, and the `Lunate.Ai` / `Lunate.Ai.Decisions` assemblies
   `Lunate.Agent` carries. Every fixture solution under `tests/fixtures/` is `net11.0` too, so the
   gate restores them against the pinned SDK's own targeting packs. A fixture whose subject is the
   multi-target shape — `tests/fixtures/verified-edits/multi-target` — writes that shape into its
   temporary copy at run time, so no project file in the repository declares more than one framework
   and the test that enforces this reads the fixtures like any other project.

2. **Nothing in the repository multi-targets.** A package ships one `lib/net11.0` asset, the tool
   ships one `tools/net11.0` asset, and the public API analyzer runs against one target. There is no
   `net10.0` compatibility floor to keep the contract surface clear of `net11.0`-only types. The
   target-framework test scans every project under the repository root, fixtures included; only
   `eval/tasks/` and `docs/dev/spikes/` are exempt as data.

3. **The `dotnet tool` package requires the .NET 11 runtime.** It is framework-dependent, so its one
   asset is `tools/net11.0`. The release note and the distribution documentation state the
   requirement; a user on .NET 10 either installs the .NET 11 runtime or uses a self-contained
   installer, which still bundles its runtime.

4. **Extension compilation follows the running host.** `SingleFileTarget.Current()` derives
   `net{runtime.Major}.{runtime.Minor}` from the process runtime, so extension compilation and its
   lock are `net11.0` with the host, without a second setting. Extensions built for this host chain
   are `net11.0`; there is no older asset to bind to, which is the breaking part of this decision.

5. **The question returns before the first external release or the first published library
   version.** Reintroducing multi-targeting then is a deliberate, breaking decision with its own ADR
   and a deprecation window — not a free addition made inside a feature card.

## Consequences

- Every project builds once, so the build and the gate carry no second target and step (c) of #192
  (union and closed types, internal first) is unblocked for internal code and for the public contract
  without an asset split to preserve. Directories that hold somebody else's project rather than a
  project of this repository — `eval/tasks/` (an eval subject) and `docs/dev/spikes/` (a record) —
  keep their own frameworks.
- An extension built against `net10.0` no longer has a `net10.0` contract asset to bind to. The
  extension template, the reference extensions and the samples are `net11.0`, and a third-party
  extension author would rebuild against the new packages. There are none today; this is why the
  decision is taken now rather than after a release.
- The tool's runtime floor rises: an existing `dotnet tool` user on .NET 10 must install the .NET 11
  runtime or switch to a self-contained installer. The release note says so.
- The startup and memory budgets are re-measured on .NET 11 after the merge (ADR-0005, ADR-0009), as
  the maintainer's own step; the per-runner CI values are re-calibrated from the post-merge/nightly
  runs, not guessed in the pull request.
- No new NuGet package and no public API change follow from this decision.

## Alternatives considered

- **Keep the `net10.0;net11.0` split for the contract packages** (the superseded decision).
  Rejected: it doubles five builds, needs the contract surface to stay `net10.0`-compatible, and
  protects a consumer that does not exist.
- **Multi-target the whole tool graph and ship `tools/net10.0` beside `tools/net11.0`.** Rejected:
  the tool graph cannot adopt the `net11.0` features under a `net10.0` target, and the card accepts
  the tool's .NET 11 requirement instead.
- **Keep a `net10.0` extension template as a floor proof.** Rejected with the floor: the template is
  a copy that lives outside the repository and must show what an extension for this host looks like.
