## 1. The pinned sample and the arms

- [ ] 1.1 Regenerate `eval/swebench/instances.txt` as the 50 instances at indices 0, 10, …, 490 of the 500-instance Verified test split, with the selection rule in its header.
- [ ] 1.2 Rewrite `eval/swebench/run.json`: the selection rule, the arm list (`semantic-search-off`, `semantic-search-on`, `baseline`), each arm's `tool`, `tier`, model id, credential and, for the Lunate arms, `semanticSearch`; the on arm's `embeddings` model and credential; one US$4.00 cost cap.
- [ ] 1.3 Copy the previous five-instance run's definition into `eval/swebench/runs/rerun-624-bosgame-2/definition/` so the committed summary still names the definition that produced it.

## 2. Arm definitions in the support layer

- [ ] 2.1 `arm_id`, `arm_tool`, `semantic_search_enabled` and `embeddings_model_id` answer an arm entry's identity, tool, tool state and embeddings model, with the tier as the identity fallback for definitions written before this change.
- [ ] 2.2 `load_definition` validates the arm list: unique arm names, a known tool, a model id and credential per arm, `semanticSearch` only on a Lunate arm, an on arm with an embeddings model and credential, an off arm without one, a baseline whose model a Lunate arm also runs, and the decision arm's existing shape.
- [ ] 2.3 `arm_credentials` forwards the embeddings credential of an on arm; `semantic_search_home_files` renders its prepared home (`tools.extra`, `usages.embeddings`, the provider entry with an `env:` credential reference).
- [ ] 2.4 An arm that runs more than one usage model (decision or embeddings) prices its instance from the trajectory's per-record prices; one unpriced record makes it unpriced.

## 3. The orchestrator

- [ ] 3.1 `run_arm` selects an arm by its `tool` instead of by tier lists, keys run directories and the adapter run id by `arm_id`, and keeps the per-instance budget split and the cap stop.
- [ ] 3.2 `run_lunate_instance` writes and passes the generated home for a semantic-search-on arm as well as for a decision arm; the off arm runs with no home.
- [ ] 3.3 Instance rows record `semanticSearchCalls` from `tool_call_start` events; the arm summary sums them; `summary.md` shows the arm id and the search call count; a row's `workDir` and the arm's `armId` are written as before.
- [ ] 3.4 The baseline arm runs only the entries whose tool is `mini-swe-agent`, on the same subset and model.

## 4. Tests

- [ ] 4.1 The pinned subset test asserts the 50 ids and the stride rule against the committed official id list.
- [ ] 4.2 Tests pin the three pinned arms: tools, model ids, credentials, `semanticSearch` on/off, the on arm's embeddings model and the US$4.00 cap.
- [ ] 4.3 Tests cover the validation refusals: unknown tool, duplicate arm, on arm without embeddings, off arm with embeddings, baseline without a Lunate counterpart.
- [ ] 4.4 Tests cover the generated home: `tools.extra`, `usages.embeddings`, the preset-derived provider entry, the `env:` credential reference, LF/UTF-8 output and the absence of the secret.
- [ ] 4.5 A fake-adapter orchestration test runs the three arms on one instance and asserts each arm's row, its search call count and its spend.
- [ ] 4.6 A test asserts an embeddings usage record is not an agent step and that a multi-model arm prices per record.

## 5. Documentation and verification

- [ ] 5.1 `eval/README.md` documents the 50-instance selection rule, the three arms, the generated home, the embeddings credential, the search call count and the US$4.00 cap; the committed five-instance summary is named as that run's record.
- [ ] 5.2 `eval/swebench/runs/.gitignore` keeps the on arm's index cache out of a committed run.
- [ ] 5.3 Run `python3 tests/scripts/test_swebench_run.py` and the new arm tests, `python3 tests/scripts/test_swebench_adapter.py`, `python3 tests/scripts/test_swebench_adapter_preflight.py`, `python3 scripts/docs-check --check`, `dotnet csharpier check .` and `openspec validate swebench-fifty-instance-three-arms --type change --strict`.
