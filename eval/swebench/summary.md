# SWE-bench — `semantic_search` on and off — 1028-semantic-search-on-off

Dataset: `SWE-bench/SWE-bench_Verified` (`test`), subset: 5 instances (the first published run's sample).
Cost cap: $5.00 across the whole comparison; reported spend: $2.6027.

| Arm | Model | Tier | Resolved | Spend | Steps | In / out tokens | $/instance |
| --- | --- | --- | --- | --- | --- | --- | --- |
| lunate, tool off | `opencode-go/deepseek-v4.1-flash` | budget | 4/5 | $0.0792 | 227 | 1,890,031 / 49,117 | $0.0158 |
| lunate, tool on | `opencode-go/deepseek-v4.1-flash` | budget | 3/5 | $0.0878 | 230 | 2,053,562 / 58,243 | $0.0176 |
| lunate, tool off | `openrouter/anthropic/claude-opus-5.5` | frontier | 4/5 | $1.0417 | 34 | 200,912 / 11,902 | $0.2083 |
| lunate, tool on | `openrouter/anthropic/claude-opus-5.5` | frontier | 4/5 | $1.3135 | 41 | 255,597 / 14,556 | $0.2627 |

`semantic_search` was called **0 times in every "tool on" instance** — 5 budget and 5 frontier. On this
subset the tool changed nothing: the differences in steps, tokens and spend between the arms are
run-to-run variance, and the one-instance resolve difference (4/5 against 3/5 in the budget pair) is
the same kind of variance, not an effect of the tool. `django__django-13128` is unresolved in every arm.

The budget "tool on" arm is a rerun: the first run of it had only the chat model's credential
forwarded, so the embeddings provider could not resolve and the tool was not offered at all — that
arm measured the tool-off arm. Its evidence is kept under `lunate-tool-on-prefix/` and marked
`superseded` in `summary.json`; the rerun, with the adapter fix that forwards every credential the
prepared home names, is `lunate-tool-on/`.

This is a measurement, not the keep-or-remove verdict on the tool: that belongs to the 50-instance
run on lunate#860.
