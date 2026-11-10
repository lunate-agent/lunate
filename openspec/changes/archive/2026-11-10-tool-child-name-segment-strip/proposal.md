## Why

The tool child's inherited environment loses only the credentials Lunate itself resolves, so a
developer's `GITHUB_TOKEN`, `AWS_SECRET_ACCESS_KEY` or `NPM_TOKEN` reaches a model-chosen command
untouched (#741). Denying by name cannot cover names Lunate never met; the maintainer decided the
default becomes a name-shape rule, with the existing exact-name pass-through list as the way back
in.

## What Changes

- The tool child's `Inherited` environment strips a variable when one of its underscore-separated
  segments is `KEY`, `TOKEN`, `SECRET` or `PASSWORD` (case-insensitive), in addition to the
  provider credential names `CredentialVariables` already holds.
- Matching is by segment, never by substring: `MONKEY` and `GITLAB_TOKENIZE_TIMEOUT` survive, and
  the code comment says why a substring test is wrong.
- `toolEnvironment.passthrough` wins over the segment rule exactly as it wins over the known-name
  strip: a listed name survives even when a segment matches. The setting stays user-scope only,
  keeps the project/workspace refusal, and keeps its startup admission notice.
- `EnvironmentScrubber` gains a companion entry point, `BuildWithNameStrip`, so a caller can hand
  the shared builder a name-shape rule; the existing six-parameter `Build` signature and its
  default behaviour stay in place, so an extension compiled against today's signature keeps working
  unchanged. (A new optional parameter on `Build` was the first shape; the repository's public-API
  overload analyzers refuse it beside the existing optional-parameter signature, and keeping that
  signature is the hard compatibility requirement.)
- A new decision record, ADR-0036 (addendum), states what is now reachable and what is not.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `child-environment`: the inherited policy's strip gains the name-segment rule, and the
  pass-through list is stated to win over it as well as over the known-name strip.

## Impact

- `src/Lunate.Coding/CredentialVariables.cs`: the segment rule, its public predicate, and the
  corrected remarks.
- `src/Lunate.Extensibility.Abstractions/EnvironmentScrubber.cs`: an optional strip predicate
  overload beside the existing signature; the pass-through re-admission is unchanged.
- `src/Lunate.Coding/ChildProcess.cs`: the tool child's policy build passes the segment rule.
- `tests/Lunate.Coding.Tests/`: one test per rule example at the real tool-child composition, plus
  the predicate's own cases.
- `tests/Lunate.Extensibility.Tests/EnvironmentScrubberTests.cs`: the predicate parameter and the
  unchanged default.
- `adr/0036-addendum-default-stripping-by-segment.md`, `docs/guide.md`,
  `docs/configuration.md`/`settings-schema.json`: the record and the stated boundary.
