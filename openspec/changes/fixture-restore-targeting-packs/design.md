## Context

The gate restores copied fixture solutions in a child `dotnet restore` whose every writable folder is
inside the fixture: `NUGET_PACKAGES`, `DOTNET_CLI_HOME`, the HTTP cache and the temp directory
(`tests/Lunate.TestSupport/FixtureRestore.cs`, #259/#261). The source list is cleared because the
fixtures carry no packages: the restore is meant to prove that MSBuild can load the tree, not to
download anything.

That held while the pinned SDK bundled the targeting packs of the framework the product targets. SDK
11.0.100 bundles only net11.0 packs: for a `net10.0` project it records three *download dependencies* —
`Microsoft.NETCore.App.Ref` 10.0.15, `Microsoft.AspNetCore.App.Ref` 10.0.15 and
`Microsoft.NETCore.App.Host.<rid>` 10.0.15 — and asks a feed for them. The only source the child has is
the SDK's own `library-packs` folder (the SDK injects it), which carries workload packs, not the
targeting packs. So every isolated restore failed with `NU1101`, on macOS and Windows alike, and 21
tests went red (#895).

```
Lunate.TestSupport.FixtureRestore  ──writes──►  <fixture>/NuGet.config   <packageSources><clear/>
                                   └──runs───►  dotnet restore --packages <fixture>/.packages
                                                  └─ asks library-packs for the net10.0 packs ─► NU1101
```

The repository's own build restores those same three packages — it targets `net10.0` under the same SDK
— into the machine's NuGet global packages folder (`NUGET_PACKAGES`, else `~/.nuget/packages`). They are
content-addressed and versioned by the SDK, so they are as reproducible as the SDK pin itself.

Constraints: no new dependencies; no network in a fixture restore (that is what the isolation was for);
the fixture's package folder must stay private; `RestoreCache` reuses the artifacts of an identical tree
across processes and machines' runs.

## Goals / Non-Goals

Goals:

- The isolated restores resolve the SDK's targeting packs offline, on all three operating systems.
- The isolation that fixed the flaky gate stays: cleared sources, private writable folders, one restore
  at a time.
- One configuration for both offline restores in the suite (`FixtureRestore` and the MSBL order test).
- A failure names the fallback folder, so a missing cache is distinguishable from a broken fixture.

Non-Goals:

- No product code, no product `NuGet.config`, no change to how a user's extension restore resolves packs.
- Not a container image or a vendored pack folder in the repository: the packs are the SDK's, and the
  repository's build already fetches them.
- Not a change to the SDK pin or the product's target framework.

## Decisions

### The machine's global packages folder as a NuGet *fallback* package folder

`<fallbackPackageFolders>` is NuGet's own mechanism for "packages that are already on this machine, read
them where they are": the restore resolves against it, never copies into it, and never writes to it. It
is declared in the fixture's `NuGet.config` next to the cleared source list, so the isolation is
unchanged and no feed is named.

Alternatives:

- *Re-add nuget.org as a source.* Works, but puts an HTTP request into every fixture restore and brings
  back exactly the class of flake the isolation was introduced to end. It also makes the gate's result
  depend on a service.
- *Use the machine's global packages folder as the fixture's `--packages` folder* (the shape
  `WriteVerificationBudgetTests` uses for its own fixture). No: a shared *writable* package folder is
  where two restores on a loaded runner collide, which is what #259 removed.
- *Point at the SDK's `packs/` folder.* The SDK resolves a targeting pack from `packs/` before any
  download, and it does not have the 10.0.15 packs — that is the defect. Copying a `packs`-shaped tree
  into the fixture and passing `NetCoreTargetingPackRoot` would work but needs a second, hand-built
  layout of the same packages, including symlink/copy handling on Windows for something NuGet already
  does.
- *Vendor the three nupkgs into the repository.* Megabytes of binary in git, and they would have to be
  bumped with every SDK patch.

### One helper produces both offline configurations

`FixtureRestore.OfflineNuGetConfig()` returns the configuration text, and
`FixtureRestore.MachinePackagesFolder()` returns the folder it names. `MsblBindingOrderTests.Restore`
writes the same text instead of its own copy, so the two cannot drift: a change to the offline recipe
changes both tests at once.

### The fallback folder is named only when it exists

NuGet answers a missing fallback folder with `NU1301` ("Source ... does not exist") for every project,
which would replace one unexplained failure with another. So the helper reads the machine folder,
`NUGET_PACKAGES` of the test host first (`~/.nuget/packages` otherwise), and emits the entry only when
the directory exists. `ChildProcessDiagnostics.Describe` prints the folder — or `<none>` — next to the
child's environment, so the failure report still says what the configuration would have used.

### `RestoreCache` format bump

The cache comment says the format is bumped when "the artifacts, their layout or the restore recipe
change". The recipe changed: a restore now resolves packages through a fallback folder, so the assets
file records package references it did not record before. `v2` → `v3` retires those entries instead of
handing a test a tree restored with an older recipe.

## Risks / Trade-offs

- [The machine's cache is empty, e.g. a developer runs the suite with `--no-build` after building with
  an older SDK] → the failure is the same `NU1101` as today, and the report now names the fallback folder
  it used, so the fix ("build the repository under the pinned SDK") is one line away. On CI the gate
  builds before it tests, and the build restores exactly these packs.
- [A fixture that gains a real package would silently resolve it from the machine's cache] → the
  fixtures are package-free by design and the source list stays cleared; a package that the SDK does not
  request and the build did not restore is still a hard `NU1101`. `FixtureRestoreTests.A_tree_that_differs_is_restored_on_its_own`
  keeps the cache keyed on tree content, so a tree that adds a package reference restores on its own.
- [A fallback folder makes the restore's result depend on what the machine happens to have cached] → the
  packages are content-addressed and pinned by the SDK's own `KnownFrameworkReference`, so the same SDK
  pin resolves the same versions everywhere.

## Migration Plan

No migration: the change is test infrastructure plus one configuration comment. Rollback is reverting
the commit; a branch built with the old recipe keeps working on an SDK that bundles the packs.

## Open Questions

None.
