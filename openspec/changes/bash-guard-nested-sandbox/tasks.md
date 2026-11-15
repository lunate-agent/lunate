## 1. Guard resolution

- [x] 1.1 Add a failing test in `tests/Lunate.Coding.Tests/BashGuardTests.cs`: a fake macOS probe whose `sandbox-exec` run returns `(1, "sandbox-exec: sandbox_apply: Operation not permitted")` leaves the shell unwrapped, keeps probe state `failure`, and yields a notice that says the process already runs inside a sandbox, that the guard's own rules are not applied, carries the probe output, and does not contain `unconfined`.
- [x] 1.2 Add the sibling regression test: another macOS probe failure (no `sandbox_apply`) still reports the existing unavailable notice with "runs unconfined".
- [x] 1.3 In `src/Lunate.Coding/BashGuard.Platforms.cs`, classify the `sandbox_apply` probe failure in `ResolveMacOs` and return the nested-sandbox notice (mechanism `none`, probe state `failure`).

## 2. Once per session

- [x] 2.1 Add a tool-level test in `tests/Lunate.Coding.Tests/BashGuardToolTests.cs` (non-Windows, fake probe): two `bash` calls under the nested probe produce the nested notice in the log exactly once, never put it in the command output, and never say `unconfined`.

## 3. Documentation

- [x] 3.1 Add the nested-sandbox case to the bash-guard paragraph in `docs/security.md`.

## 4. Gate and archive

- [x] 4.1 Run the fast gate: `dotnet csharpier check .`, `dotnet build` of the touched projects (Release, tests included), and the named test classes (`BashGuardTests`, `BashGuardToolTests`, `BashGuardStatusSessionTests`).
- [x] 4.2 Write the changelog fragment `changes/<pull-request-number>.md` under `### Fixed`, and run `openspec validate bash-guard-nested-sandbox --type change --strict`.
- [x] 4.3 Sync the delta specs and archive the change with `openspec archive bash-guard-nested-sandbox --yes` as the last commit; then run `openspec validate --all --strict`.
