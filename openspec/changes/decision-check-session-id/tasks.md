## 1. Regression tests (red first)

- [ ] 1.1 `SetupWizardDecisionCheckTests`: the `opencode-go` native `jev-1.13` decision check passes and its System One request carries a non-empty `x-opencode-session`; it fails before the fix with the reported header error.
- [ ] 1.2 `SetupWizardDecisionCheckTests`: a `/setup` decision check inside a session sends exactly that session's id on the wire.
- [ ] 1.3 `SetupWizardDecisionCheckTests`: the existing `openrouter` native check (a provider with no session header) passes and carries no `x-opencode-session`.

## 2. Fix through the production path

- [ ] 2.1 `WizardModelCheck.RunDecisionAsync` accepts a `RequestContext?`, resolves `context ?? RequestContext.Ephemeral()`, and passes it to `DecisionUsageModels.CreateUsageClient` (native and adapter paths).
- [ ] 2.2 `InteractiveSession.SetupWizard` passes `SessionRequestContext()`; `SetupCommand.Walk` passes `context: null`; no header is patched at either call site.

## 3. Docs and gates

- [ ] 3.1 Review `docs/first-run.md` for the setup-check wording; update only if the identity behaviour is user-visible.
- [ ] 3.2 Run the edited/referencing test classes by filter and `dotnet csharpier format .`, then `dotnet csharpier check .` and a Release build.
- [ ] 3.3 Run `openspec validate decision-check-session-id --strict`, `openspec validate --all --strict`, and `bash scripts/check-openspec-skills.sh`.
