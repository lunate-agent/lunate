# ADR Review Manifest

- Status: pending sign-off (the two queue questions in design.md settle the durable part)
- Review date: 2026-11-02
- Issue: #446 (rework of the #439 "same wiring reviewed" finding)

## Review Summary

No new durable ADR is created by this change. The decision it records — question suggestions attach
only where a person answers, and unattended runs reach a judgement through the `decide` tool — is a
narrowing of who is wired to the primitive that ADR-0028 already defines ("question suggestions
(display only)" as one consumer of the general decision layer). That is spec text, not a new
architectural pattern, so it is recorded as the ADDED requirement in
`specs/decision-layer/spec.md`.

If the maintainer answers the sign-off queue's question 1 (print-mode annotation) or question 2 (an
ACP `ask_user` surface) with "yes, design it", the durable half becomes an **addendum to ADR-0028**
naming each consumer's surface (interactive only / annotated degrade / editor-side question UI), and
a follow-up change carries it — not this one.

## In-Force ADRs Reviewed

- `adr/0028-decision-layer-primitive.md` — this change's scoping lives under its consumer list; it
  is re-read, not amended. Its "display only" wording stays as accepted; the delta spec narrows
  where the consumer attaches without redefining what suggestions are.
- `adr/0003-loop-own-vs-maf-harness.md` — the loop owns everything between tool calls, including
  `ask_user`'s degrade decision; the pinning tests exercise that ownership, not a new seam.
- `adr/0017-extensibility-architecture.md` — unattended front ends (print mode, ACP) observe
  through `AgentEvent`; no new channel is introduced for suggestions.

## New Durable ADRs Created

None.
