## 1. The offline configuration

- [x] 1.1 `FixtureRestore.MachinePackagesFolder()` names the folder a fixture restore may read packages
      from: `NUGET_PACKAGES` of the test host when set, else `<user profile>/.nuget/packages`, as an
      absolute path — and `LUNATE_TEST_MACHINE_PACKAGES` first, for the MSBL probe child which repoints
      `NUGET_PACKAGES` at a folder of its own.
- [x] 1.2 `FixtureRestore.OfflineNuGetConfig()` produces the configuration the gate writes into a
      fixture: every package source cleared, and the machine folder named as one fallback package folder
      when — and only when — it exists, because NuGet answers a missing fallback folder with `NU1301`.
- [x] 1.3 `FixtureRestore.Restore` writes that text, and `ChildProcessDiagnostics.Describe` prints the
      folder (or `<none>`) with the child's environment so a failed restore names it.
- [x] 1.4 `RestoreCache`'s format moves to `v3`: the restore recipe changed, so the artifacts of the old
      one retire.

## 2. The second offline restore

- [x] 2.1 `MsblBindingOrderTests.Restore` writes `FixtureRestore.OfflineNuGetConfig()` instead of its own
      copy, so the gate's two offline restores cannot drift.

## 3. The tests

- [x] 3.1 `FixtureRestoreEnvironmentTests.The_offline_configuration_names_the_machine_cache_as_a_fallback_folder`:
      the text clears the sources, names the machine folder as one fallback folder, names no network
      source, and does not name a fallback folder that does not exist.
- [x] 3.2 `Lunate.Roslyn.Tests.FixtureRestoreTests` (the two restoring tests) and
      `MsblBindingOrderTests` are the behavioural proof: they failed with `NU1101` under SDK 11 before
      this change and pass after it, with no package source named.
- [x] 3.3 `FixtureRestoreEnvironmentTests` keeps its existing assertions — the child's own folders,
      the one-node argument list, the never-silent failure report — unchanged.

## 4. The pin and the record

- [x] 4.1 `global.json` stays at the SDK bumped in #892 (`11.0.100`): the bump is in this branch's
      history and the fix lands on it.
- [x] 4.2 `.github/dependabot.yml` records, next to the pin, that an SDK bump which changes the bundled
      targeting packs lands together with the gate's offline pack resolution.
- [x] 4.3 `changes/898.md` carries the release-note line.

## 5. Verification

- [x] 5.1 `dotnet csharpier format .` and `dotnet csharpier check .` on the changed files.
- [x] 5.2 `openspec validate --all --strict`.
- [x] 5.3 Release build of every touched project, and the named tests: `FixtureRestoreTests`,
      `FixtureRestoreEnvironmentTests`, `MsblBindingOrderTests`, the Roslyn workspace tests and the
      Coding Roslyn fixture tests. The lane's sandbox cannot run the MSBuild-workspace tests: its
      `$TMPDIR` is 63 characters, so Roslyn's build host cannot bind its Unix socket — the
      `CoreFxPipe_` path is longer than the platform's 104 — and the load comes back partial. CI runs
      those; the offline restores themselves pass here (`FixtureRestoreTests`, 5 tests).
