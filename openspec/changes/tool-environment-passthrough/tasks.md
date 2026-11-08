# Tasks — tool-environment-passthrough

## 1. Setting, schema and docs

- [ ] 1.1 Add `ToolEnvironmentSettings` (`Passthrough`) to `AgentSettings` and parse the user-scope
      `toolEnvironment` object: `passthrough` is an array of exact, non-empty variable names; a
      value containing `=` or a NUL, and a duplicate name, are refused with the key named; an
      unknown key under `toolEnvironment` is refused like every other section.
- [ ] 1.2 Add the sorted `toolEnvironment.passthrough` entry to `settings-schema.json` (type
      `string array`, default `[]`, scope `user`), regenerate `docs/configuration.md` with
      `scripts/docs-sync`, and add the guide bullet under the `bash` tool's environment policy.
- [ ] 1.3 Extend `SettingsSchemaTests` (probe value, parser prefix, structural key) and add
      `SettingsStoreToolEnvironmentTests` for defaults, a valid list, and each refused shape.

## 2. The builder and where the list is read

- [ ] 2.1 `EnvironmentScrubber.Build`: under `Inherited`, capture the listed names from the
      inherited set, strip the credential names, then re-admit the captured values, then apply
      declared values; `Scrubbed` keeps ignoring the list and a declared value cannot put a stripped
      name back. Cover with `EnvironmentScrubberTests`.
- [ ] 2.2 Add `ToolEnvironmentPassthrough` (process-wide `Names`, `Configure` returning the
      credential-name collisions under the platform comparer, `CollisionWarning`, `ResetForTests`),
      mirroring `CredentialVariables`.
- [ ] 2.3 `ChildProcess.BuildEnvironment` passes `passthrough: ToolEnvironmentPassthrough.Names`
      next to the strip list, and `ChildEnvironmentGuardTests` pins that this method is the one read
      site and no spawn site reads or configures the list.

## 3. Startup wiring

- [ ] 3.1 Configure the list from resolved settings at startup in `InteractiveSession`, `PrintMode`
      and `AcpMode` (after the provider store registers custom `env:` names, and again when a
      wizard commit or login re-resolves settings), emitting one warning for a collision.
- [ ] 3.2 Test the configuration/collision helper and the warning text; test through `PrintMode`
      that a settings file naming a credential variable surfaces one warning on stderr.

## 4. Spawn proof and finish

- [ ] 4.1 Add a POSIX and a Windows spawn test (one xUnit collection, explicit parent environment,
      no test-process mutation) that a passed-through name reaches the real child, a credential
      name not listed stays stripped, and a look-alike name survives.
- [ ] 4.2 Run the focused test classes, `dotnet csharpier format .` then `check`, `openspec validate
      tool-environment-passthrough --strict` and `bash scripts/check-openspec-skills.sh`.
- [ ] 4.3 Sync the delta specs into `openspec/specs/` and archive the change as the final commit.
