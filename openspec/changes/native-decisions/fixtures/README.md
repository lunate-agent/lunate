# Fixtures: System One protocol, recorded live

Recorded 2026-11-02 from this machine, verbatim response bodies, for the design in
`../design.md`. No secrets are stored here.

Provenance:

- `openrouter-*`: `POST https://openrouter.ai/api/v1/systemone`, `typesafe/jev-1.14`, the configured
  OpenRouter key (user-scope credential; calls billed to the account).
- `opencode-*`: `POST https://opencode.ai/inference/systemone/v1/systemone`, `jev-1.13`, the
  OpenCode Go key, which the endpoint accepted and billed to balance (`usage.opencode.billing_source`).
- **Not recorded**: TypeSafe direct (`https://api.typesafe.ai/v1/systemone`) — no key on this machine.
  First implementation task; if its shape differs, it becomes a preset variant.

Files and what each pins:

- `openrouter-noul-choice-score.json` — the three classic kinds in one request; `score.criteria` as an
  array, `choice.criteria` as a record; response `answers` and `usage`.
- `openrouter-box.json` — box with an image part; four fractions + confidence.
- `openrouter-score-criteria-array-required.json` — 400 when score criteria are sent as a record.
- `openrouter-choice-criteria-object-required.json` — 400 when choice criteria are sent as an array.
- `openrouter-unknown-question-type.json` — 400 listing the four valid discriminators.
- `openrouter-unknown-model.json` — 400 "Model … does not exist".
- `opencode-jev113-noul.json` — same request shape; response adds `usage.opencode`.
- `opencode-images-need-header.json` — 400 requiring `TypeSafe-Danger-Zone-Images: true`.
- `opencode-jev114-unavailable.json` — 400 "Model is unavailable" for `jev-1.14` on OpenCode.

These become test fixtures in the implementation; they are kept here so the design's claims are
auditable and a reviewer can diff the later client against the recorded wire shapes.
