# 1028-semantic-search-on-off

Four arms of the `semantic_search` comparison, run on Wall-E (bosgame) against the v0.8.0 linux-x64
release binary, on the first published run's five Verified instances.

- `lunate-tool-off/` — `--arm lunate` without a prepared home (budget and frontier).
- `lunate-tool-on/` — the budget arm rerun with `--lunate-home` and the adapter fix that forwards the
  home's own `env:` credentials (lunate#1045).
- `lunate-tool-on-prefix/` — the same home before that fix, kept as the evidence that the tool was
  silently not offered (its budget rows are superseded; the frontier rows are the "offered, never
  called" measurement).

Per instance the committed evidence is `predictions.jsonl` and, when the instance submitted a patch,
`report.json` — the official evaluation's own report. Trajectories and logs stay out of git.
