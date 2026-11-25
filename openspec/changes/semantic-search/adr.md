# ADR Review Manifest

- Status: completed
- Review date: 2026-11-25

## Review Summary

No new repository-level ADR is created. The decision this change implements — `embeddings` is a usage
role with the embeddings API as its declared requirement, and it is now built by a consumer — is not
a new architectural commitment: it is the role ADR-0027 already names, using the capability model,
the provider transport, the optional-tool table and the schema-4 attribution ADR-0027 and ADR-0002
already fix. The change therefore records a dated amendment section on ADR-0027 (the record that
defines usage roles) and leaves every other ADR untouched. The amendment appends, it does not rewrite
the 2026-11-22 text: the earlier "named, not built" wording stays as the history of that decision,
and the new section is the later word.

The one durable rule worth carrying forward is stated in the amendment: a usage role is built only
when a consumer exists, and building it means its requirement is enforced at resolution and its
attribution value is recorded — the same three costs ADR-0027's 2026-11-22 amendment lists.

## In-Force ADRs Reviewed

- `adr/0002-layering-and-meai-model-types.md` — the embeddings client is a MEAI
  `IEmbeddingGenerator` over the existing provider client factory; no parallel model type is added.
- `adr/0003-loop-own-vs-maf-harness.md` — the harness still owns the loop; the tool reports usage
  through the harness's one accounting path, not through a second model-call path.
- `adr/0015-session-format.md` — no session-format change: attribution is a free string in schema 4
  and the index never reaches a session file.
- `adr/0016-workspace-boundary.md` — the index walk uses the same workspace boundary and `.gitignore`
  rules as the existing search tools.
- `adr/0027-provider-store-model-profiles-and-usages.md` — amended in place with a new dated section:
  the `embeddings` role moves from named to built, and its requirement is the model's declared
  embeddings API.
- `adr/0041-net11-only.md` and `adr/0045-decision-model-package.md` — no target-framework or package
  boundary changes; the embeddings seam lives in `Lunate.Ai` beside the chat factory.

## New Durable ADRs Created

- None. The amendment to `adr/0027-provider-store-model-profiles-and-usages.md` is the durable
  handoff, because the rule it extends (usages as capability-bearing roles) belongs to that record.

## Consequences Carried Forward

- A future usage role (`image`, for example) is built the same way: one settings key, one checked
  requirement, one attribution value, and a consumer that proves it.
- Removing the tool, if the benchmark comparison says it buys nothing, removes the consumer but
  leaves the role and the capability facts, which is the state this change starts from.
