## Why

A profile may point at a vision model, yet a session on a text-only chat model still refuses an
image-bearing turn: the pre-send capability check sees no image input and stops the submission. The
image is lost to the run even though an existing auxiliary profile can see it.

## What Changes

- Add a vision-fallback policy at the harness pre-send image check: when the chat model is known not
  to accept images and the request carries at least one image, the configured **fast** profile
  describes each image and the request carries that description as text next to the image's
  `[image <w>x<h>]` placeholder.
- Treat the `fast` usage as the user's configured vision profile only when its resolved model
  declares image input; when the usage is unset or its model does not declare image input, today's
  `model <id> doesn't accept images; switch profile or remove the image` refusal is unchanged.
- Emit the description and the profile that produced it on the run's transcript event stream, so the
  person can see what replaced the image.
- On a vision-profile failure, fail the turn with an error that names both the chat model's refusal
  and the vision profile's error; never silently drop the image.
- Keep the settings schema, session schema and profile selection shape #253 defines unchanged: no new
  usage key, no new session entry, no new attribution value.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `images`: the vision gate gains the fallback path, its profile choice, the text the chat model
  receives, the transcript line, and the both-errors failure.
- `agent-loop`: the harness pre-send image check performs the fallback before hydration and records
  the vision call, while the no-profile path keeps the existing refusal.
- `provider-store`: the existing `fast` usage is the vision profile when its model declares image
  input; no new usage, setting or schema is introduced.

## Impact

- `src/Lunate.Agent`: the pre-send image check, request construction, vision call accounting and
  transcript event.
- `src/Lunate.Coding`: resolve the fast profile for interactive, print and ACP frontends, and let an
  image submission through the early resolver gate only when a vision-capable fast profile exists.
- Tests under `tests/Lunate.Agent.Tests` and `tests/Lunate.Coding.Tests`; docs for profiles/usages,
  images and providers; one changelog fragment.
- No new NuGet package, public settings key, session schema version or command.
