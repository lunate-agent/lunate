# ADR-0036: The tool child withholds credential-shaped names by default, and the pass-through list admits one by name

Status: accepted — 2026-11-14

Amends: ADR-0035 (the exact-name pass-through), whose decision 1 left a future pattern feature to a
new decision; this is that decision. ADR-0035's pass-through contract, its user-scope argument and
its read-at-policy-build rule are unchanged.

## Context

The tool child (`bash`) runs under the `Inherited` environment policy: the user's whole environment
minus every name Lunate resolves as a provider credential — each built-in preset's key variable and
alias, plus each configured provider's `env:` reference (#621, ADR-0035). That deny is exact and
deliberately narrow. A credential Lunate never resolved — a developer machine's `GITHUB_TOKEN`,
`NPM_TOKEN`, `AWS_SECRET_ACCESS_KEY`, `SSH_ASKPASS` — is inherited by every command the model
chooses and the user approves. The approval panel is a skim, `--yolo` exists, and print/ACP mode
approve by policy rather than by reading the command, so the leak is the one the strip exists to
close, for every credential Lunate does not happen to hold.

Codex CLI's shell environment policy denies by name shape by default (`*KEY*`, `*SECRET*`,
`*TOKEN*`) and offers `include_only`/`set` to opt an individual variable back in. Lunate now has the
opt-in half of that model (#642, #659): `toolEnvironment.passthrough`, exact names, user scope only.
The missing question is the default half.

## Considered Options

- **Keep the known-names-only strip.** Cheapest and already true; it protects only the keys Lunate
  resolved, and no user has been surprised by it. Rejected as the default because it leaves every
  ambient credential exposed, and the pass-through list cannot help — there is nothing to opt out
  of.
- **Codex's rule: strip every name containing `KEY`, `SECRET` or `TOKEN`, case-insensitively.** The
  closest prior art and the widest gain. Rejected because a substring match is a guess about the
  user's naming: it withholds `MONKEY`, `KEYBOARD_LAYOUT`, `HOCKEY_POOL` and
  `GITLAB_TOKENIZE_TIMEOUT`, and a variable silently missing from a tool child is a build failure
  with no error message.
- **Word-boundary match against a fixed credential-word list.** Chosen. It keeps the gain where
  credentials actually live (`SCREAMING_SNAKE_CASE` puts a credential word in its own `_`-separated
  part) and drops the four false positives the issue names.
- **Word match plus `URL`/`URI`/`DSN` words, to catch `DATABASE_URL`.** Rejected: a URL's name says
  nothing about whether it embeds a password, and the same rule would withhold `OPENAI_BASE_URL` and
  `NEXT_PUBLIC_*_URL`, which toolchains legitimately read. `DATABASE_URL` therefore stays inherited,
  and this ADR says so rather than pretending the rule is complete.
- **Report every withheld name to the user (a startup line or a per-command note).** Rejected for
  this decision: at startup nothing knows what the parent will hold at spawn time, and a per-command
  note is noise on every bash call and text the model reads. The rule and its word list are
  documented instead; the passthrough entry is the recovery path. A follow-up can add reporting.

## Decision

1. **The default is a pattern strip.** `Inherited` withholds an inherited variable when the *name* is
   credential-shaped, whether or not Lunate resolves it. `Scrubbed` is untouched: extension hosts
   never receive the inherited set, so a credential-shaped name reaches one only when that host's
   own declared names ask for it.

2. **The matching rule is a word match, not a substring and not a glob.** The name is split on `_`
   and `-`; the variable is withheld when any part equals one of `KEY`, `KEYS`, `TOKEN`, `TOKENS`,
   `SECRET`, `SECRETS`, `PASSWORD`, `PASSWORDS`, `PASSWD`, `PASS`, `CREDENTIAL`, `CREDENTIALS` or
   `CREDS`, compared case-insensitively on every platform. `MONKEY`, `KEYBOARD_LAYOUT`,
   `HOCKEY_POOL`, `GITLAB_TOKENIZE_TIMEOUT` and `OPENAI_BASE_URL` survive. `AUTH`, `PWD`, `URL`,
   `URI` and `DSN` are deliberately not words: `SSH_AUTH_SOCK`, `PWD`, `OPENAI_BASE_URL` and
   `NEXT_PUBLIC_*_URL` are ambient names toolchains read.

3. **A user admits an individual variable by exact name, as before.** One entry in the user-scope
   `toolEnvironment.passthrough` list — `{"toolEnvironment": {"passthrough": ["GITHUB_TOKEN"]}}` —
   re-admits that name from the parent after both denies. The list stays exact-name only and user
   scope only: a cloned repository still cannot widen a tool child's environment, and a wildcard in
   the list is still a name that matches nothing. The admitted names are listed at session start, and
   one warning fires when a listed name is also one of Lunate's resolved credential names. A listed
   name that merely matches the shape rule is admitted without that warning: the shape rule is a
   default, not a claim that Lunate resolves the name.

4. **The shape deny is part of the strip for declared values too.** A name the rule withholds cannot
   be re-introduced through `ExtraEnvironment` under `Inherited`, just as a resolved credential name
   cannot; a spawn site never overrides the policy.

5. **The rule is one fact in the one builder.** `EnvironmentScrubber` owns it, beside the `Scrubbed`
   base set and the deny ordering, so no spawn site and no second file decides a child's environment.
   `CredentialVariables` stays what it is: the exact names Lunate resolved.

## Consequences

- **The failure mode this prefers: a missing variable over an exfiltrated secret.** A false positive
  removes a variable the user can restore with one exact name in the settings file, and the next
  start names it; a false negative leaks a token that cannot be un-leaked. The cost is that a build
  whose environment happens to contain `SECRET_SANTA_EXCHANGE`, `GOOGLE_APPLICATION_CREDENTIALS` (a
  path, not a secret) or `GIT_CONFIG_KEY_0` breaks in a way that looks like a broken toolchain until
  the user finds this rule. That is why the word list and the punctuation of the rule are in the
  guide and the settings reference, not only here.
- **The rule is a shape heuristic, not a guarantee.** `DATABASE_URL` with a password in it,
  `PGPASSWORD`, `DOCKER_AUTH_CONFIG` and `SLACK_WEBHOOK_URL` still reach a tool child. No
  documentation may claim the tool child sees no credentials; the container boundary and the
  approval policy remain the layers that answer the rest.
- **Case folding is safer than faithful.** On POSIX `github_token` is withheld too. Nothing legitimate
  breaks by that, and the alternative — withholding a credential only when it is spelled in capitals
  — is a policy a user cannot predict.
- Every documented example of the rule is worth a test: the survivors, the withheld names, the
  case-folded spelling, the declared-value refusal, and a real POSIX and Windows child that does not
  read a withheld name and does read a listed one.
- The pass-through list becomes load-bearing for more variables than before, which is the reason
  #659 made its enforcement and its spawn proof structural first.
