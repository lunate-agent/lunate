## Context

The repository already pins .NET SDK 11 (`global.json`) and builds the .NET 10 product with it. The
C# 15 / .NET 11 inventory ([`docs/dev/csharp-15.md`](../../../docs/dev/csharp-15.md), #899) records
the probes: union types and closed hierarchies need the `net11.0` BCL (`IUnion`,
`UnionAttribute`, `IsClosedTypeAttribute`); collection-expression arguments and labeled jumps need
only the C# 15 compiler. The inventory's target-framework decision is what this change applies.

Current state after step (a):

- `Directory.Build.props` sets `TargetFramework=net10.0` for every project.
- `Lunate.Coding` packs as a framework-dependent `dotnet tool`, so its current asset is
  `tools/net10.0`.
- The published extension-facing packages (`Lunate.Contracts`,
  `Lunate.Extensibility.Abstractions`, `Lunate.Agent`) each ship one `net10.0` asset.
- Extension compilation already derives its target from the host runtime
  (`SingleFileTarget.Current()`), so it moves with the runtime; only test fixtures and script paths
  hard-code `net10.0`.
- `scripts/perf.sh` (local default 150 ms) and `scripts/memory.sh` (150 MB gate) hold the startup and
  memory budgets calibrated in ADR-0005 / ADR-0009.

## Goals / Non-Goals

**Goals:**

- The self-contained app and every internal project build and run on `net11.0` / C# 15.
- The published extension-facing packages carry both a `net10.0` and a `net11.0` asset, with an
  identical public surface, so an existing extension keeps compiling and loading.
- The `dotnet tool` package is a `net11.0` framework-dependent tool and its release note states the
  .NET 11 runtime requirement.
- Startup and memory are measured again on the .NET 11 publish, with the evidence recorded.

**Non-Goals:**

- No C# 15 refactor (union/closed types) in this change; that is step (c).
- No drop of the `net10.0` contract asset, and no change to `ExtensionApi`.
- No new package, no new public type, no change to the extension lock format.
- No per-runner CI budget re-calibration: only the post-merge runners can measure it.

## Decisions

### 1. The target-framework split

`Directory.Build.props` sets `TargetFramework=net11.0`. The published extension-facing projects
override it with `TargetFrameworks=net10.0;net11.0`:

- `Lunate.Contracts`
- `Lunate.Extensibility.Abstractions`
- `Lunate.Agent`
- `Lunate.Ai` and `Lunate.Ai.Decisions` (the assemblies `Lunate.Agent` carries in its `lib/`)

`Lunate.Extensibility.Testing` stays `net11.0` because it references `Lunate.Coding` (the tool), and
the tool is `net11.0` only. `Mermaid.Terminal` stays `net11.0` because it is not extension-facing and
is a candidate for the step (c) refactor.

Alternative rejected: multi-target the whole graph so the tool keeps a `net10.0` asset. It doubles
every build, contradicts the decision that the app is `net11.0`, and buys a runtime floor the card
explicitly replaces.

### 2. The tool package requires .NET 11

`Lunate.Coding` packs one `tools/net11.0` asset. The package metadata and the changelog fragment say
that `dotnet tool install -g lunate` now needs the .NET 11 runtime; the self-contained installers are
unchanged and still need no runtime. Alternative rejected: carry both `tools/net10.0` and
`tools/net11.0`, which the inventory tested but which the card's acceptance replaces with the
explicit .NET 11 requirement.

### 3. Package layout per asset

`Lunate.Agent` and `Lunate.Extensibility.Testing` use `lib/$(TargetFramework)` in their
`TfmSpecificPackageFile` paths instead of a hard-coded `lib/net10.0`. `Lunate.Agent` then packs its
own `Lunate.Ai.dll` and `Lunate.Ai.Decisions.dll` beside each asset; the contract packages ride the
dependency chain as before.

### 4. Extension compile target follows the host

No product change: `SingleFileTarget.Current()` already produces `net{runtime.Major}.{runtime.Minor}`
from `Environment.Version`. The tests and fixtures that hard-code `ref/net10.0` for finding the
machine's reference pack change to the current TFM; package-asset strings that describe a dependency
package (for example `lib/net10.0/Octokit.dll`) stay as they are, because they model a real package
layout, not the host.

### 5. The template proves the net10 floor; its test project moves to net11

`TemplateExtension.csproj` keeps `TargetFramework=net10.0` explicitly (it is self-contained outside
this repository). `TemplateExtension.Tests.csproj` moves to `net11.0` because the test kit it
consumes is `net11.0`; the copy target reads the extension output at its own `net10.0` path. The
copy end-to-end test asserts the packed contract packages carry both `lib/net10.0` and `lib/net11.0`
entries, so the floor and the new asset are both proven.

### 6. Documentation and scripts follow

Live instructions (`AGENTS.md`, `openspec/config.yaml`, `CONTRIBUTING.md`, `docs/dev/guide.md`,
`docs/dev/csharp-15.md`, `docs/extensions/README.md`, `README.md`, `docs/quickstart.md`,
`docs/dev/distribution.md`) name .NET 11 / C# 15 and the tool's runtime requirement. Live scripts
that address build output use `net11.0`. Historical records (`docs/dev/spikes/**`,
`openspec/changes/archive/**`, `docs/dev/eval/**`) keep their original paths.

### 7. Budgets

Re-measure on the locally published single-file binary with `scripts/perf.sh` and
`scripts/memory.sh`, record the floor/median and peak RSS in this design and in the pull request. The
150 ms local startup default stays: every measured floor (131–148 ms) is inside it (ADR-0005
addendum). The published single-file ReadyToRun memory subject is above the old 150 MB gate, so the
local default is re-calibrated under ADR-0009's own rule: 353 × 1.5 = 529.5, rounded up to the next
50 MB, gives 550 MB. The ADR-0009 addendum records the value and reason. The 100 MB target stays the
session-footprint goal (reported, not enforced), and the per-runner CI `memory_gate_mb` values stay
provisional until the post-merge/nightly runners supply their own subject and measurement (ADR-0005,
ADR-0009). Where the sandbox denies a gate's own seam — no pty can be allocated and `ps` is blocked —
the same subject is measured with the gate's method and the substitution is recorded (see
Measurements).

## Measurements

Taken on the implementation machine (macOS 26.3.1, osx-arm64, self-contained single-file Release
publish with ReadyToRun, load average about 2-5). The .NET 10 baseline is the same source at
`493c401f` (the branch base), published and measured with the same commands.

### Startup

| Measurement | .NET 10 baseline | .NET 11 (#902) |
| --- | --- | --- |
| Published binary | 377 MiB | 439 MiB |
| `scripts/perf.sh` startup floor | 122 ms | 131–148 ms (three runs) |
| `scripts/perf.sh` startup median | 158 ms | 186–202 ms (three runs) |
| `--version` peak RSS (`/usr/bin/time -l`) | 434 MiB | 537 MiB |

The startup floor is the gate's statistic (ADR-0005 addendum); on net11 it is at or below the 150 ms
local default in all three runs, so the local budget is unchanged. The medians on this machine are
above 150 ms for the **net10 baseline as well**, so the shared-machine load, not the retarget, is the
dominant term; a quiet re-calibration and the per-runner CI numbers belong to the post-merge/nightly
run (ADR-0005).

### Idle TUI memory (ADR-0009)

`scripts/memory.sh idle` needs a long-lived interactive subject and `ps`, and this sandbox denies
the terminal it needs (`/dev/ptmx: Operation not permitted`, so no pty can be allocated) and `/bin/ps`.
The subject was therefore measured with the ADR-0009 method — peak RSS of the whole process tree,
sampled every 150 ms from outside the process over a 20 s idle window — over the product's own
interactive session, with two sandbox substitutions:

- the RSS source is libproc's `proc_pid_rusage(RUSAGE_INFO_V2).ri_resident_size`, the counter `ps`
  reads (it reports 531 MB for `lunate --version`, against the 537 MiB `/usr/bin/time -l` row above);
- the terminal is the product's own `IConsoleIO` seam implemented as a virtual 120x30 screen that
  never sends a key. The session itself is composed by the real `Cli.Run` with an empty argument
  list, over a configured stub model with the update check off (`--offline`); it paints the welcome
  header and idles exactly as a configured install does. The missing terminal driver and the absent
  update check are the only differences from the shipped idle TUI.

The subject binary is a scratch host that references the product's projects, so its single-file
ReadyToRun bundle is within 0.1 MiB of the published binary (439 MiB net11) and carries the same
session. It is published with the release flags (`--self-contained true -p:PublishSingleFile=true
-p:PublishReadyToRun=true`); ADR-0009 calibrated the 150 MB gate on the S-5 shell baseline (82 MB),
so the same host is measured in three packaging shapes to separate the TUI's own footprint from the
bundle mapping. RSS is reported in the gate's unit (`scripts/memory.sh`'s kB/1024 rounding).

| Idle TUI peak RSS (20 s window, two runs each) | .NET 10 baseline | .NET 11 (#902) |
| --- | --- | --- |
| Framework-dependent (`dotnet <host>.dll`) | 85 MB | 88 MB |
| Single-file + ReadyToRun (bundle 377 / 439 MiB) | 298 MB | 353 MB |
| Single-file, no ReadyToRun (bundle 137 MiB) | not measured | 85 MB |

Verdict against the idle memory gate (ADR-0009):

- The shipped single-file **ReadyToRun** subject peaks at 353 MB on net11 (298 MB on net10), above
  the old 150 MB gate. ADR-0009 calibrated that gate on a single-file + ReadyToRun subject, so the
  88 MB framework-dependent row does not replace it. Under ADR-0009's median + 50% rule, 353 × 1.5 =
  529.5 rounds up to 550 MB; the local default gate is now 550 MB and the ADR-0009 addendum records
  the value and reason.
- The TUI's own footprint remains inside the old gate. At 88 MB it is 3 MB above the net10 baseline
  (85 MB, +3.5%) and 6 MB above the 82 MB S-5 shell baseline, so the 100 MB target stays the
  session-footprint goal; it is reported, not enforced.
- The excess is the mapped ReadyToRun images of the uncompressed bundle, not the session: the same
  net11 host without ReadyToRun maps 137 MiB of bundle and peaks at 85 MB, and the
  framework-dependent host peaks at 88 MB. The retarget moves the ReadyToRun row by 55 MB (+18%)
  because the bundle grew 377 → 439 MiB, tracking the bundle rather than the session.

The per-runner CI `memory_gate_mb` values stay provisional until the post-merge/nightly runners
measure their own subject; the CI memory step has no `MEMORY_SUBJECT` configured
(`scripts/memory.sh` reports no subject and self-tests), so only then can its numbers be
re-calibrated (ADR-0005, ADR-0009).

## Diagram

Deployment view of what this change ships:

```mermaid
flowchart LR
    subgraph Host["Developer or user machine"]
        Tool["dotnet tool lunate<br/>tools/net11.0<br/>requires .NET 11 runtime"]
        Self["self-contained lunate<br/>osx-arm64 / win-x64 / linux-x64<br/>bundles .NET 11"]
        Ext10["extension built for net10.0"]
        Ext11["extension built for net11.0"]
    end
    subgraph Feed["NuGet feed"]
        Contracts["Lunate.Contracts<br/>lib/net10.0 + lib/net11.0"]
        ExtAbs["Lunate.Extensibility.Abstractions<br/>lib/net10.0 + lib/net11.0"]
        Agent["Lunate.Agent<br/>lib/net10.0 + lib/net11.0"]
    end
    Tool --> Feed
    Self --> Feed
    Ext10 --> Contracts
    Ext10 --> ExtAbs
    Ext11 --> Contracts
    Ext11 --> ExtAbs
    Self -. "loads" .-> Ext10
    Self -. "loads" .-> Ext11
    Tool -. "loads" .-> Ext10
    Tool -. "loads" .-> Ext11
```

Boundaries: the self-contained binary and the tool are both the `net11.0` host; the extension
packages are the compatibility boundary and carry both assets. Assumption: an extension targeting
`net10.0` binds to the host's `net11.0` contract assembly at load because the public surface is
identical; that is already the loader's behaviour and the reason the net10 asset is kept.

## Risks / Trade-offs

- **A `net10.0` asset of a dependency package does not exist** → the multi-target build restores
  per TFM; the build fails loudly if a package cannot provide a `net10.0` asset, and the dependency
  versions stay unchanged.
- **The public API drifts between assets** → `Microsoft.CodeAnalysis.PublicApiAnalyzers` runs per TFM
  against the same `PublicAPI.*.txt`; a divergence fails the build.
- **The extension test kit can no longer serve a net10 test project** → the template's test project
  moves to `net11.0`; the extension project itself stays `net10.0`, which is the floor that matters.
- **The tool's local roundtrip needs a .NET 11 runtime** → the pinned SDK and CI runners already
  install .NET 11; the changelog and distribution docs state the requirement.
- **A budget regresses on the new runtime** → the local re-measurement catches it before the branch
  is done; a measurement beyond the headroom re-calibrates per ADR-0005/ADR-0009.
- **CI budget values were measured on .NET 10** → keep them until the post-merge/nightly run can
  re-measure; record that as the maintainer's follow-up evidence rather than guessing new numbers.

## Migration Plan

1. Retarget `Directory.Build.props` and the multi-target project files.
2. Update the tests/fixtures and live script paths that name the host TFM.
3. Move the template test project to `net11.0` and assert both contract assets in the copy test.
4. Update live docs and the OpenSpec config; add the changelog fragment.
5. Build Release, run the focused tests, publish locally, measure startup and memory, and
   re-calibrate the local memory gate from the shipped ReadyToRun result (ADR-0009 addendum).
6. Roll back by reverting the branch; no data migration is involved.

## Open Questions

- The per-runner CI startup/memory budgets are calibrated from data only the post-merge (main push,
  nightly) runners produce. This change records the local re-measurement and re-calibrates the local
  memory default to 550 MB from the shipped ReadyToRun subject; the CI matrix values stay
  provisional until the first post-merge/nightly run with a subject. No in-force ADR needs
  supersession; ADR-0009 is amended by an addendum that records the recalibration.
