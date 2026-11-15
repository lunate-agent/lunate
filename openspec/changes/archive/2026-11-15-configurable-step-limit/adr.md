# ADR Review Manifest

- Status: completed
- Review date: 2026-11-15

## Review Summary

ADR review completed for this change. The change resolves the loop's existing `MaxSteps` ceiling
from a settings key and a launch flag, using the precedence the sibling cost and wall-clock limits
already use. It introduces no architectural pattern, boundary, protocol or dependency: the loop
keeps owning the ceiling check, `Lunate.Coding` keeps owning settings and launch parsing, and no
public API or session format changes.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the setting and flag stay in `Lunate.Coding`; the
  loop keeps consuming a plain `int` on `AgentHarnessOptions`.
- `adr/0003-loop-own-vs-maf-harness.md` — the step limit remains a loop check in `AgentHarness`
  between model calls; this change only changes the resolved value.
- `adr/0015-session-format.md` — unchanged: the step ceiling is not recorded in the session, and
  `StepLimitReached` already carries the reached value.
- `adr/0027-provider-store-model-profiles-and-usages.md` — unrelated; no provider or profile
  resolution is touched.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced. Configurable step ceilings follow
  the established run-limit precedence and do not require a new boundary or contract.
