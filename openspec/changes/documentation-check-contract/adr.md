# ADR Review Manifest

- Status: completed
- Review date: 2026-11-13

## Review Summary

ADR review completed for this change. This is a specification correction: it records the
documentation checker that merged as #801 in the two capabilities that already own the documentation
conventions and the verification matrix. No boundary, dependency, data format, protocol or public
contract moves, so nothing in it constrains a future architecture choice.

## In-Force ADRs Reviewed

- `adr/0011-repo-quality-tooling.md` — the documentation map, markdownlint and the gate's quality
  steps. The change adds the link/anchor/redirect/navigation check to the same gate; the ADR's
  decisions are unchanged and need no successor.
- `adr/0003-agent-loop-owns-tool-execution.md` — out of scope: no runtime, tool or event path is
  touched.

## New Durable ADRs Created

- None — no major durable architectural decisions were introduced. The checker itself and its
  placement in the gate were decided and shipped in #801; recording that placement in the specs is
  not a new decision.
