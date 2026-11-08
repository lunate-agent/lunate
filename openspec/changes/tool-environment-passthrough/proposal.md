## Why

The tool child's environment policy is absolute: it inherits the user's whole environment minus
every name Lunate resolves as a provider credential. That strip closes a real leak (#621), but it
leaves a user no way to say "this one variable is mine and my tool child must see it" — a project
token, a build-machine marker, or a proxy variable whose name happens to collide with a credential
name. Today the only options are "everything except Lunate's names" or nothing.

## What Changes

- A new user-scope setting, `toolEnvironment.passthrough`: an array of **exact** environment
  variable names the user allows into tool processes. No wildcards, no prefixes, no patterns.
- The `Inherited` policy applies the pass-through **after** the credential strip, so an explicitly
  named variable wins over the deny for that exact name and nothing else. Names the parent does not
  hold stay absent.
- A passed-through name that is also a Lunate credential name is an explicit user act: it is
  allowed, and startup prints one warning naming it instead of silently allowing or refusing.
- The `Scrubbed` policy is unchanged — extension hosts never get the inherited set, so the
  pass-through list does not apply to them.
- The pass-through list is read where the tool child's policy is built
  (`ChildProcess.BuildEnvironment`), never at a spawn site. No public API change and no new package.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `child-environment`: the `Inherited` policy gains an explicit pass-through list applied after the
  credential strip, exact-name only.
- `agent-config`: a new user-scope settings key `toolEnvironment.passthrough` with its parse,
  validation, schema and generated reference entry.

## Impact

- `src/Lunate.Extensibility.Abstractions/EnvironmentScrubber.cs`: `Inherited` re-admits the exact
  pass-through names from the inherited set after the strip.
- `src/Lunate.Coding/ChildProcess.cs`: the one place that builds the tool child's policy names the
  pass-through list it reads.
- `src/Lunate.Coding/ToolEnvironmentPassthrough.cs` (new): the process-wide configured list and the
  collision warning, mirroring how `CredentialVariables` holds the strip list.
- `src/Lunate.Coding/SettingsStore*.cs`, `settings-schema.json`, `docs/configuration.md`,
  `docs/guide.md`: the setting, its validation and its documentation.
- `src/Lunate.Coding/InteractiveSession*.cs`, `PrintMode.cs`, `AcpMode.cs`: configure the list from
  resolved settings at startup and emit the one warning.
- Tests: `EnvironmentScrubberTests`, `SettingsStore`/schema tests, `ChildEnvironmentGuardTests`, and
  a POSIX and a Windows spawn test that a passed-through name reaches the real child.
