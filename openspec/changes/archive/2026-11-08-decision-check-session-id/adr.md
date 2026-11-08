# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for this change. The change gives the decision-profile check the same
`RequestContext` identity the model check already uses; it follows the in-force provider-header
and provider-family decisions rather than introducing a new pattern. No in-force ADR is
superseded or needs revisiting.

## In-Force ADRs Reviewed

The full `adr/` folder was read for the supersession graph. These files are currently in force
(no later ADR supersedes any of them wholly; ADR-0029 supersedes part of ADR-0027, and
ADR-0031/0032/0033 supersede parts of ADR-0017's skills addendum):

- `0001-no-aot-single-file-budgets.md`
- `0002-layering-and-meai-model-types.md`
- `0003-loop-own-vs-maf-harness.md`
- `0004-mintty-input-support.md`
- `0005-budget-calibration.md`
- `0005-addendum-startup-floor.md`
- `0006-roslyn-placement.md`
- `0007-live-area-concurrency.md`
- `0008-release-publish-flags.md`
- `0009-memory-budgets.md`
- `0010-anthropic-adapter.md`
- `0010-addendum-prompt-caching.md`
- `0011-repo-quality-tooling.md`
- `0012-tool-contract.md`
- `0012-addendum-context-hygiene.md`
- `0013-agent-loop.md`
- `0014-csharp-backend-interface.md`
- `0015-session-format.md`
- `0016-workspace-boundary.md`
- `0017-extensibility-architecture.md`
- `0017-addendum-commands.md`
- `0017-addendum-extensions.md`
- `0017-addendum-model-registry.md`
- `0017-addendum-packages.md`
- `0017-addendum-remote-host.md`
- `0017-addendum-skills.md`
- `0018-session-schema-2.md`
- `0018-addendum-branching-sessions.md`
- `0018-addendum-images.md`
- `0019-xenoatom-terminal-ui.md`
- `0020-acp-sdk-choice.md`
- `0021-per-run-workspaces.md`
- `0022-model-catalog-and-provider-auth.md`
- `0023-first-party-extensions.md`
- `0024-bash-guard.md`
- `0025-verified-edits.md`
- `0026-roslyn-built-in-boundary.md`
- `0027-provider-store-model-profiles-and-usages.md`
- `0028-decision-layer-primitive.md`
- `0029-provider-api-families.md`
- `0030-native-decisions.md`
- `0031-addendum-skills-index-budget.md`
- `0032-addendum-skill-description-spec-limit.md`
- `0033-addendum-unknown-window-disables-skills-budgets.md`

The directly constraining decisions are ADR-0029 (headers render from `RequestContext` at
request time through the shared handler; the provider is a credential plus one base URL per
family), ADR-0030 (native decisions are a provider family), ADR-0027 (provider store and
profiles), and ADR-0028 (the decision layer primitive).

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced. The fix reuses the existing
  request-identity and provider-header contracts.
