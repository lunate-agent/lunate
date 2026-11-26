## 1. The specification correction

- [x] 1.1 Correct the `agent-tools` "Optional search tools" requirement in the delta: name
  `grep`/`find`/`ls`'s `[N more …]` count markers, `semantic_search`'s
  `[N shown; more matching regions exist, raise max_results for more]` count marker and its
  `[search output capped at 8192 bytes]` byte marker, and keep the rest of the requirement intact.
- [x] 1.2 Add the `semantic_search` count-cap and byte-cap scenarios to the same delta.
- [x] 1.3 Confirm `changes/1035.md` still covers the card; a spec-only correction adds no release
  note and `CHANGELOG.md` stays untouched.

## 2. Verification

- [x] 2.1 Run `dotnet csharpier check .` (no source changed, but the gate includes it).
- [x] 2.2 Run the named search-tool tests that assert the markers:
  `SemanticSearchToolTests` and the `grep`, `find` and `ls` tool test classes.
- [x] 2.3 Run `openspec validate clarify-search-cap-markers --type change --strict`.

## 3. Sync and archive

- [x] 3.1 Sync the `agent-tools` delta into `openspec/specs/agent-tools/spec.md` with the OpenSpec
  sync workflow — never by hand — and commit it.
- [x] 3.2 Archive the change with `openspec archive clarify-search-cap-markers --yes` as the branch's
  last commit, then run `openspec validate --all --strict`.
