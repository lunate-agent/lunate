## Why

Slice S1 of the 0.4.0 documentation architecture (#749) delivered `scripts/docs-check`, the
`docs/docs.json` navigation manifest and its wiring into `docs-lint.yml`, `tree-checks.yml`, the
`Process` job and both `scripts/verify.sh` profiles, and it merged as #801 — but no spec records it.
`ci-pipeline` still enumerates the checks that run once in the platform job and in the docs profile
without the link, anchor, redirect and navigation check, and `repo-quality` owns the documentation
conventions without stating what the checker enforces. Spec and code now disagree, and AGENTS.md
makes the spec the source of truth: deleting the step from a workflow and the gate is currently a
legal change. The checker is what every later slice verifies with, so its contract has to be written
down before those slices lean on it.

## What Changes

- `repo-quality` gains one requirement for the documentation check: every relative link and `#anchor`
  on the user and maintainer pages resolves, every redirect is the four-line stub (or a maintainer
  rename with no stub), `docs/docs.json` lists every user-path page exactly once, and every page
  carries a runnable marker or a `proseOnly` reason.
- `ci-pipeline`'s per-commit verification matrix names the check in the OS-independent list the single
  Linux job runs, in the docs profile, and in the push-to-`main` sentence, with a scenario that a
  broken link turns the gate red.
- No code, workflow or page change: the behaviour and its tests exist on `main`, and this change only
  records them. `docs/README.md` already states the rule in prose and needs no edit.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `repo-quality`: adds the documentation link, anchor, redirect and navigation check as a repository
  convention with its scenarios.
- `ci-pipeline`: names the documentation check in the OS-independent set, the docs profile and the
  documentation-only push, so removing it becomes a spec change rather than a quiet regression.

## Impact

`openspec/specs/repo-quality/spec.md` and `openspec/specs/ci-pipeline/spec.md` (through the deltas in
this change). No source, script, workflow, documentation page or `docs/docs.json` change; no new
package, public API, runtime behaviour or session format.
