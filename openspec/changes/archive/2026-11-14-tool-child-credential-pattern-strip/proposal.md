## Why

The tool child's strip covers only the credential names Lunate itself resolved, so a developer
machine's `GITHUB_TOKEN`, `NPM_TOKEN` or `AWS_SECRET_ACCESS_KEY` reaches a model-chosen `bash`
command untouched — the very leak (#621) the strip exists to close, for every credential Lunate does
not happen to resolve. The pass-through list from #642/#659 is the opt-in that makes a wider default
deny usable, and Codex CLI's shell environment policy is the prior art.

## What Changes

- The `Inherited` policy gains a second default deny beside the resolved-name strip: an inherited
  variable whose **name is shaped like a credential** is withheld from the tool child unless
  `toolEnvironment.passthrough` names it exactly.
- The matching rule is stated precisely: the name is split on `_` and `-`, and any part equal
  (case-insensitively) to one of a fixed list of credential words — `KEY`, `KEYS`, `TOKEN`,
  `TOKENS`, `SECRET`, `SECRETS`, `PASSWORD`, `PASSWORDS`, `PASSWD`, `CREDENTIAL`, `CREDENTIALS`,
  `CREDS` — withholds the variable. Not a substring match: `MONKEY`, `KEYBOARD_LAYOUT`,
  `HOCKEY_POOL` and `GITLAB_TOKENIZE_TIMEOUT` survive.
- The pass-through list stays exact-name only: it is how a user admits one variable back, including
  one the shape rule withheld. Matching a listed name never widens to a pattern.
- `Scrubbed` is unchanged — extension hosts never receive the inherited set. The shape rule applies
  under `Inherited` only, to inherited variables and to declared values alike, so no spawn site can
  re-introduce a withheld name by declaring it.
- The boundary sentence in `docs/guide.md` and the `toolEnvironment.passthrough` settings reference
  change with it, and the ADR names the false-positive risk and the failure mode the decision
  prefers.
- No public API change, no new package, no new setting.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `child-environment`: the `Inherited` policy withholds credential-shaped names by default, the
  matching rule is part of the requirement, and the pass-through list is the only way one is
  admitted.

## Impact

- `src/Lunate.Extensibility.Abstractions/EnvironmentScrubber.cs`: the `Inherited` policy applies the
  credential-shape deny (one rule, one builder) before the pass-through re-admission and for
  declared values too.
- `src/Lunate.Coding/CredentialVariables.cs`: its remarks say the strip is a name list; they now
  point at the builder's separate shape deny so the two denies are not confused.
- `docs/guide.md` (the tool child's boundary sentence), `src/Lunate.Coding/settings-schema.json` and
  the generated `docs/settings.md` (`toolEnvironment.passthrough`): the rule and the opt-in.
- `adr/0036-...`: the durable decision, amending ADR-0035.
- Tests: `EnvironmentScrubberTests` (the rule, its survivors and its false positives),
  `ChildProcessToolEnvironmentTests` (a real POSIX/Windows child: a shaped name does not reach it, a
  listed one does).
