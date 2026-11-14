## Why

The repository is now driven by a lane that opens one implementation pull request per card and reviews
that pull request — proposal, implementation and archive together — but two of its written documents
still describe the previous, interactive flow: `AGENTS.md:12` says proposals land on `main` before apply
and that the archive is "added after review and before merge", and
`.agents/skills/openspec-git-discipline` says archive runs only after review, only from `main`, and
repeatedly instructs the agent to ask the user. A worker that follows those two documents produces the
opposite of what the lane expects: a proposal waiting in a second pull request, an archive missing from
the reviewed tree, and a run that stalls on a question nobody is there to answer.

## What Changes

- `AGENTS.md` and `.agents/skills/openspec-git-discipline/SKILL.md` state the same discipline, in the
  same words for the two points that must not drift:
  - one implementation pull request per card — proposed, applied and archived together, not in
    separate pull requests;
  - the archive is the last commit, and it lands **before review**, so the reviewer reads a tree whose
    change is already archived and its deltas synced;
  - a rework requested by a review redoes the archive (or its update) as the last commit again;
  - the reviewer reads the proposal delta under `openspec/changes/<change>/` first, so a mismatch
    between intent and code is a review finding rather than a merge-order accident.
- Neither document instructs an unattended worker to ask the user. A worker with nobody to answer
  decides inside the card's scope, commits on its branch and puts the question in the pull request for
  the reviewer. **The one exception, named in both documents, is an ADR-level decision**: it keeps
  "propose and stop" until Daniel signs it off.
- Because `openspec/config.yaml` requires `grill-me` for a proposal and `grill-me` interviews the user,
  an unattended worker runs it in self-answer mode and lists every question with the answer it chose
  under a heading **Intent decisions** in the pull request description. The reviewer checks that list;
  a missing list is a send-back.
- A conflict under `openspec/specs/**` is never resolved by editing a spec by hand: `git revert` the
  archive commit, merge `origin/main`, re-run `openspec archive <change> --yes` as the last commit
  again. Both documents state the rule.
- `CONTRIBUTING.md`'s OpenSpec workflow section stops contradicting the same two facts (the proposal
  waiting on `main`, the archive after review).
- `openspec/specs/repo-quality/spec.md` records the discipline: the gate requirement's archiving
  scenario, plus requirements for the reviewable intent interview and for specs that only the CLI writes.

Not in this change: the generated lifecycle skills (`.agents/skills/openspec-*` are `openspec update`
output and `scripts/check-openspec-skills.sh` rejects a hand edit), the lane's own reviewer prompt
(outside this repository), and `docs/dev/guide.md` (background material, per `openspec/config.yaml`).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `repo-quality`: the archiving scenario changes from "archive after review" to "archive is the last
  commit, before review"; the reviewable intent interview (`grill-me` self-answer mode, the **Intent
  decisions** heading, the reviewer's send-back) and the revert-and-re-archive rule for
  `openspec/specs/**` conflicts become requirements; the documents that must state them are named.

## Impact

`AGENTS.md`, `.agents/skills/openspec-git-discipline/SKILL.md`, `CONTRIBUTING.md`,
`openspec/specs/repo-quality/spec.md`, `changes/<pr>.md`.
No source, test, workflow, public API, package or store-format change; the guards that already assume
this shape (`scripts/archive-only-check.sh`, `scripts/check-spec-archive.py`) are unchanged.
