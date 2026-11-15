## Why

#892 bumped the pinned SDK to 11.0.100 and the macOS and Windows legs went red with 21 failures,
every one of them an isolated fixture restore (`NU1101`/`NU1100`). SDK 11 bundles only its own
net11.0 targeting packs, so a `net10.0` project asks for `Microsoft.NETCore.App.Ref` 10.0.15 — plus
`Microsoft.AspNetCore.App.Ref` and `Microsoft.NETCore.App.Host.<rid>` — as a package download, and
the gate's fixture restore clears every package source, so there is nowhere to get them from. Ubuntu
stayed green, and the merge queue runs Ubuntu only, so the bump landed (#895). Reverting to SDK 10
(#894) would only postpone it: the gate has to survive an SDK that is newer than the runtime the
product targets.

## What Changes

- The offline NuGet configuration the gate writes into a fixture gains a **fallback package folder**:
  NuGet's own global packages folder of the machine, which the repository's build has already filled
  with exactly the packs the SDK asks for (the build targets the same `net10.0` under the same SDK).
  Package sources stay cleared, so the restore still installs nothing from a feed, and the folder is
  read-only for the child, so the fixture's per-tree isolation is unchanged.
- One helper in `Lunate.TestSupport` produces that configuration text and names the folder, so the
  gate's fixture restore (`FixtureRestore`) and the MSBL order test's own offline workspace restore
  cannot drift apart.
- The fallback folder is named only when it exists — NuGet answers a missing one with `NU1301`, which
  would replace one confusing failure with another — and the failure report names the folder the
  restore used either way.
- `RestoreCache`'s format is bumped: the restore recipe changed, so the artifacts of the old recipe
  retire instead of being reused.
- A test pins the configuration (cleared sources, no network source, the machine folder as the one
  fallback folder), and the fixture-restore tests that failed under SDK 11 are the behavioural proof.
- The SDK pin stays at 11.0.100 — #892's commit is in this branch's history — and
  `.github/dependabot.yml` records that an SDK bump lands together with the gate's proof of it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `ci-pipeline`: the gate's fixture restores gain a requirement — a fixture that targets a framework
  the pinned SDK does not bundle packs for SHALL resolve those packs offline from the machine's own
  NuGet cache, as a read-only fallback folder, without naming a package source and without writing
  into the machine's folder.

## Impact

- `tests/Lunate.TestSupport/FixtureRestore.cs`, `RestoreCache.cs`, `ChildProcessDiagnostics.cs`: the
  offline configuration, the cache format and the failure report.
- `tests/Lunate.Extensibility.Compile.Tests/MsblBindingOrderTests.cs`: the second offline restore uses
  the shared configuration.
- `tests/Lunate.Coding.Tests/FixtureRestoreEnvironmentTests.cs`: the configuration is pinned there.
- `.github/dependabot.yml`: the coupling of an SDK bump to this gate is recorded next to the pin it
  bumps.
- No product assembly, no public API, no session format, no dependency change.
