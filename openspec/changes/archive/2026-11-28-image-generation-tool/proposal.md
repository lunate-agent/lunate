## Why

The `image-generation` capability entry (#357's first item) has no consumer: the catalog can name
the API, ADR-0027 names the `image` usage, and nothing in a run ever asks a model for an image. The
maintainer decision on #980 settled where a generated image lives (the session sidecar,
content-addressed as ADR-0018 defines) and how a turn names it (the typed image part), so the
missing piece is the tool that produces one — and that proves the capability end to end.

## What Changes

- Add a `generate_image` tool, registered beside the core tools whenever `usages.image` resolves to
  a profile that can serve it. It takes a prompt and an optional workspace `path`.
- Add `usages.image` as a configured usage whose declared requirement is the model's declared image
  output (ADR-0027's usage-role table). A profile whose known capability facts say it does not
  produce images is refused at resolution; an unknown capability set is accepted. Only the
  `openai-chat` family has an image wire in this build.
- A successful call stores the returned bytes content-addressed in the session's own sidecar
  (`<sessions-dir>/<session-id>/images/<sha256>.<ext>`, ADR-0018) through the loop's existing tool
  image path, so the transcript records the typed `image` part and the model-facing text is the
  exact `[image <w>x<h>]` placeholder. No session-schema change.
- With a `path`, the same bytes are also written into the workspace under the write tool's path
  rules and the write risk's approval. No new settings key beyond `usages.image`.
- Charge the generation call to the run as usage `image` with its profile, provider and resolved
  model id; schema 4 already records usage as a free string, so no session-schema bump.
- Amend ADR-0027: the `image` role moves from "named, not built" to built, with its requirement
  enforced at resolution and its attribution recorded.
- Document the tool, the usage and the workspace path in the docs.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `agent-tools`: the `generate_image` tool — its arguments, its registration rule, its sidecar and
  workspace artefacts, its approval and its errors.
- `provider-store`: `usages.image` joins the configured usages, requires the model's declared image
  output, is refused on a family without an image wire, and is a recorded attribution value.
- `images`: the placeholder contract also covers a successful `generate_image` tool result.

## Impact

- `src/Lunate.Ai`: one image-generator seam on the provider client factory, reusing the existing
  endpoint, headers and credential placement of the `openai-chat` family.
- `src/Lunate.Coding`: the image usage resolver and client seam, the tool, the settings key and its
  schema entry, and the three frontends' composition.
- Docs (`docs/settings.md`, `docs/models-and-providers.md`, `docs/how-lunate-works.md`),
  ADR-0027's amendment, and the changelog fragment.
- No new NuGet package, no session-schema version bump, no new settings key beyond `usages.image`.
- Tests use a stub image-generation client; no network is reached.
