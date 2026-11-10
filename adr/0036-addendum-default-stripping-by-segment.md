# ADR-0036 (addendum): The tool child strips credential-shaped name segments by default

Status: accepted — 2026-11-10
Amends: ADR-0034 (tool environment pass-through) — the inherited policy's strip is no longer the
known-name list alone, so a variable whose name carries a credential-shaped segment is removed even
though Lunate never resolved it; the exact-name pass-through list is the opt-in against that rule
too. The rest of ADR-0034 (exact-name matching, user-scope only, the read site, `Scrubbed`
unaffected) stays in force. ADR-0034's file also carries the number of the child-descriptor record;
it is being renumbered to ADR-0035 in a separate fix, and this record takes the next free number.

## Context

ADR-0034 kept the tool child's credential strip honest by bounding it: Lunate removes exactly the
names it resolved (`CredentialVariables`), and a name it never resolved — `GITHUB_TOKEN`,
`AWS_SECRET_ACCESS_KEY`, `NPM_TOKEN` — reaches a model-chosen command untouched (#741). The strip
was a strip list, not a pattern, and the record said so on purpose.

That bounded strip leaves the common case open: a developer machine's `GITHUB_TOKEN` is exactly the
kind of credential a shell a model drives should not inherit by default, and it is not a name
Lunate ever resolved. Denying by name can only cover names Lunate knows about; a default that
protects the user's secrets has to describe the shape of a secret name, not enumerate one
application's names.

The shape rule has a cost the decision has to name. A substring test (`*KEY*`, `*TOKEN*`) is the
obvious form and it is wrong: it strips `MONKEY`, `KEYBOARD_LAYOUT` and `HOCKEY_POOL` for `KEY`, and
`GITLAB_TOKENIZE_TIMEOUT` for `TOKEN`. A build that breaks because a variable happened to contain a
syllable is a worse failure than the leak it prevents, and it is invisible until something breaks.
The maintainer decided the middle path: match whole underscore-separated segments, so the rule
describes a word in the name, not a run of characters inside one.

## Decision

1. **A variable is stripped when one of its underscore-separated segments is `KEY`, `TOKEN`,
   `SECRET` or `PASSWORD`, compared case-insensitively.** Matching is by whole segment under the
   underscore separator, never by substring: `GITHUB_TOKEN` and `AWS_SECRET_ACCESS_KEY` are
   stripped; `MONKEY` (one segment, `MONKEY`) and `GITLAB_TOKENIZE_TIMEOUT` (`TOKENIZE` is not
   `TOKEN`) survive. A name with no underscore is its own single segment. The known provider names
   and registered `env:` names stay stripped as before.
2. **`toolEnvironment.passthrough` wins over the segment rule exactly as it wins over the
   known-name strip.** The list is still matched by exact name, still user-scope settings only
   (a project or workspace settings file carrying `toolEnvironment` is refused by name), and still
   named in one startup admission notice. An admitted name is an explicit user act.
3. **The rule reaches the child through the one shared builder.** `EnvironmentScrubber.Build`
   gains an optional name predicate in a new overload; the six-parameter signature stays untouched
   and keeps today's behaviour, so an extension compiled against it keeps working unchanged. The
   tool child's policy build (`ChildProcess.BuildEnvironment`) passes the segment rule, so every
   tool call gets it by default and no spawn site decides an environment of its own.
4. **The `Scrubbed` policy is unaffected.** Extension hosts never receive the inherited set; the
   rule, like the strip, is an `Inherited`-only instrument.

### What is now reachable and what is not

- A variable that the strip removes — because Lunate resolved its name, or because one of its
  segments is `KEY`, `TOKEN`, `SECRET` or `PASSWORD` — **is not visible to a model-chosen command's
  child process**. The command cannot read it, print it, or send it anywhere.
- A variable admitted by `toolEnvironment.passthrough` **is visible to that child process, and the
  model can read it** from a command it runs. That is the point of the opt-in, and the startup
  notice names every admitted variable so the user can see what they allowed.
- The strip is a name rule over the inherited environment. It says nothing about a secret a command
  reads from a file, a socket, or a value the user types into the session.

## Consequences

- The default tool child loses `GITHUB_TOKEN`, `AWS_SECRET_ACCESS_KEY`, `NPM_TOKEN`,
  `DATABASE_PASSWORD` and every other credential-shaped name a developer's shell holds, without
  Lunate having to know the application that set it.
- `MONKEY`, `GITLAB_TOKENIZE_TIMEOUT`, `KEYBOARD_LAYOUT` and `HOCKEY_POOL` keep arriving, which is
  the false-positive failure the segment rule exists to avoid.
- A user's own `*_TOKEN` or `*_KEY` variable is absent from a tool child until it is listed in
  `toolEnvironment.passthrough`. That is the new failure mode: a build whose command needs the
  variable breaks visibly, and the guide and configuration reference name the rule and the opt-in.
- The startup collision warning keeps its current trigger (a listed name that is also a provider
  credential). A listed `GITHUB_TOKEN` survives without that warning; the admission notice is the
  signal that names it. Widening the warning is a separate presentation decision.
- `docs/guide.md` and the `toolEnvironment.passthrough` reference row state the rule and the
  consequence; the `child-environment` spec carries the requirement and its scenarios.

## What it does not change

- The pass-through matching (exact name, platform comparison, no prefix/suffix/wildcard), its
  user-scope-only rule, the project/workspace refusal, the collision warning and the startup
  admission notice.
- The `Scrubbed` policy and every extension host's environment.
- The composition of `CredentialVariables.All`: it remains the list of names Lunate resolved, and
  the collision warning still asks that question.
- The existing six-parameter `EnvironmentScrubber.Build` signature and its default behaviour; the
  session format; the redaction of values in session files and diagnostics.
