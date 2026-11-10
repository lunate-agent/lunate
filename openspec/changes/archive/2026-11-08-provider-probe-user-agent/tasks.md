## 1. Reproduce the symptom

- [x] 1.1 Extend the production-path test `ProviderReachabilityTests.Auth_list_reports_presence_and_bounded_reachability_without_sending_secrets` (or add a focused sibling) to assert every probe request carries `User-Agent: lunate/<RequestContext.LunateVersion>`; run it and record that it fails on the unfixed probe.
- [x] 1.2 Record the failing run's output on the pull request as the reproduction.

## 2. Fix the probe

- [x] 2.1 In `src/Lunate.Coding/ProviderReachabilityProbe.cs`, write `User-Agent` from `RequestContext.LunateVersion` on the probe request (remove-then-add, one agent), leaving the timeout, status handling and error mapping unchanged.
- [x] 2.2 Keep the request free of credential, provider-declared headers and body; the existing assertions in the same test must stay green.

## 3. Verify

- [x] 3.1 `dotnet csharpier format .`, then confirm a second `format` changes nothing and `git status --short` is clean of formatting churn.
- [x] 3.2 Release build of `src/Lunate.Coding/Lunate.Coding.csproj` and filtered run of `ProviderReachabilityTests` (plus every test class referencing `ProviderReachabilityProbe`/`AuthCommand`).
- [x] 3.3 `openspec validate --all --strict` and `bash scripts/check-openspec-skills.sh`.

## 4. Close the change

- [x] 4.1 Update docs only if behaviour-visible wording changes (none expected; `docs/configuration.md` already states the agent string).
- [x] 4.2 `openspec archive provider-probe-user-agent --yes` as the final commit on the branch.
