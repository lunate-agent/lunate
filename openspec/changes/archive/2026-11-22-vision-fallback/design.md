## Context

The capability shape from #253 already resolves a chat model and rejects a request whose model is
known not to accept image input. The harness keeps image references in history and hydrates them
only at request-build time, so that pre-send check is the right seam: it sees the whole request,
including resumed history, before any provider call.

The hard constraint is no settings or session-schema change beyond #253. That rules out a new
`usages.vision` key and a new schema-4 usage value. The existing `fast` usage is the configured
auxiliary profile; binding it to a vision model is the way a user designates the vision profile.

## Goals / Non-Goals

**Goals:**

- A text-only chat model can run an image-bearing turn through the configured `fast` vision profile.
- The chat model receives the description plus the exact image placeholder; it never receives image
  content it cannot accept.
- The transcript shows the description and which profile produced it.
- A missing or failing vision profile fails the turn visibly, naming both errors and never dropping
  the image.
- No new settings key, usage value, session entry or schema version.

**Non-Goals:**

- Adding vision fallback to the `read` tool (an image there is not yet a request image; the existing
  read refusal stays).
- Describing images for a model whose image support is unknown; that path keeps the existing
  attempt-and-translate behavior.
- Choosing among arbitrary vision-capable profiles or adding automatic model discovery.
- Changing compaction, image generation or embeddings.

## Decisions

### The pre-send check owns the fallback

`AgentHarness` performs the fallback while building the request, after the capability check and
before hydration. It describes each distinct `ImageContentReference` through the `fast` usage's
client, caches the description per digest for the harness lifetime, and builds the request copy with
`TextContent` instead of `DataContent` for that reference. The stored history and session entry keep
the reference, so a resumed session runs the same policy and the session format is untouched.
Alternative: strip images in the interactive/print resolver. Rejected because it cannot cover
resumed history and moves the policy out of the check #357 names as the seam.

### The `fast` usage is the configured vision profile

The profile is used only when `usages.fast` resolves and its model declares image input — either
`capabilities.inputModalities` contains `image`, or `supportsVision` is true when no modality set is
declared. Otherwise today's refusal is unchanged. This keeps #253's selection shape and schema 4's
usage list (`chat`, `fast`, `decision`, `subagents`, `compaction`) valid. Alternative:
`usages.vision` would be the clearest name, but it is exactly the settings change #357 forbids.
Alternative: scan every configured profile for a vision model. Rejected because the choice would be
implicit and ambiguous, and attributing the call would need a usage value the schema does not have.

### The request text carries description and placeholder; the transcript uses an existing event

For one image the chat model sees a `TextContent` of the form
`[image 2x3] described by profile 'fast' (<model>): <description>`. The placeholder remains exact,
so the existing placeholder contract and compaction rendering still hold. The harness emits the same
line as a `SteeringInjected` event before the chat request; the interactive session already echoes
that event into the transcript and scrollback, and print mode can surface it as a notice. This reuses
the existing event contract rather than adding a new `AgentEvent` kind and moving `ExtensionApi`.
Alternative: a new `ImageDescribed` event is cleaner in name, but is a contract change with its own
version note and is not required by this card.

### Vision calls are billed and attributable, but are not conversation messages

The vision request is a plain `IChatClient.GetResponseAsync` call with a fixed instruction and the
image bytes, no tools. Its usage is recorded through the harness accounting with the `fast` usage
and the vision profile's name; its response is not appended as an assistant message. The vision
profile's own failure becomes a run error whose message contains the chat refusal, the profile name,
the image placeholder and the vision error.

### Failure is a run error, never a silent drop

If no vision profile exists, the existing `model <id> doesn't accept images; switch profile or
remove the image` error stands. If the vision profile exists but fails or returns no text, the turn
fails before the chat request with both errors named. The request is never sent with the image
removed and without a description.

## Risks / Trade-offs

- **`fast` has another meaning.** A user may already use it for cheap text work; binding it to a
  vision model changes both roles. Mitigation: the behavior only applies when the model declares
  image input, and the transcript names the profile used.
- **The screenshot may contain sensitive data.** It now goes to the fast profile's provider as well
  as being visible to the chat provider. Mitigation: the policy is explicit in docs and the
  transcript shows the description; a user who does not want this leaves `fast` on a text model.
- **A vision call adds latency and cost before every first image turn.** Mitigation: descriptions
  are cached per image digest per harness and one request carries all the images it needs.
- **Overloading `SteeringInjected` could read as user steering.** Mitigation: the text says
  `described by profile ...`, and the event remains a transcript-only line, not a history message.

## Migration Plan

No migration. Existing settings and sessions keep their meaning; a user who wants fallback binds
`usages.fast` to a vision-capable profile. A user who does not keeps today's refusal.

## Open Questions

None. The card delegates the profile-choice detail to this design; the choice above is recorded in
the pull request's Intent decisions.

## Diagram

```text
User               Session / frontend        AgentHarness              fast profile       chat profile
 | submit @shot.png       |                       |                         |                  |
 |----------------------->| resolve image         |                         |                  |
 |                        |-- known no image? --->|                         |                  |
 |                        |   vision profile?     |                         |                  |
 |                        |                       |-- describe(image) ----->|                  |
 |                        |                       |<-- description ---------|                  |
 |                        |<-- transcript line ---|  (profile + model)      |                  |
 |                        |                       |-- request: description |                  |
 |                        |                       |   + [image WxH], no bytes --------------->|
 |                        |                       |<-- answer --------------------------------|
```
