# ADR Review Manifest

- Status: completed
- Review date: 2026-11-22

## Review Summary

ADR review completed for this change. The maintainer decision of 2026-11-22 is binding and settles the
one durable rule this change introduces: a usage is a role with declared capability requirements, and
`vision` is such a role with image input as its requirement. The card directs that rule into ADR-0027 as
an amendment rather than a new record; the ADR is extended with a dated amendment section and its status
line records the amendment, while the original 2026-10-30 text stays intact as history. No new
repository-level ADR file is created.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the fallback stays inside the MEAI client and the
  existing layering; the event record remains a MEAI-free contract type.
- `adr/0003-loop-own-vs-maf-harness.md` — the harness still owns the pre-send request check and emits
  the new event on the run stream.
- `adr/0015-session-format.md` — schema 4 already carries a free-string usage, so `vision` needs no
  schema version or entry change.
- `adr/0027-provider-store-model-profiles-and-usages.md` — amended by this change with the usage-role
  rule the maintainer decision records.
- `adr/0040-hook-based-extensibility-on-observables.md` — the new event reaches the observable
  projection and the `--json`/wire kind vocabulary; no second event model is introduced.
- `adr/0043-contract-unions.md` — `ImageDescribed` is a new case in the `AgentEvent` union, so the
  contract API version moves and the extension-author note lands with it.
- `adr/0045-decision-model-package.md` — no decision-model or package boundary changes.

## New Durable ADRs Created

- None - the durable rule is recorded as a maintainer-directed amendment to
  `adr/0027-provider-store-model-profiles-and-usages.md`, per the card's decision. No separate
  repository-level ADR file is created.
