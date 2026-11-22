## Context

The probes already exist, but each belongs to a different feature: `LunateHome` resolves the state
root, `SettingsStore` reads configuration, `AuthStore` and `ProviderStore` hold credentials and
entries, `HarnessFactory` loads the catalog, `UpdateCheckCache` holds the last daily answer,
`TerminalCapabilities` detects the terminal and `ExecutionEnvironmentSelection` names a container
engine. Nothing composes them into one read-only report.

The command runs from `Lunate.Coding` and is registered in `Cli` before launch parsing, like
`cache`, `packages` and `trust`.

```text
lunate doctor
    |
    v
DoctorCommand.Run(args, output, errors, seams)
    |
    +--> DoctorReport.Collect(seams)
    |      +--> LunateHome / AuthStore sibling paths (state + settings)
    |      +--> SettingsStore.Resolve
    |      +--> AuthStore.Load + ProviderStore.Load
    |      +--> HarnessFactory.LoadCatalog + ChatModelResolver
    |      +--> UpdateCheckCache.Read (no request)
    |      +--> TerminalCapabilities.Detect
    |      +--> PATH lookup for docker and podman
    |
    +--> DoctorView.Text(report, output) or DoctorView.Json(report, output)
    +--> 0 healthy / 1 required path or settings problem / 2 usage
```

## Goals / Non-Goals

**Goals:**

- One offline command that answers the install, path, provider, container, terminal and update
  questions a bug report starts with.
- A stable JSON object for pasting into an issue.
- A health signal that is only about the required paths and a readable settings file.
- Testable seams for path, environment, terminal, process path, PATH lookup, clock and update cache.

**Non-Goals:**

- No provider reachability probe, no release request, no container start, no extension or MCP host.
- No new public API and no Telemetry-style upload.
- No redaction pass over the whole report: secret values are never read into a reported field.

## Decisions

- **A new `DoctorCommand` beside the other subcommands.** The report needs only the settings,
  auth/provider, catalog, update-cache and terminal types already in `Lunate.Coding`; a new
  assembly would add a project and a dependency for one command. Alternative: extend `lunate auth
  status`, rejected because that command is about credentials and must stay useful without a
  catalog or terminal.
- **Config means `settings.json`; state means the state root.** Lunate keeps every user file in one
  root, so the two facts a bug report needs are the root and the one file whose absence explains a
  first-run refusal. `auth.json` and `providers.json` are reported only through credential presence.
- **Required paths are the state root and the settings file.** Both missing cases are precisely the
  facts the command exists to report, and both make "the basics" unhealthy. A missing auth entry,
  engine or model is a fact, not an exit-code failure. Alternative: exit non-zero only when the
  state root is missing, rejected because a configured state root with no settings file is still the
  first-run case the reader is diagnosing.
- **The report is built from non-secret values; credential presence is a boolean plus a source.**
  `AuthStore.TryGet` is used only to answer present/absent, and the value never enters the report
  model. Alternative: a redaction pass over a rendered string, rejected because a redactor can miss
  a value the report never needed to hold.
- **Install channel from the executable path.** `.dotnet/tools`, `Cellar/lunate` and
  `scoop/apps/lunate` are stable markers of the three package managers; everything else is the
  self-contained archive (including a working-tree build). Alternative: ask the package manager or
  read a manifest, rejected because it would run a process and could need a network.
- **`--json` is one compact object with lower-camel keys; the text view is one fact per line.** Both
  are rendered from the same `DoctorReport` record, so they cannot drift. `problems` carries the
  same text the human view prints, and the exit code is computed once from that list.
- **The update-check answer is read from the cache only.** A doctor run must work on an offline
  machine and must not consume the daily request budget.

## Risks / Trade-offs

- [A process launched through a symlinked Homebrew shim may not expose its real path] -> the
  detection falls back to `self-contained`; a wrong channel is a report fact, not a failure, and the
  path itself is printed so the reader can correct it.
- [The provider/model can be unresolved because the catalog is malformed] -> collection catches the
  load error into `problems`, still prints every other fact and exits 1, matching the unreadable
  settings case.
- [A new command adds a second place that reads the update cache] -> it calls the existing
  `UpdateCheckCache.Read` and never calls `UpdateCheck.CheckAsync`.
- [Auth-store loading can migrate providers] -> `ProviderStore.Load` is read-only until a migration
  is needed; the command passes the same paths the session uses, and tests cover a seeded auth store.

## Migration Plan

No migration. The command is additive, has no stored format and no default-launch change. Existing
commands and files keep their behaviour.

## Open Questions

None.
