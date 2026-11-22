# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The fallback is a policy over the model-selection and
capability rules already accepted in ADR-0027 and the harness ownership in ADR-0003; it introduces
no new boundary, dependency, wire contract or session format.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the fallback stays inside the existing MEAI client
  and project layering; no parallel message type is added.
- `adr/0003-loop-own-vs-maf-harness.md` — the loop owns the pre-send request check, so the fallback
  lives in `AgentHarness`, not in provider middleware.
- `adr/0012-tool-contract.md` — no tool contract changes; the `read` image refusal is unchanged.
- `adr/0015-session-format.md` — the session keeps image references and schema 4; no new entry or
  usage value is written.
- `adr/0027-provider-store-model-profiles-and-usages.md` — the `fast` usage remains the configured
  auxiliary profile and the image fallback uses the capability facts it already defines.
- `adr/0043-contract-unions.md` — the transcript reuses `SteeringInjected`; no event kind is added,
  so the contract union and `ExtensionApi` do not move.
- `adr/0045-decision-model-package.md` — no decision-model or package boundary changes.

## New Durable ADRs Created

- None - no major durable architectural decisions were introduced. The profile choice and event
  reuse are recorded in this change's `design.md`; changing them later does not require a new
  architecture commitment.
