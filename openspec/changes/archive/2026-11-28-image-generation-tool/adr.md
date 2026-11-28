# ADR Review Manifest

- Status: completed
- Review date: 2026-11-28

## Review Summary

ADR review completed for this change. No new repository-level ADR is created: the decision this
change implements — `image` is a usage role whose declared requirement is the model's image output,
and it is now built by a consumer — is the role ADR-0027 already names, using the capability model,
the provider transport, the tool contract and the schema-4 attribution that ADR-0027, ADR-0002 and
ADR-0043 already fix. The change appends a dated amendment section to ADR-0027 (the record that
defines usage roles) and leaves every other ADR untouched. The amendment appends; it does not rewrite
the 2026-11-22 or 2026-11-25 text, which stay as the history of those decisions.

The durable rule carried forward is the one the embeddings amendment already stated: a usage role is
built only when a consumer exists, and building it means its requirement is enforced at resolution
and its attribution value is recorded — the same three costs ADR-0027's 2026-11-22 amendment lists.
This change adds the `image` instance of that rule and the wire fact that only the `openai-chat`
family serves it in this build.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the image client is a MEAI `IImageGenerator` built by
  the existing provider client factory; no parallel model type is added.
- `adr/0003-loop-own-vs-maf-harness.md` — the harness still owns the loop; the tool reports its usage
  through the harness's one accounting path, and the tool-image history message stays the loop's.
- `adr/0015-session-format.md` and `adr/0018-addendum-images.md` — no session-format change: the
  generated image reuses the content-addressed sidecar and the typed `image` part the addendum fixes.
- `adr/0016-workspace-boundary.md` — the optional workspace write uses the same workspace boundary and
  path resolution as `write`.
- `adr/0027-provider-store-model-profiles-and-usages.md` — amended in place with a new dated section:
  the `image` role moves from named to built, its requirement is the declared image output, and only
  `openai-chat` serves it in this build.
- `adr/0041-net11-only.md` and `adr/0043-contract-unions.md` — no target-framework change, and no
  contract change: the tool is a new `ITool` implementation, not a new event or hook-result family.

## New Durable ADRs Created

- None. The amendment to `adr/0027-provider-store-model-profiles-and-usages.md` is the durable
  handoff, because the rule it extends (usages as capability-bearing roles) belongs to that record.

## Consequences Carried Forward

- A future usage role is built the same way: one settings key, one checked requirement, one
  attribution value, and a consumer that proves it.
- Removing the tool, if the capability proves valueless, removes the consumer but leaves the role and
  the capability facts, which is the state this change starts from.
