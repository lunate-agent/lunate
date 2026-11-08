## Context

The decisions settings step in the setup wizard is a single picker that answers one switch and then
advances the walk. The step already stages a `WizardDecisionsChoice` and the commit already writes
every non-null switch field, so the close-after-one-change behaviour is in the step driver, not in
the model or the writers. The change is local to `DecisionsStepAsync` and the driver tests.

The wider navigation redesign is 0.4.0 work and is not part of this change.

## Goals / Non-Goals

**Goals:**

- Keep the decisions list open after each switch, preserving the answers already collected in the
  pass.
- Leave the step through an explicit **Done — leave the decisions settings** entry.
- Collect and write several switches from one pass, through the existing summary and commit.

**Non-Goals:**

- A new navigation model for the wizard, a back entry inside the decisions list, or changing the
  full walk's step order.
- Clearing keys that an existing settings file already carries; the step's existing "default writes
  nothing" rule stays as it is.
- Changing the flag path (`lunate setup`), which does not interact with a list.

## Decisions

- **Loop inside the step, not a new step.** The driver repeats the list while it stays on
  `SetupStep.Decisions`; the walk's step order and `WizardMove` contract are unchanged. This keeps
  the change local and avoids inventing a second decision step for the walk.
- **Done replaces the old defaults row and is selected first.** The old first row,
  "Keep the defaults (nothing written for the layer)", is what users pressed to leave with no
  switch. Done takes that position so Enter still leaves the step; when no switch has a non-default
  answer the step records the defaults, preserving the existing no-write behaviour. The other
  rows keep their order.
- **Accumulate staged answers in the step.** The step keeps the collected switches (and the routing
  profiles plus descriptions) as it loops, and records one `WizardDecisionsChoice` when Done is
  chosen. Re-answering a switch replaces that switch's value, including turning it back to its
  default; the other switches from the pass survive. Re-enabling routing asks for the descriptions
  again and replaces the routing set; turning routing off clears that set.
- **No new interaction primitive.** The list and the switch questions reuse the existing picker and
  question components.

```text
DecisionsStep
  pick list ── Done ──────────────► stage collected answers ──► Advanced
      │                                  (or defaults when none)
      ├─ approvals / routing /
      │  questions / tool
      ▼
  answer switch ── Esc ───────────► Abort
      │
      └────────────────────────────► pick list again
```

## Risks / Trade-offs

- [A user selects Done after changing a switch to its default and expects a reset of the other
  switches] -> Done stages what the pass collected; only the switch they re-answered changes.
  The existing settings keys that were never touched stay untouched, as before.
- [The list becomes a hidden multi-select with no visible current values] -> The existing rows
  already describe the switch's default; the summary after Done is the review step and names every
  collected value. Showing current values in the list is outside this 0.3.0 fix.
- [Tests that assumed the step closes after one answer break] -> Update those driver tests to
  choose Done explicitly; the new behaviour is pinned by a two-switch one-pass test.

## Migration Plan

No store migration: the commit writes the same settings keys through the same writer. Rollback is a
code revert; existing settings files remain valid.

## Open Questions

None. No in-force ADR needs revisiting; this is a tactical wizard interaction fix, not a durable
architectural decision.
