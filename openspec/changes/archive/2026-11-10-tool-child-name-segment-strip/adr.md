# ADR Review Manifest

- Status: completed
- Review date: 2026-11-10

## Review Summary

ADR review completed for this change. It changes a security-relevant default, so it records one
durable decision: ADR-0036 (addendum), which amends the tool-process environment decision
(ADR-0034, being renumbered to ADR-0035 in a separate fix) by making the inherited strip a
name-segment rule with the exact-name pass-through list as the opt-in.

## In-Force ADRs Reviewed

- ADR-0034 (tool environment pass-through): amended — the strip is no longer the known-name list
  alone; exact-name matching, the user-scope-only rule, the read site and `Scrubbed` unaffected stay
  in force.
- ADR-0024 (the bash guard): unchanged — the child-environment default is a different boundary from
  the guard's file read-deny, and both remain in force.
- ADR-0003 (the loop runs tools): context only — no loop change.

## New Durable ADRs Created

- `adr/0036-addendum-default-stripping-by-segment.md` — the segment rule, the pass-through opt-in,
  and what is now reachable from a model-chosen command's child process.
