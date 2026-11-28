# ADR-0027: User-scoped provider profiles, usages, and capability facts

- Status: accepted — 2026-10-30 (maintainer amendments for `provider-store`, #253); amended 2026-11-22 (usages as capability-bearing roles, `usages.vision`, #979); further amended 2026-11-25 (`embeddings` is built, `semantic_search`, #1028); further amended 2026-11-28 (`image` is built, `generate_image`, #1027)
- Date: 2026-10-30
- Duplicate removed: `adr/0027-provider-store-and-model-profiles.md` was an earlier draft of this same decision and claimed the same number; deleted as a duplicate in commit `28dc4440` so the ADR numbers stay unique and dense.
- Relates to: ADR-0002 (layering and MEAI model types), ADR-0003 (loop ownership), ADR-0015 (pinned session format), ADR-0022 (model catalog and provider auth)

## Context

Provider instances, model settings, and model selection were spread across presets, auth storage,
catalog entries, and one active model. The `provider-store` change introduces named user-owned
provider instances, profiles, and assignments for existing model usages. The change also adds
capability facts to catalog/profile resolution and adds usage attribution to the pinned session
format. ADR-0022's preset-instance and API-key-only provider model no longer describes the accepted
design; ADR-0015 requires any persisted session-format extension to be explicit and versioned.

## Decision

1. **Provider definitions are user-scoped instances.** Built-in provider kinds remain templates for
   adapter selection and defaults. User settings may define multiple named instances of one kind,
   endpoints, and credential references. Project settings may refer to existing user providers and
   profiles or tighten limits, but may not define providers, endpoints, or credential references.
   Region remains metadata and does not route requests.
2. **Core resolves existing usages through profiles and providers.** Selection is usage → profile →
   provider; individual request routing remains inside the declared provider. This change adds no
   new usages. `/model` changes chat only, and compaction uses chat unless the user explicitly opts
   into fast.
3. **Capabilities are extensible facts, not a closed product policy.** API names, input and output
   modalities, and features are open string sets. Facts layer from catalog to provider discovery to
   user profile override; an unknown category remains allowed. The host checks only declared
   requirements of existing usages: chat requires text input and tools, decision requires structured
   output, and an attached image is rejected before sending only when image input is known to be
   unsupported. Unknown image support is attempted; a provider rejection is translated to a
   model-named switch-profile-or-remove-image notice. Image generation, embeddings, and vision
   fallback remain out of scope for #253 and belong to #357.
4. **Decision suitability belongs to the user.** Any model can decide if the user configures it; the
   contract is checked, the choice is the user's, and decisions can only tighten approvals. No
   per-model calibration state, inferred “cannot decide” verdict, or required logprobs policy exists.
   `apiType` labels `native` versus `adapter`; it does not assess a model's intelligence. A response
   must name a defined option and provide well-formed probabilities. A malformed response is retried
   once, then reported as a failed decision naming the model. Shadow mode is opt-in. A decision
   cannot override deny or approve outside the user's allow envelope.
5. **Session schema 4 remains as approved.** Schema-4 attribution records usage, profile, provider,
   and resolved model identity, plus decision method and probability where applicable; it does not
   record capabilities or provider configuration. Schemas 1–3 retain their existing interpretation.

This supersedes ADR-0022's provider-instance and auth assumptions without rewriting that historical
record, and records the schema-4 extension required by ADR-0015.

## Consequences

- Users can name and reuse providers and profiles without making project configuration an authority
  to redirect credentials.
- Capability checks catch known transport incompatibilities while unknown facts remain permissive;
  they do not claim that a configured model is intelligent enough for a decision.
- Decision contract errors remain transient request failures rather than persistent model verdicts,
  and approval policy remains authoritative over every model output.
- Schema 4 remains the attribution boundary. Capability facts are runtime/configuration data and are
  not copied into session files.
- Image generation, embeddings, and vision fallback require the separately scoped #357 work.

## Amendment 2026-11-22 — a usage is a role with declared capability requirements

The maintainer decision of 2026-11-22 extends decision 3 with the rule this ADR's usages follow. It
does not replace decisions 1, 2, 4 or 5; where decision 2 or 3 said "no new usages" or kept vision
fallback out of scope, this amendment is the later word.

### Decision 6 — a usage is a role with declared capability requirements

A usage is not a quality label and not an alias for another usage; it names what the configured
profile is for and which capability facts the host checks. The roles and their requirements are:

| Usage | Requirement |
| --- | --- |
| `chat` | text input + tools |
| `decision` | structured output |
| `vision` | image input |
| `embeddings` (named, not built) | embeddings API |
| `image` (named, not built) | image output |

`fast` and `subagents` select a profile without adding a capability requirement beyond the chat
family; `compaction` selects the summarizer and defaults to chat.

A new usage costs one settings key (`usages.<role>`), its declared requirement at resolution, and
its usage attribution value. While the schema-4 attribution usage stays a free string, a new usage
needs no session schema bump. Embeddings and image generation are named here so a later consumer
finds the place, and are not built until one exists.

`vision` is the vision fallback profile: when the chat model's known capabilities say it accepts no
images and a request carries one, the `vision` profile describes the image and the chat request
carries the description beside the `[image <w>x<h>]` placeholder. An unset `vision` usage, or a
profile whose model does not declare image input, leaves the existing image refusal unchanged.
`fast` carries no vision role. The vision call attributes as usage `vision`, and the transcript
announces it with the `ImageDescribed` event rather than a steering event.

## Amendment 2026-11-25 — `embeddings` is built by repo search

The 2026-11-22 amendment named `embeddings` so a later consumer would find the place and kept it
unbuilt until one existed. #981 makes repo search that consumer, and #1028 builds it as the optional
`semantic_search` tool. This section is the later word; the earlier text stays as the record of the
2026-11-22 decision.

The role is unchanged in shape: one settings key (`usages.embeddings`), one declared requirement at
resolution (the model's `embeddings` API), and one attribution value (`embeddings`). What is new is
that all three are exercised:

- The requirement is checked the way decision 3 says capability facts are: a **known** API set
  without `embeddings` is refused by profile and model, an **unknown** set stays accepted, and the
  resolved family must be `openai-chat` — the only family this build has an embeddings wire for.
- An embeddings call is charged to the run through the loop's one usage-accounting path and recorded
  in schema 4 under usage `embeddings`. Schema 4 is unchanged; the attribution usage is the free
  string the 2026-11-22 amendment relies on.
- The consumer's index is a rebuildable cache under the state root's `cache` directory, keyed by
  workspace and model. It is not provider configuration and not session content: deleting it costs
  re-embedding time and nothing else, which is the line decision 5 draws around session state.

Image generation remains named and unbuilt. Building a role is this change's rule for any later one:
the requirement is enforced at resolution, the attribution is recorded, and a consumer exists.

## Amendment 2026-11-28 — `image` is built by the image generation tool

The maintainer decision on issue #980 settled where a generated image lives (the session sidecar,
content-addressed as ADR-0018 defines) and how a turn names it (the typed image part). Issue #1027 is
that consumer: the
`generate_image` tool resolves `usages.image` and asks the profile for an image. This section is the
later word; the earlier text stays as the record of the 2026-11-22 and 2026-11-25 decisions.

The role is unchanged in shape: one settings key (`usages.image`), one declared requirement at
resolution (the model's **image output**), and one attribution value (`image`). What is new is that
all three are exercised:

- The requirement is checked the way decision 3 says capability facts are: a **known** output
  modality set without `image` is refused by profile and model, an **unknown** set stays accepted,
  and the resolved family must be `openai-chat` — the only family this build has an image-generation
  wire for. The `image-generation` API name stays a capability label; decision 3 keeps API names
  from gating an existing usage, so the usage's declared requirement does the check.
- A generation call is charged to the run through the loop's one usage-accounting path and recorded
  in schema 4 under usage `image`. Schema 4 is unchanged; the attribution usage is the free string
  the 2026-11-22 amendment relies on.
- The produced bytes are not new state: they go through the same tool-image path as a `read` image,
  into the session's content-addressed sidecar and the transcript's typed image part. This amendment
  adds no session-format value.

Both roles named on 2026-11-22 that a consumer now exists for are built; the 2026-11-28 rule for any
later one is unchanged.
