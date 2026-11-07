## Why

rc.3 review finding 3: OpenCode Go exposes OpenCode **System One** (Jev) natively, but the
`opencode-go` preset did not declare the `systemone` family and the catalog pinned no Go Jev
model, so `opencode-go/jev-1.13` could not be selected and the wizard's native decision
profile surfaced no Go entry. A Go key at the System One surface bills to the OpenCode
pay-as-you-go balance, not the Go plan — the wizard must say so at the point where a user
would pick it.

## What Changes

- `providers.json`: the `opencode-go` preset declares an additional `systemone` base URL
  (`https://opencode.ai/inference/systemone/v1/systemone`), keeps the `x-opencode-session`
  header and adds the System One image header (`TypeSafe-Danger-Zone-Images`) as a family
  header; `OPENCODE_GO_API_KEY` is accepted as the key-variable alias of `OPENCODE_API_KEY`.
- `models.json`: the built-in catalog pins the `opencode-go/jev-1.13` entry
  (`wireId: jev-1.13`, `apiFamily: systemone`, no tools).
- Setup wizard, provider choice step: the decision-profile line for `opencode-go` is
  labelled "native Jev — billed to your OpenCode pay-as-you-go balance, not the Go plan; no
  box questions" (the label is `opencode-go`-specific; other presets keep the existing
  wording).
- Docs: `docs/configuration.md` and `docs/guide.md` name the fifth family, the Go key alias
  and the pay-as-you-go billing.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `provider-store`: the `opencode-go` preset template offers the `systemone` family on the
  OpenCode System One surface, send the System One image header on that family, and accept
  `OPENCODE_GO_API_KEY` as an alias of the preset's key variable.
- `setup-wizard`: the wizard's decision-profile offer for `opencode-go` states the
  pay-as-you-go billing explicitly.

## Impact

- `src/Lunate.Ai/providers.json`, `src/Lunate.Ai/models.json` (embedded data).
- `src/Lunate.Coding/InteractiveSession.SetupWizard.cs` (one label string).
- Tests: preset/catalog family tests (`Lunate.Ai.Tests`), wizard step pins
  (`Lunate.Coding.Tests`), and a key-gated live smoke test
  (`OpenCodeGoSystemOneLiveSmokeTests`, skipped unless `LUNATE_LIVE=1` plus
  `OPENCODE_GO_API_KEY` or legacy `OPENCODE_API_KEY`).
- Docs: `docs/configuration.md`, `docs/guide.md`. No public API changes, no new packages.
