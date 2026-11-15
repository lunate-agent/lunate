# ADR-0038: The tool child keeps git's environment configuration whole

Status: accepted — 2026-11-15

Amends: ADR-0036 (the credential-shape strip), whose word list withholds `GIT_CONFIG_KEY_<n>` and
whose Consequences named that name as an accepted false positive; this ADR pays that one back without
widening the rule anywhere else.

## Context

ADR-0036 withholds an inherited variable when any `_`- or `-`-separated part of its name is a
credential word, and `KEY` is one of them. Git's environment configuration is a family of three
spellings: `GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_<n>` and `GIT_CONFIG_VALUE_<n>`, where git reads the
key/value pairs `0..COUNT-1` from the environment. A supervisor that configures git for a session —
the lunate-lane worker does, with `credential.helper` empty, `gc.auto=0` and `maintenance.auto=false`
— therefore handed the tool child a partial family: the count and the values survived, all three
`GIT_CONFIG_KEY_<n>` names were withheld, and every git command in the session died with `error:
missing config key GIT_CONFIG_KEY_0` / `fatal: unable to parse command-line config`. Lunate's own
post-bash change scan failed the same way, and the workaround available to a model is to unset every
`GIT_CONFIG_*`, which drops the supervisor's git hardening as well (#871).

## Decision

1. **The shape rule gains exactly one name-family exemption.** `GIT_CONFIG_COUNT`,
   `GIT_CONFIG_KEY_<n>` and `GIT_CONFIG_VALUE_<n>` (`<n>` all digits) are not withheld by the
   credential-shape rule; they reach the tool child whole. The family is matched
   case-insensitively, like the shape rule itself.

2. **Exempt, not withheld-together.** "Together or not at all" is the requirement; keeping every
   member is the only reading that repairs the break, because git refuses the run when a member is
   missing. Dropping all seven would leave git as broken as the partial set, only without the
   misleading error.

3. **The family is fixed in the builder, not configurable.** It is one predicate beside the word
   list in `EnvironmentScrubber`, the one place that decides a child's environment (ADR-0036 decision
   5). It is not a setting, a wildcard, a regex or a wider pass-through list, and
   `GIT_CONFIG_PARAMETERS`, `GIT_CONFIG_GLOBAL`, `GIT_CONFIG_SYSTEM` and `GIT_CONFIG_NOSYSTEM` are
   deliberately not included: they match no credential word today and this exemption stays as narrow
   as the break it repairs.

4. **Declared values get the same exemption.** A spawn site that sets `GIT_CONFIG_KEY_<n>` as an
   extra value is not silently half-broken, matching ADR-0036 decision 4's symmetry.

5. **Every tool child shares it, the change scan included.** The post-bash scan's `git` runs through
   the same `ChildProcess.BuildEnvironment` policy; ADR-0036's Consequences promised a test for every
   documented example of the rule, and this family gets one at the builder, one at a real bash child
   and one at the scan.

## Consequences

- **A supervisor's git configuration now works in the tool child.** `git status`, `git commit` and the
  change scan run under the same configuration the session was started with, and `credential.helper`
  is empty rather than a caller's helper.
- **`GIT_CONFIG_VALUE_<n>` may carry a secret and is not inspected.** The rule is a name rule and stays
  one. The value is the caller's own git configuration, and a tool child already inherits `HOME`, so
  the same configuration is readable from the user's git files; no documentation may claim the
  exemption makes git values safe.
- **The next protocol whose names a credential word splits is a new decision, not a wider rule.** A
  second family is added by amending this ADR with its own failure and its own test, the way this one
  amends ADR-0036.
- **The false positive is gone, the heuristic is not.** `GIT_CONFIG_KEY_0` no longer breaks a tool
  child; `GOOGLE_APPLICATION_CREDENTIALS` and `SECRET_SANTA_EXCHANGE` still do, and the pass-through
  list is still the recovery for them.
