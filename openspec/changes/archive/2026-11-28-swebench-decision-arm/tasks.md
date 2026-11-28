## 1. Run definition

- [x] 1.1 Add the `decision` arm (`main`, `decision`, `decisionLayer: "on"`, the card's note) to `eval/swebench/run.json` and set `limits.maxCostUsd` to 2.
- [x] 1.2 `load_definition` accepts exactly one budget and one frontier arm plus at most one optional `decision` arm, and refuses a decision arm without `main.id`, `decision.id`, either credential or `decisionLayer`.

## 2. The arm's configuration and command

- [x] 2.1 `arm_chat_model` / `arm_credentials` answer the `--model` id and the credential variables for either arm shape.
- [x] 2.2 `decision_home_files` renders the arm's `settings.json` (main and decision profiles, `decisions` block) from the definition; the orchestrator writes it under the run tree and passes `--lunate-home`.
- [x] 2.3 `adapter_command` passes the main model id, the arm's credential variables by name and the generated home for a decision arm.
- [x] 2.4 The generated `decisions` block turns on both consumers the arm's note declares — routing and the approval gate, each in shadow — so the recorded configuration matches the behaviour it claims; the home tests assert it.

## 3. Measurements in the summary

- [x] 3.1 `trajectory_totals` counts decision-attributed usage separately and records how many usage lines carried no price.
- [x] 3.2 A multi-model arm's instance spend is the sum of the priced records; a record without a price is unpriced.
- [x] 3.3 Instance rows record `seconds` from the adapter's run record; the baseline records its own start and finish.
- [x] 3.4 `summarize_arm` reports `seconds` and `costPerResolvedUsd`; `render_markdown` shows wall time and cost per resolved instance for every arm.
- [x] 3.5 The baseline arm skips the `decision` tier.

## 4. Documentation

- [x] 4.1 `eval/README.md` documents the third arm, the generated home, the four per-instance numbers, cost per resolved instance and the US$2.00 cap.

## 5. Verification

- [x] 5.1 Tests in `tests/scripts/test_swebench_run.py` cover 1.2, 2.1–2.3, 3.1–3.5.
- [x] 5.2 `openspec validate swebench-decision-arm --strict` and `dotnet csharpier check .` are green; the script tests pass.
