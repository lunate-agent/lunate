## Why

A custom provider entry declares an API *family*; its kind is only the adapter that speaks that
family's wire (`openai` for both OpenAI families, `anthropic`, `google`). `ModelProfileResolver`
nevertheless requires the catalogue entry a profile names to belong to the entry's *kind*, so a
custom OpenAI-compatible endpoint whose discovery offers `kimi-k2` writes
`profiles.chat-main = { provider: "local", model: "kimi-k2" }` and the next start refuses it with
`'local' has kind 'openai', but model 'opencode/kimi-k2' belongs to 'opencode'` (#708). The same
holds for any model the catalogue happens to know under another provider, which is the normal case
for anyone running local models under common names.

Maintainer decision (option 1, recorded on #708): a custom entry's profile resolves its model
**through the entry itself** — `<provider id>/<model>` — and the check compares the entry's
**family**, not its adapter kind. A matching catalogue entry under a different provider may supply
**metadata only** and is never a reason to refuse the profile.

## What Changes

- A profile whose provider entry is a custom entry (its name is not a provider preset id) resolves
  its model through that entry: the resolved model id is `<provider id>/<model>`, the wire id is
  what the endpoint is sent, and the entry supplies endpoint, headers and credential placement as
  before. A catalogue entry under another provider is no longer a refusal.
- The provider check for a custom entry compares **API families**: the catalogue entry's family
  (its declared `apiFamily`, otherwise its provider preset's default family) must be one the entry
  serves. A model of another family is still refused, naming the entry, the family it serves and
  the family the model speaks.
- A matching catalogue entry under a different provider supplies **model metadata only** — context
  window, tool/vision support, reasoning style, maximum output, capabilities, declared family and
  wire id. It never supplies pricing: a custom endpoint that resells a known model must not inherit
  another provider's rates, so such a profile stays unpriced.
- A bare id that several catalogue entries hold and none of them in the entry's family is still
  refused; a bare id several entries hold *within* the entry's family resolves through the entry
  with unknown limits instead of failing as ambiguous.
- Preset entries keep today's behaviour exactly: the catalogue entry must belong to the preset
  kind, and an ambiguous bare id is still refused with every candidate named.
- No public API change, no file-format change, no session-format change, no new package.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `provider-store`: a profile on a custom provider entry resolves its model through the entry
  (`<provider id>/<model>`) instead of being refused because the catalogue holds that model under
  another provider; the family check and the metadata-only rule apply.
- `ai-layer`: profile resolution compares a custom entry's family against the catalogue entry's
  family instead of the adapter kind, and carries the catalogue entry's model facts — never its
  pricing — onto the model the entry serves.

## Impact

- Affected code: `src/Lunate.Ai/ModelProfile.cs` (`ModelProfileResolver`), plus one new internal
  helper file in `src/Lunate.Ai` for the custom-entry resolution so `ModelProfile.cs` stays under
  the repo's file-size guideline.
- Tests: `Lunate.Ai.Tests/ModelProfileResolutionTests` (the reported case, the family refusal, the
  metadata rule, the preset case unchanged), and every test class referencing `ModelProfileResolver`
  or `ResolvedModelProfile` (grep-checked), including `SettingsStoreProfileMergeTests` and
  `WizardModelCheckWireTests` if they assert on profile resolution.
- Documentation: `docs/first-run.md`'s profile paragraph states the wider rule.
- No new NuGet packages. Culture-invariant formatting only. No golden session files.
