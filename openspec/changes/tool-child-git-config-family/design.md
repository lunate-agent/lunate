# Design — tool-child-git-config-family

Binding implementation detail. Where this file pins a behaviour, apply follows it verbatim; a
contradiction between this file and the spec delta is a stop-and-report, not a choice.

## Context

`EnvironmentScrubber.Build` is the one builder of a child environment. `ChildProcess.BuildEnvironment`
builds every tool child (the `bash` tool and the post-bash change scan's `git` invocations) with the
`Inherited` policy, `strip: CredentialVariables.All` and `passthrough: ToolEnvironmentPassthrough.Names`.
Inside the builder, the `Inherited` branch removes the stripped names, then removes every name the
credential-shape rule matches, then re-admits the pass-through names, then applies declared values
(skipping a stripped or shape-withheld name).

ADR-0036 owns that rule. Its word list contains `KEY`, so a parent that configures git through
git's environment configuration — `GIT_CONFIG_COUNT=3`, `GIT_CONFIG_KEY_0..2`,
`GIT_CONFIG_VALUE_0..2` — loses exactly the three `GIT_CONFIG_KEY_<n>` names and keeps the count and
values. Git reads the first `GIT_CONFIG_COUNT` key/value pairs and fails the whole invocation when a
key is missing (`error: missing config key GIT_CONFIG_KEY_0`), so the tool child's git is unusable and
the change scan's `git status` fails the same way (#871). ADR-0036's Consequences already name
`GIT_CONFIG_KEY_0` as a false positive it accepted; this change pays that one back.

```
ChildProcess.BuildEnvironment(request)
        │  EnvironmentScrubber.Build(Inherited, strip, passthrough, set)
        ▼
  inherit ──▶ strip Lunate's resolved names ──▶ shape deny ──▶ pass-through re-admit ──▶ declared values
                                                     │
                                        withheld = shaped && not a git env-config name
                                                     │
                                        GIT_CONFIG_COUNT / _KEY_<n> / _VALUE_<n> ──▶ kept whole
```

## Goals / Non-Goals

**Goals:**

- The seven names of a three-entry git environment configuration reach a tool child together.
- Real credentials (`*_TOKEN`, `*_API_KEY`, `AWS_SECRET_ACCESS_KEY`, …) stay withheld.
- One rule, one builder, no new setting, no public API change, no new package.

**Non-Goals:**

- No general "cohesive family" mechanism, no user-configured family list, no wildcard or regex.
- No change to the resolved-name strip, the `Scrubbed` policy, the pass-through semantics or the
  shape rule's word list.
- No value inspection: the rule stays a name rule, and `GIT_CONFIG_VALUE_<n>` is not read or filtered.

## Decisions

- **The family is exempt, not withheld.** Both halves of "together or not at all" fit the failure
  (`git` refuses a partial set), but only one keeps the card's acceptance: the lane's configuration
  must let `git status` and `git commit` succeed and see an empty `credential.helper`. Withholding
  all seven would leave git exactly as broken as today, only quieter.
- **The exemption is one fixed predicate in `EnvironmentScrubber`, beside the word list.**
  `IsCredentialShaped(name)` stays the word rule; the deny sites ask
  `IsCredentialShaped(name) && !IsGitConfigurationName(name)`. Alternative rejected: a `Func<string,
  bool>` or family list on `Build` — public API churn for a rule with one shape, and ADR-0036 decision
  5 keeps the policy in the one builder, not at a spawn site.
- **The family is exactly three spellings, with a numeric index.** `GIT_CONFIG_COUNT`,
  `GIT_CONFIG_KEY_<n>`, `GIT_CONFIG_VALUE_<n>`, `n` all digits, compared case-insensitively like the
  shape rule. `GIT_CONFIG_PARAMETERS`, `GIT_CONFIG_GLOBAL`, `GIT_CONFIG_SYSTEM` and
  `GIT_CONFIG_NOSYSTEM` match no credential word today and are deliberately left out, so the
  exemption stays as narrow as the break it repairs.
- **The exemption applies to declared values too.** The `set` deny consults the same predicate, so a
  spawn site that declares `GIT_CONFIG_KEY_0` is not silently half-broken. Consistent with ADR-0036
  decision 4.
- **The scan needs no code change.** `BashChangeTracker.RunGitAsync` spawns through
  `LocalRunner`/`ChildProcess.Start` with `Environment: null`, so the scan's git gets the same
  `BuildEnvironment` policy. The test pins that, so a future scan that stops sharing the policy fails
  loudly instead of silently falling back to the stamp walk.

## Risks / Trade-offs

- **A `GIT_CONFIG_VALUE_<n>` may hold a secret** (an inline credential helper, an `http.extraHeader`
  with a token) and now reaches the tool child. Mitigation: it is the caller's own git configuration,
  and the child already inherits `HOME`, so the same configuration is readable from
  `~/.gitconfig`; the shape rule never inspected values and does not start now. The ADR states it, so
  no documentation claims the tool child sees no credentials.
- **`GIT_CONFIG_KEY_<n>` is no longer withheld, and the next name that pairs with another
  documented tool's protocol will be found the same way.** Mitigation: the family is one predicate
  with a comment naming the failure and the issue; a second such family is a new ADR, not a widening
  of this one.
- **The scan test sets process-wide variables.** Mitigation: it lives in its own
  `DisableParallelization` collection (the shape `ToolEnvironmentCollection` already uses) and
  restores every name in a `finally`.

## Migration Plan

None: behaviour changes only for names that match the git family. No data, setting or format
changes, so a rollback is reverting the commit.

## Open Questions

None.
