## Why

The merged vision fallback (#1014) binds the `fast` usage and announces its description as
`SteeringInjected`. The maintainer's binding decision of 2026-11-22 makes vision its own usage,
`usages.vision`, with image input as its declared capability requirement, and asks the transcript to
carry a dedicated event. Continuing to overload `fast` hides a user's real fast model behind the
vision role and records vision spend under the wrong usage.

## What Changes

- Add `usages.vision` as an allowed settings key beside `usages.chat`, `fast`, `subagents` and
  `compaction`. Vision requires image input; a model that does not declare it is not a vision
  profile.
- Bind the fallback to `usages.vision`: an unset usage, or a vision profile whose model does not
  declare image input, leaves today's image refusal unchanged. `fast` is no longer the fallback.
- Attribute the vision call as usage `vision`, not `fast`. No session schema bump: schema-4
  attribution already carries a free-string usage.
- **BREAKING (extension contract)**: add the dedicated `ImageDescribed` `AgentEvent` case carrying
  the description, the image placeholder, and the profile and model that produced it, replacing the
  `SteeringInjected` reuse. The new event case moves `ExtensionApi` to 1.19.0 with an
  extension-author note and the `--json`/wire kind `image_described`.
- Amend ADR-0027 with the rule the decision records: a usage is a role with declared capability
  requirements — chat: text+tools; decision: structured output; vision: image input; later
  embeddings: embeddings API; image: image output — and a new usage is one settings key, its
  requirement and an attribution value, with no session schema bump while attribution stays a free
  string.
- Describe `usages.vision`, not `fast`, in `docs/models-and-providers.md` and `docs/providers.md`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `images`: the vision fallback uses the configured `usages.vision` profile, and its transcript line
  is the dedicated image-description event.
- `agent-loop`: the harness resolves and attributes the fallback under `vision`, and the
  vision-profile failure still names both errors.
- `provider-store`: `usages.vision` joins the configured usages and is the vision profile when its
  resolved model declares image input; `fast` returns to being only the fast usage.
- `agent-events`: the event set gains `ImageDescribed`, and the contract version note records the
  added case.

## Impact

- `src/Lunate.Coding`: settings validation gains the usage key, the profile resolver reads
  `usages.vision`, and the interactive, print and ACP frontends wire the vision client under it.
- `src/Lunate.Agent`: the pre-send fallback reads the `vision` usage client, attributes usage
  `vision`, and emits `ImageDescribed` instead of `SteeringInjected`.
- `src/Lunate.Contracts` plus the remote wire, ACP mapper, print JSON, diagnostics and the published
  schema gain the `ImageDescribed` case and `image_described` kind.
- `docs/models-and-providers.md`, `docs/providers.md`, `docs/json-events.md`, the extension version
  notes, `adr/0027-provider-store-model-profiles-and-usages.md` and the changelog fragment.
- No new NuGet package, no session schema version bump, no settings key beyond `usages.vision`.
