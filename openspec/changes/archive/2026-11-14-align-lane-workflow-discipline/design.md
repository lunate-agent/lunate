## Context

The lane opens one pull request per card, pushes the worker's first commit while the worker is still
writing, and hands the finished branch to a reviewer. That shape is already enforced by the repository's
own guards: `scripts/archive-only-check.sh` refuses a pull request that only archives, with the message
that "the archive belongs as the final commit of the change's own pull request, so the reviewer sees the
implementation and the bookkeeping together", and `scripts/check-spec-archive.py` fails a change whose
delta specs were archived away without the spec files arriving with them. `CONTRIBUTING.md` repeats the
same expectation ("the *final commit of the implementation pull request*").

Two documents were not updated for the lane:

- `AGENTS.md:12` — "proposals land on `main` before apply; the archive is the final commit of the
  implementation PR, added after review and before merge."
- `.agents/skills/openspec-git-discipline/SKILL.md` — archive from `main` only, after review; a table of
  moments that each end in "ask the user"; "Never create commits, branches, or merges unless the user
  explicitly asks."

`openspec/specs/repo-quality/spec.md` pins the wrong half of this too: its archiving scenario reads
"When a change's implementation is reviewed and the fast gate is green", which is the after-review order
the lane replaced. Since the specs are the source of truth, the documents cannot be corrected alone — the
spec delta has to move first, in this change.

`openspec/config.yaml` requires `grill-me` for a proposal. `grill-me` interviews the user, and an
unattended worker has nobody to interview; without a written rule the worker either stalls or silently
answers itself, and a silent self-answer is an intent nobody can review.

## Goals / Non-Goals

**Goals:**

- One discipline, stated in `AGENTS.md` and in the `openspec-git-discipline` skill, worded identically
  where the two must not drift (archive is last and lands before review; a rework redoes it).
- A worker that reads only those two documents produces the pull request the lane expects: proposal
  reviewed first, one pull request, archive as the last commit, no question waiting on a user.
- The intent interview becomes a reviewable artifact (**Intent decisions**) instead of a silent decision.
- A spec conflict has one written resolution (revert the archive, merge, re-archive) and no hand edit.
- `openspec/specs/repo-quality/spec.md` says the same thing, so the documents and the source of truth
  agree rather than the documents being right on their own.

**Non-Goals:**

- Changing any guard: `scripts/archive-only-check.sh`, `scripts/check-spec-archive.py` and the CI
  profiles already assume this shape.
- Editing the generated lifecycle skills (`.agents/skills/openspec-*`): they are `openspec update`
  output, and `scripts/check-openspec-skills.sh` fails on a hand edit.
- Editing the lane's own reviewer prompt. It lives outside this repository; the reviewer reads
  `AGENTS.md` and the skill as this repository's brief, and those are what this change corrects.
- Rewriting `docs/dev/guide.md`. `openspec/config.yaml` names it background material, and its working
  loop describes an attended OpenCode session; a rewrite is a separate card.
- Any product, source, test, workflow or public-API change.

## Decisions

### The two load-bearing sentences are identical in both documents

`AGENTS.md` and the skill carry the same wording for the archive ("the archive is the last commit of the
implementation pull request, and it lands before review, so the reviewer reads a tree whose change is
already archived and its delta specs already synced") and for a rework ("a rework that a review requests
redoes the archive, or its update, as the last commit again"). Single-sentence paraphrases drift and the
card is about a drift; identical wording is the cheapest way to make a future divergence visible in a
diff. The spec file quotes the two facts, not the sentence, so the spec states behaviour while the
documents own the phrasing.

Alternative: one document owns the wording and the other links to it. Rejected — an unattended worker may
load only one of the two (a propose/apply run loads the skill; a src task loads `AGENTS.md`), and a link
that is not followed is not a rule.

### The skill becomes lane-shaped instead of user-shaped

The skill's gate table, its per-moment "ask the user" language and its "Required Checks" (verify the
proposal exists on `main`, stop if not on `main`) all describe an attended session. It is rewritten
around the four lane moments — propose inside the card's pull request, apply, verify, archive last — and
its red flags become the failures a lane run actually produces: a second pull request for the proposal, an
archive added after review, a spec edited by hand, a run that stopped to ask.

### The ADR exception is named in both documents, next to the rule it excepts

`AGENTS.md`'s Rules section already says architecture decisions "require maintainer sign-off: propose and
stop". The lane rule ("decide within scope, put the question in the pull request") sits beside it, and
both documents name the ADR case as the single exception, so the two rules cannot read as contradictory.

### CONTRIBUTING.md is corrected in the same change

`CONTRIBUTING.md`'s OpenSpec workflow section repeats both superseded facts (the proposal lands on `main`
before implementation; the archive is added after review). It is the contributor-facing page for the same
discipline and it would otherwise contradict the spec this change writes, so its two sentences change with
this change. Nothing else in it is touched.

### The intent interview is recorded in the pull request, not in the change folder

The self-answer list goes in the pull request description under **Intent decisions**, because that is what
the reviewer reads first and what a rework edits. Putting it in `openspec/changes/<change>/` would make it
a second, unvalidated artifact that the archive then moves out of reach.

## Risks / Trade-offs

- **The skill is hand-written, the rest of `.agents/skills/openspec-*` is generated** → an
  `openspec update` cannot overwrite it (`openspec-git-discipline` is a companion skill listed in
  `openspec/schemas/intent-driven/skills.txt`, not a rendered workflow), and
  `scripts/check-openspec-skills.sh` proves it by regenerating the tree and diffing.
- **Identical wording is a maintenance cost** → it is two sentences in two files; a change to the rule
  must touch both, which is exactly the review signal wanted.
- **No test can assert the wording** → the behaviour this change fixes is a document's instruction, and no
  test reads root `AGENTS.md` content (`scripts/docs_check/constants.py` only lists it as a linted root
  document). The pins are the docs gate: markdownlint, `openspec validate --all --strict` and
  `scripts/docs-check --check`.
- **A reviewer's prompt may still say "review the code first"** → outside this repository; the in-repo
  documents now say otherwise, and the card's review checks that.

## Migration Plan

Documentation and one spec delta; no deployment, no rollback beyond reverting the commit. A worker picks
the new rules up by reading the two documents it already loads.

## Open Questions

None. The one open decision the card records — that one pull request per card stays — is restated here
rather than decided.
