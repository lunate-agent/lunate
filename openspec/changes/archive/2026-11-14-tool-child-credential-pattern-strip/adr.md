# ADR Review Manifest

- Status: completed
- Review date: 2026-11-14

## Review Summary

ADR review completed for the tool-child credential-shape strip change. One durable decision was
recorded: the tool child's `Inherited` policy withholds credential-shaped names by default — a
word-boundary match against a fixed credential-word list, case-insensitive, on `_` and `-`
separators — and the exact-name, user-scope `toolEnvironment.passthrough` list is the only way to
admit one variable back. Codex CLI's shell environment policy is the prior art named by the issue.

## In-Force ADRs Reviewed

- ADR-0035 (the exact-name pass-through): amended, not superseded. Its pass-through contract, its
  "the list is user scope only, and that is what makes it safe" argument and its read-at-policy-build
  rule are unchanged; its decision 1 left a future pattern feature to a new decision, and this is
  that decision. The prior ADR file is untouched.
- ADR-0024 (the bash guard): unaffected — the guard wraps the child, the environment policy decides
  what the child sees; both boundaries compose.
- ADR-0016 (the workspace boundary) and ADR-0003 (the loop runs tools): context only — this change
  touches neither the file tools nor the loop.
- ADR-0022 (model catalog and provider auth): the resolved-name strip still follows its provider key
  variables and aliases; this change adds a second default deny beside it.

## New Durable ADRs Created

- `adr/0036-tool-child-credential-shape-strip.md` — the pattern-strip default, the precise matching
  rule and its word list, the exact-name pass-through as the opt-in, and the failure mode the
  decision prefers (a recoverable missing variable over an unrecoverable leak).
