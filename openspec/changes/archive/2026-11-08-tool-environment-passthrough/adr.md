# ADR Review Manifest

- Status: completed
- Review date: 2026-11-08

## Review Summary

ADR review completed for the tool-environment pass-through change. One durable decision was
recorded: the tool child's `Inherited` policy gains an exact-name pass-through applied after the
credential strip, read where the policy is built, with a startup warning when a listed name is also
a Lunate credential name. Codex CLI's shell environment policy is the prior art cited by the issue
and in the new ADR.

## In-Force ADRs Reviewed

- ADR-0024 (the bash guard): unaffected — the guard wraps the child, the environment policy decides
  what the child sees; both boundaries compose.
- ADR-0003 (the agent loop runs tools): context only — this change touches the tool process
  environment, not the loop.
- ADR-0022 (model catalog and provider auth): the `CredentialVariables` strip follows its provider
  key variables and aliases; this change does not alter key resolution or storage.

## New Durable ADRs Created

- `adr/0034-tool-environment-pass-through.md` — the inherited policy's exact-name pass-through, the
  strip as the default deny, the explicit-credential startup warning, and the read-at-policy-build
  boundary.
