## ADDED Requirements

### Requirement: Fixture restores resolve the SDK's targeting packs offline

A fixture the gate restores SHALL NOT need a package source, and SHALL still restore when the pinned
SDK bundles targeting packs only for its own framework: the offline NuGet configuration the gate
writes into a fixture SHALL name each package source as cleared, and SHALL name the machine's NuGet
global packages folder as a fallback package folder, so a `net10.0` fixture restored by an SDK 11 that
ships only net11.0 packs resolves `Microsoft.NETCore.App.Ref`, `Microsoft.AspNetCore.App.Ref` and
`Microsoft.NETCore.App.Host.<rid>` from the packages the repository's own build already restored there.
The fallback folder SHALL be read-only for the restore child — the child writes its packages inside the
fixture — and SHALL be named only when it exists, because NuGet answers a missing fallback folder with
`NU1301` instead of falling through to the source list. A restore failure SHALL name the fallback folder
the configuration used, so the next reader can tell a missing cache from a broken fixture. Both offline
restores in the suite — the gate's fixture restore and the MSBL binding-order test's workspace restore —
SHALL be produced by one helper, so they cannot drift apart.

#### Scenario: A framework the SDK does not bundle still restores

- **GIVEN** a pinned SDK whose bundled targeting packs cover only its own framework, the repository's
  build has restored the packs for the product's framework into the machine's NuGet global packages
  folder, and a fixture that targets the product's framework
- **WHEN** the gate restores that fixture offline
- **THEN** the restore succeeds with no package source named, and the packs are resolved from the
  machine's global packages folder without being copied into it

#### Scenario: A missing machine cache is not a second confusing failure

- **GIVEN** a machine with no NuGet global packages folder
- **WHEN** the gate writes its offline configuration
- **THEN** the fallback folder is not named at all, so the child fails with the missing package instead
  of `NU1301`, and the failure report says which fallback folder the configuration would have used

#### Scenario: The two offline restores share one configuration

- **GIVEN** the gate's fixture restore and the MSBL binding-order test's workspace restore
- **WHEN** either writes its `NuGet.config`
- **THEN** both texts come from the same helper, name no network source, and name the same fallback
  folder

## MODIFIED Requirements

### Requirement: SDK pinning

The CI, CodeQL and release workflows SHALL install the .NET SDK version pinned in `global.json`. The pin SHALL be one exact version — `rollForward: disable` and `allowPrerelease: false` — so a patch that Microsoft has staged but not published cannot change what the gate installs; the pinned SDK also decides the runtime bundled into the self-contained binary, so the version is a product decision, not only a build input. A newer patch SHALL arrive as a Dependabot pull request against `global.json` (the `dotnet-sdk` ecosystem, weekly), which runs the full gate before merge; when a newer feature band carries fixes that affect the gate (for example test-runner exit behaviour on Windows), the pin SHALL move in its own change with the rationale recorded. A bump that changes which targeting packs the SDK bundles for the product's framework SHALL land in one change together with the gate's offline pack resolution, because such a bump is red on the operating systems the merge queue does not run, and the Dependabot configuration SHALL record that coupling next to the pin it bumps.

#### Scenario: Workflows resolve the pinned SDK

- **GIVEN** a workflow run on any runner
- **WHEN** the SDK is set up
- **THEN** the installed version is the one pinned in `global.json`

#### Scenario: A staged-but-unpublished patch cannot change the install

- **GIVEN** setup metadata that advertises a patch the download feed does not serve
- **WHEN** a workflow sets up the SDK
- **THEN** the pinned version is installed and the run continues, instead of resolving the phantom patch and failing

#### Scenario: A published patch arrives as a reviewed pull request

- **GIVEN** a newer patch of the pinned feature band published on the feed
- **WHEN** Dependabot's weekly run completes
- **THEN** one pull request bumps `global.json`, and the full gate runs on it before anyone merges

#### Scenario: A gate-relevant band fix moves the pin

- **GIVEN** an SDK feature band newer than the pin carrying a fix that affects gate behaviour
- **WHEN** the pin is bumped
- **THEN** the change records the measured before/after and the workflows install the new version

#### Scenario: A bump that changes the bundled packs is not merged alone

- **GIVEN** an SDK bump whose SDK no longer bundles the targeting packs of the framework the product
  and its fixtures target
- **WHEN** the bump is proposed
- **THEN** it lands in the same change as the gate's offline pack resolution, and the Dependabot
  configuration next to the pin records that the two belong together
