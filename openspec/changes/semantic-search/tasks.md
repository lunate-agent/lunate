## 1. Settings and the embeddings usage

- [x] 1.1 Accept `usages.embeddings` in `SettingsStore.Profiles.ReadUsages`, add the key to
  `src/Lunate.Coding/settings-schema.json`, and add its probe to `SettingsSchemaTests`.
- [x] 1.2 Add `EmbeddingsUsage` (profile resolution, the declared-API requirement, the
  `openai-chat` family rule, the suggested notice when unusable) with tests for the known-absent
  refusal, the unknown-set acceptance and the family refusal.
- [x] 1.3 Add the internal embedding-generator seam to `ChatClientFactory` over the existing
  header and credential transport, with a test that the request reaches the provider's
  `/embeddings` path with the declared credential placement.

## 2. The index and the tool

- [x] 2.1 Add `SemanticIndex`: the `.gitignore`/binary-aware walk, 40-line chunks, the digest-keyed
  reuse, the 2,000-chunk cap, the JSON cache under the state root's `cache` directory, and the
  atomic write. Tests: build, reuse, one-file change, corrupt cache, ignored and binary files.
- [x] 2.2 Add `SemanticSearchTool` with `query` and `max_results`, ranked path/start/end/snippet
  output, the byte cap and the argument errors, over an `IEmbeddingsClient` seam. Tests: ranking,
  cap, bad arguments, empty workspace, provider failure.
- [x] 2.3 Add `semantic_search` to `OptionalTools` and register it in `HarnessFactory` only when an
  embeddings binding resolves; warn once naming `usages.embeddings` otherwise. Tests: disabled by
  default, enabled plus profile registers, enabled without profile does not.
- [x] 2.4 Wire the interactive, print and ACP frontends to resolve the binding when the name is
  enabled, and skip `embeddings` in the interactive chat-usage loop.

## 3. Usage attribution

- [x] 3.1 Stamp an internal usage hook into `AgentToolContextState`/`AgentHarness` and record an
  `embeddings` attribution through the one `AccumulateUsage` path.
- [x] 3.2 Record the attributed usage in the interactive session beside the decision usage, and add
  a test that an embeddings usage reaches `/cost` and the session entry.
- [x] 3.3 Confirm no session-schema change: an existing schema-4 session loads unchanged and a
  reload keeps the embeddings attribution.

## 4. Benchmark enabler

- [x] 4.1 Add `--lunate-home <dir>` to `scripts/eval/swebench.py`: copy exactly `settings.json` and
  `providers.json` into `<run-dir>/lunate-home`, refuse a directory without `settings.json`, and
  record the path in `run.json`.
- [x] 4.2 Pass it through `scripts/eval/swebench_run.py`'s adapter command, with the adapter tests
  updated and `eval/README.md` documenting the on/off arms.

The comparison itself — Docker, the official instance images and a live provider credential — is a
maintainer step after merge; this change delivers the machinery and the recorded fields.

## 5. Documentation, ADR and changelog

- [x] 5.1 Update `docs/cli.md`, `docs/environment-variables.md`, `docs/dev/guide.md`,
  `docs/models-and-providers.md` and `docs/providers.md`; regenerate `docs/settings.md` with
  `scripts/docs-sync`.
- [x] 5.2 Append the dated amendment to `adr/0027-provider-store-model-profiles-and-usages.md` and
  update its status line.
- [x] 5.3 Add the changelog fragment at `changes/<pr-number>.md`.

## 6. Verification

- [x] 6.1 Run `dotnet csharpier format .`, the fast gate, `openspec validate semantic-search
  --type change --strict`, and the touched test classes; the golden session files stay untouched.
- [x] 6.2 Sync the delta specs, then `openspec archive semantic-search --yes` as the last commit.
