## Why

The setup wizard's decisions settings list closes after a single change, so changing several
decision switches means re-entering the decisions step once per switch. The list should stay open
after each change and offer an explicit way to leave it.

## What Changes

- The decisions settings step shows its list again after each switch is answered, keeping the
  answers already collected in the same pass.
- The list's first entry becomes **Done — leave the decisions settings**, which exits the step and
  stages the collected answers (or the defaults when nothing was changed).
- A pass that changes two settings writes both in one summary and one commit.
- The existing full walk and `/setup` entry behaviour stays otherwise unchanged; accepting the
  defaults still writes nothing for the decisions layer.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `setup-wizard`: the decisions settings step collects several switches in one pass and exits only
  through the Done entry.

## Impact

- `src/Lunate.Coding/InteractiveSession.SetupWizard.cs`: the decisions settings step loop and its
  staged answer accumulation.
- `tests/Lunate.Coding.Tests`: driver-level setup-wizard tests for the repeated list and the
  one-pass multi-change sequence.
- `docs/first-run.md`: the decisions step description names Done.
- No public API, dependency, settings-schema or session-format change.
