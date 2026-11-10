# ADR Review Manifest

- Status: completed
- Review date: 2026-11-10

## Review Summary

No new durable architectural decision. This change adds a startup notice beside the existing
collision warning and a consequence sentence to the reference docs. It follows ADR-0035
(tool-environment pass-through): the admitted set, the exact-name rule, the user-scope limit and the
policy-build read site are unchanged.

## In-Force ADRs Reviewed

- ADR-0035 (tool-environment pass-through): unchanged — the notice renders the set this ADR
  defines; it does not alter the strip, the pass-through ordering or the read site.
- ADR-0024 (the bash guard): unaffected — the notice is presentation at session start, not a child
  environment policy.
- ADR-0003 (the agent loop runs tools): context only — nothing in the loop changes.

## New Durable ADRs Created

None.
