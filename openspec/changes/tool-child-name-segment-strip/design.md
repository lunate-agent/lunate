## Context

The tool child's environment is composed in exactly one place: `ChildProcess.BuildEnvironment`
builds a `ProcessRunRequest` into a child environment through
`EnvironmentScrubber.Build(ChildEnvironmentPolicy.Inherited, …)`, handing it the known credential
names (`CredentialVariables.All`) to strip and the user's exact-name pass-through list
(`ToolEnvironmentPassthrough.Names`) to re-admit after the strip. Both inputs are lists of names,
and a list cannot express "a name whose segment is `TOKEN`".

```
ChildProcess.BuildEnvironment                       (Lunate.Coding)
        │  policy: Inherited, strip: CredentialVariables.All,
        │  passthrough: ToolEnvironmentPassthrough.Names
        ▼
EnvironmentScrubber.Build                            (Lunate.Extensibility.Abstractions)
        │  inherited set ─► remember pass-through values ─► remove strip names ─► re-admit
        ▼
Child process environment (bash tool)
```

The provider-credential names Lunate resolves are already a lower-layer concern: the segment rule
belongs beside them in `CredentialVariables`, but the builder that applies the strip lives below
`Lunate.Coding` and cannot reference it.

## Goals / Non-Goals

**Goals:**

- The tool child's inherited environment loses a variable whose underscore-separated segment is
  `KEY`, `TOKEN`, `SECRET` or `PASSWORD` (case-insensitive), in addition to the known credential
  names.
- The segmentation is in the rule: `MONKEY` and `GITLAB_TOKENIZE_TIMEOUT` survive.
- `toolEnvironment.passthrough` wins over the segment rule exactly as it wins over the known-name
  strip.
- `EnvironmentScrubber.Build`'s existing six-parameter signature keeps its current behaviour, so an
  extension compiled against it keeps working.

**Non-Goals:**

- No change to the `Scrubbed` policy, the exact-name pass-through matching, the user-scope refusal
  or the startup admission notice (#742/#746).
- No new setting; no wildcard or pattern syntax in `toolEnvironment.passthrough`.
- No change to `CredentialVariables.All`: the known-name list stays the list of names Lunate
  resolved, and the collision warning keeps reporting against it.

## Decisions

- **The rule lives in `CredentialVariables`, as a predicate beside `All`.** It is the credential
  vocabulary (`KEY`, `TOKEN`, `SECRET`, `PASSWORD`) and the reason the list exists: a name a
  model-chosen command must not read. `All` stays the resolved-name list, because it answers a
  different question ("did Lunate resolve this name?") that the collision warning still asks.
  Alternative rejected: a second static class, which would split the credential vocabulary across
  two files.
- **The builder takes the rule as an optional predicate, via a new overload that keeps the old
  signature.** `Inherited` callers may pass `stripMatching: name => …`; the six-parameter overload
  delegates with `null`, so today's callers and already-compiled extensions get exactly today's
  behaviour. The predicate is applied under `Inherited` only, like `strip`: `Scrubbed` already
  cannot carry an undeclared name. Alternative rejected: a name-list parameter of patterns, which
  would put string parsing in the shared builder and make the segment rule a second implementation
  there.
- **Ordering inside `Build` is unchanged: remember pass-through values, remove strip names *and*
  predicate matches, re-admit the remembered values, then apply declared values with the strip as a
  deny.** So an exact pass-through name wins over both denies, and a declared `ExtraEnvironment`
  value still cannot re-introduce a stripped name.
- **`ChildProcess.BuildEnvironment` passes `CredentialVariables.IsSecretName`.** It is the one place
  that names the tool child's policy, so the default reaches every tool call without a spawn site
  deciding anything.
- **The startup collision warning keeps its current trigger (known credential names).** A listed
  `GITHUB_TOKEN` now survives the strip without a warning naming it as a credential: it is not a
  name Lunate resolved, and the admission notice already names it. Widening the warning is a
  separate presentation decision, not part of the default.

## Risks / Trade-offs

- [A user's own `*_TOKEN`/`*_KEY` variable is now absent from a tool child] → The pass-through list
  is the documented way back in, and the new ADR-0036 addendum states the boundary plainly; the
  guide and configuration docs name the rule.
- [A false negative by segmentation: a name with no underscore is its own segment, so `TOKEN`
  alone is stripped and `MYSECRET` is not] → Deliberate: the rule the maintainer chose is segment
  equality, and the docs say so.
- [Binary compatibility for compiled extensions] → The old signature is untouched; the new
  capability is an added overload. `PublicAPI.Unshipped.txt` records it.

## Migration

None for users: the rule is the new default, and only a `toolEnvironment.passthrough` entry admits
a matching name. No setting, schema or session-format change.
