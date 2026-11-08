# ADR-0034: The tool child's environment is an inherited policy plus an exact-name pass-through

Status: accepted — 2026-11-08

## Context

The bash tool runs the user's shell, so its child environment is the `Inherited` policy: everything
the user holds minus every variable Lunate resolves as a provider credential. That strip closed the
leak where a model-chosen command could read the user's provider key out of its environment (#621).
It also made the split absolute: a user cannot let one deliberately owned variable — a project
token, a build-machine marker, a proxy variable whose name collides with a credential name — into a
tool child without a wrapper script the tool itself calls.

Codex CLI's shell environment policy is the model: keep the policy that decides what a child
inherits separate from an allow-list of named variables the user opts back in, and keep the
credential deny as the default. This ADR records that shape for Lunate and the boundary around it.

## Decision

1. **`Inherited` gains an explicit pass-through list of exact names.** The names come from the
   user-scope setting `toolEnvironment.passthrough`, are taken from the inherited set, and are
   re-admitted **after** the credential strip. Matching is by exact name under the platform's
   environment-name comparison (case-insensitive on Windows, case-sensitive elsewhere) — never a
   prefix, suffix or pattern. A listed name the parent does not hold stays absent.
2. **The strip remains the default deny.** A credential name the user did not list is still
   removed, and the existing rule that a declared `ExtraEnvironment` value cannot re-introduce a
   stripped name is unchanged: the pass-through re-admits the inherited value only.
3. **A listed name that is also a credential name is allowed, with one startup warning naming it.**
   An explicit name is an explicit user act; silently refusing would make the setting lie, silently
   allowing would hide a deliberate credential exposure.
4. **The list is read where the tool child's policy is built, never at a spawn site.**
   `ChildProcess.BuildEnvironment` reads the process-wide configured list and hands it to the one
   shared builder; `EnvironmentScrubber.Build` remains the only builder, and the frontends configure
   the list once from resolved settings before the first tool runs.
5. **`Scrubbed` is unaffected.** Extension hosts never receive the inherited set, so the pass-through
   does not apply to them.
6. **The setting is user-scope only, and that is what makes it safe.** A project or workspace
   `.lunate/settings.json` may not carry `toolEnvironment`: the project scope scan refuses the
   section by name before any merge, and `MergeProject` never overlays the resolved section. The
   safety argument is structural, not a tendency — a cloned repository cannot introduce a
   pass-through at all, so it cannot pull a credential into a tool process this way. A repository
   may still reference user providers, define profiles on them and lower limits, because those have
   their own rules; the pass-through has this one.

## Consequences

- The tool child can see a credential the user explicitly names; the warning is the only signal, by
  design. A mistaken entry is a deliberate act the user can see and remove.
- A wildcard or prefix in the list is an exact name that matches nothing; a future pattern feature
  would be a new decision, not an extension of this one.
- The pass-through is a user-scope setting only, and that is what makes the opt-in safe: a project
  settings file is refused by name (`SettingsStore.ValidateProjectScope`) and never merged, so a
  repository cannot widen a child's environment. `ToolEnvironmentScopeTests` pins that a project
  attempt leaves the tool child's environment unchanged.
- `ChildEnvironmentGuardTests` keeps the boundary reviewable: one builder file, the list read in
  `ChildProcess.BuildEnvironment`, and no spawn site deciding an environment of its own.
