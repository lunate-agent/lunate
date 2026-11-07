## Context

The provider presets already model one base URL per API family (ADR-0029), and the decision
layer already offers a native Jev profile when a preset exposes the `systemone` family
(ADR-0030). The archived `2026-11-06-provider-family-set` change built that machinery on the
`opencode` preset. The `opencode-go` preset kept only its four legacy families and pinned no
Jev model, although OpenCode Go reviews (rc.3 finding 3) showed that the same Go key is
accepted at the System One surface and bills pay-as-you-go. The `jev-1.13` catalog entry for
systemone generation is already in the upstream models.dev snapshot under the `opencode`
provider; `opencode-go` needs the same entry pinned against its own namespace.

## Goals / Non-Goals

**Goals:**

- Serve System One from `opencode-go`: family URL, family header, key-variable alias.
- Pin `opencode-go/jev-1.13` in the embedded catalog (`wireId: jev-1.13`,
  `apiFamily: systemone`).
- Label the Go native Jev decision profile with the pay-as-you-go billing notice in the
  wizard's fast-and-decision step.
- Document the fifth family and the billing model in `docs/configuration.md` and
  `docs/guide.md`.

**Non-Goals:**

- No region or routing policy for Go keys; pay-as-you-go billing is stated, not enforced.
- No protocol work: the systemone surface already has its client (ADR-0030).
- No catalog regeneration or schema change; `jev-1.13` is a snapshot entry like any other.

## Decisions

- **Family on the shared System One URL.** `opencode-go` declares
  `"systemone": "https://opencode.ai/inference/systemone/v1/systemone"` — System One is a
  separate surface, not a `/go/...` path. `x-opencode-session` stays on every family as a
  free header; `TypeSafe-Danger-Zone-Images: true` rides the `systemone` family only, as
  `familyHeaders`. Alternative considered: a second `opencode-systemone` preset — rejected
  because the Go key is one credential and the wizard lists one OpenCode Go provider.
- **Key variable alias, not a rename.** The preset keeps `OPENCODE_API_KEY` and adds
  `OPENCODE_GO_API_KEY` as the accepted alias, matching OpenCode's docs and old setups.
- **Label pinning by id.** The pay-as-you-go label is keyed to `opencode-go`, set where the
  wizard renders the decision-profile line; other presets keep the existing wording.
- **Live smoke behind two switches.** A real decision request against System One bills real
  money, so the smoke test runs only with `LUNATE_LIVE=1` and `OPENCODE_GO_API_KEY` (or the
  legacy `OPENCODE_API_KEY`); normal runs skip it and stay offline.

## Risks / Trade-offs

- [The pinned `jev-1.13` may drift from what OpenCode actually serves] -> the live smoke
  pins the request shape and family routing; catalog snapshots are regenerated from
  models.dev on demand.
- [A Go user can be billed pay-as-you-go without noticing] -> the wizard label and docs
  state it at the decision point; enforcement is out of scope by design.

## Migration Plan

Embedded-data-only change; nothing migrates. Existing `opencode-go` installs keep working
(the new family is additive), and profiles that already exist keep their saved models.

## Open Questions

None. In-force ADRs 0022, 0029 and 0030 already cover catalog, families and native decisions;
no new ADR is warranted for a data-plus-label change.
