## Why

The `embeddings` capability entry (#357's second item) has no consumer: the catalog names the API,
ADR-0027 names the usage, and nothing in a run ever sends an embeddings request, so the entry is
unproven metadata. The maintainer decision on #981 makes repo search the consumer, so a tool the
agent can call is what proves the capability end to end — and removes it again if the benchmark
comparison shows it buys nothing.

## What Changes

- Add an optional `semantic_search` tool over the workspace — off by default, enabled exactly like
  `grep`/`find`/`ls` (`tools.extra`, `LUNATE_EXTRA_TOOLS`, `--extra-tools`). When it is not enabled,
  or no embeddings profile resolves, it is not offered to the model.
- Add `usages.embeddings` as a configured usage whose declared requirement is the model's
  `embeddings` API, and attribute its calls as usage `embeddings` (schema-4 attribution is a free
  string, so no session-schema bump).
- Make the workspace index a rebuildable cache under the Lunate state root's `cache` directory:
  never inside the workspace (so never committed), never written into a session, and safe to delete
  — a deleted index costs re-embedding time and nothing else.
- Add a `--extra-tools` passthrough to the SWE-bench adapters so the tool-on vs tool-off comparison
  can be run; the comparison itself needs Docker and live credentials (maintainer step after merge).
- Amend ADR-0027's usage-role amendment: the `embeddings` role moves from "named, not built" to
  built, with its requirement enforced at resolution and its attribution recorded.
- Document the tool, the usage and the cache in `docs/cli.md`, `docs/environment-variables.md`,
  `docs/settings.md`, `docs/models-and-providers.md`, `docs/providers.md` and `docs/dev/guide.md`.

## Capabilities

### New Capabilities

- `semantic-search`: the optional `semantic_search` tool, its ranked output, its embeddings-usage
  binding, and the rebuildable index cache that never touches git or a session.

### Modified Capabilities

- `provider-store`: `usages.embeddings` joins the configured usages, requires the model's declared
  `embeddings` API, and is a recorded attribution value.
- `agent-config`: `tools.extra` accepts `semantic_search` beside `grep`, `find` and `ls`.
- `benchmark-harness`: the adapters pass `--extra-tools` through to the in-image Lunate run so the
  tool-on and tool-off arms of one subset are reproducible.

## Impact

- `src/Lunate.Agent`: the internal tool-context state gains a usage record hook so a non-chat model
  call is charged to the run like every other usage.
- `src/Lunate.Coding`: the embeddings usage resolver and client, the index, the tool, the optional
  tool table, the settings key, the schema and the three frontends' composition.
- `src/Lunate.Ai`: one embedding-generator seam on the provider client factory, reusing the existing
  headers/credential transport.
- `scripts/eval/swebench*.py` and `eval/README.md`: the `--extra-tools` passthrough.
- Docs, ADR-0027's amendment and the changelog fragment.
- No new NuGet package, no session-schema version bump, no new core tool.
