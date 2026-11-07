## 1. Prove the leak (test first)

- [x] 1.1 Add a `UnixChildProcessTests` end-to-end test: a tool child sees `PATH` and an unrelated user variable, and does not see a provider key variable or a preset key alias. Run it against the current tree — it fails, because nothing strips the inherited environment yet.
- [x] 1.2 Add builder unit tests for both policies (`Scrubbed` minimal set, `Inherited` whole set), the credential strip, the alias, a declared custom `env:` name, and the non-pattern rule (`OPENAI_API_KEY_BACKUP` survives).

## 2. One builder, two policies

- [x] 2.1 Add `ChildEnvironmentPolicy { Scrubbed, Inherited }` and make it the first, non-defaulted parameter of `EnvironmentScrubber.Build`; implement `Inherited` (whole environment, declared values on top, stripped names removed) beside `Scrubbed`.
- [x] 2.2 Add `CredentialVariables` in `Lunate.Coding`: every preset's `KeyVariable`/`KeyVariableAlias` plus the registered custom `env:` names; register from `ProviderStore.Load`.
- [x] 2.3 Delete `ChildProcess.BuildEnvironment`'s own construction and delegate to `EnvironmentScrubber.Build(ChildEnvironmentPolicy.Inherited, …)` with the strip names.
- [x] 2.4 Name the policy at every remaining spawn site (`McpServerHost`, `RemoteExtension`, `RemoteConformanceRunner`): `Scrubbed`.
- [x] 2.5 Update `PublicAPI.Unshipped.txt` for the new public surface.

## 3. Pin the rule

- [x] 3.1 `ChildEnvironmentGuardTests`: pin `EnvironmentScrubber.cs` as the only file that reads the process environment into a child set, and add a test that no spawn site builds an environment itself — every file that assigns a child environment names a `ChildEnvironmentPolicy`.

## 4. Verify

- [x] 4.1 `dotnet test tests/Lunate.Coding.Tests --filter "FullyQualifiedName~UnixChildProcessTests"` and `dotnet test tests/Lunate.Protocols.Tests --filter "FullyQualifiedName~ChildEnvironmentGuardTests|FullyQualifiedName~EnvironmentScrubber"` green.
- [x] 4.2 `dotnet build Lunate.slnx -c Release` 0 warnings, `dotnet csharpier check .` clean.
- [x] 4.3 `openspec validate one-child-environment-builder --type change --strict` before archive.
