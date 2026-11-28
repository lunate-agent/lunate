## Context

`eval/swebench/run.json` names two single-model arms (`budget`, `frontier`); the orchestrator
iterates that list, runs the adapter with `--model <id>`, prices each instance from the token lanes
at that one model's catalog price, and merges a summary of `resolved` and `spendUsd`. The decision
layer already runs in print mode (ADR-0044/0045, `docs/decisions.md`), and the adapter already
accepts `--lunate-home` so an arm can carry a prepared `settings.json` (#1028). Two models at two
prices are the only new shape; nothing in `src/` has to change.

```
run.json ──► swebench_run.py ──► swebench.py (adapter, one container per instance)
              │                    │
              │                    └─ LUNATE_HOME = prepared settings.json
              ├─ trajectory.jsonl (usage records: usage, modelId, costUsd each)
              └─ summary.json / summary.md
```

## Goals / Non-Goals

**Goals**

- The third Lunate arm is committed data: `main`, `decision`, `decisionLayer` and a note in
  `run.json`, and the arm's prepared home is generated from that data, so the configuration the
  numbers came from travels with them.
- The summary reports resolved, spend, steps and wall time per instance, and cost per resolved
  instance per arm, for all five arms.
- A US$2.00 cap for one run; the existing cap-stop behaviour reports what it got.

**Non-Goals**

- Changing the decision layer, the decision usage or the print-mode JSON in `src/`.
- Producing the live numbers. The run needs Docker, a `linux-x64` single-file build and an
  `OPENCODE_GO_API_KEY`; this change makes the run report them.
- A baseline decision arm. mini-swe-agent has no decision layer.

## Decisions

### D1 — a new tier literal, not a second field on a model entry

`models[]` already means "one chat model per arm". The decision arm is two models and a layer
switch, so it gets its own tier literal `decision` with `main`, `decision`, `decisionLayer` and
`note`, and `load_definition` keeps "exactly one budget, exactly one frontier" while allowing at
most one `decision` arm. A single-model arm keeps resolving through `id`; `arm_chat_model` answers
"which id does `--model` take" once, for both shapes.

Alternative rejected: a nested `models` object per arm. It rewrites the two existing arms and the
committed summary's shape for no gain.

### D2 — the prepared home is generated from the arm, in the run tree

The orchestrator writes `<work-root>/lunate-home/<tier>/settings.json` and `providers.json` and
passes that directory as `--lunate-home`; the adapter copies exactly those files into the instance
run, which is the committed evidence for the configuration. `settings.json` holds one profile per
model and the `decisions` block:

```json
{"decisions": {"profile": "decision", "routing": "shadow",
               "routingProfiles": ["main", "decision"], "approvals": "shadow"}}
```

`decisionLayer: "on"` maps to the documented "turn it on" block with the two consumers the note
names: routing in shadow and the approval gate in shadow. The note says the decision model advises
on routing *and stops*, so a home that enabled only routing would make the arm's recorded
configuration disagree with the behaviour it claims — a stop never reaches the decision model at
all. `approvals` defaults to `off`, and the print mode's `NonInteractiveApprover` is still an
approver, so `shadow` is what makes the harness evaluate a stop while the ordinary approval flow
answers it: shadow records the would-have result and cannot change an approval outcome, delay a
call or fail closed (#859 review). Shadow routing keeps the main model as the chat model (only
`enforce` can switch it), so the arm really does measure "the main model with the decision model
advising". Consumers that act are deliberately out: with `--yolo` an `enforce` approval gate fails
closed on a deny, which would measure the gate rather than the agent.

A profile resolves only against a *configured* provider: `ModelProfileResolver.Resolve` looks the
profile's provider name up in the provider store, and the store holds `providers.json` entries plus
presets migrated from stored keys — an in-image `LUNATE_HOME` has neither, so a profile naming
`opencode-go` would fail with "does not name a configured provider" while `--model
opencode-go/…` succeeds through the preset fallback. The generated `providers.json` therefore names
one entry per preset/credential pair (`bench-opencode-go`) built from the repository's own preset
table — endpoints, models URL, headers and family headers — with `credentialReference:
env:OPENCODE_GO_API_KEY` instead of the preset's `keyVariable`. One entry per pair, not two, so the
print-mode selection string never sees two entries of one kind; the credential appears only as a
variable name, and the arm's `credential` field drives both the reference and `--env` forwarding.

### D3 — spend for a multi-model arm comes from the priced records

The run stamps `costUsd` on every `usage_updated` line, priced with the record's own `modelId`, and
decision usage is recorded with the decision model's id. Summing those records prices the two
models at their own catalog rates; re-pricing the summed token lanes at the main model's price (what
a single-model arm does) would under- or over-state the arm. A usage record with no price makes the
instance unpriced and stops the run, matching the existing unpriced rule. Single-model arms keep
lane pricing unchanged so `rerun-624-bosgame-2` stays reproducible.

### D4 — steps split by attribution, wall time from the instance record

`usage_updated` counts decision calls too, so `steps` stays "records not attributed to the
`decision` usage" and a new `decisionSteps` counts the rest. Wall time is
`finishedAt - startedAt` of the instance in the adapter's `run.json`; the baseline records the same
two timestamps around its own run. `summarize_arm` adds `seconds` and `costPerResolvedUsd`; the
latter is `None` when spend is unpriced or nothing resolved, so the summary never divides by zero or
publishes a fake per-resolve cost.
