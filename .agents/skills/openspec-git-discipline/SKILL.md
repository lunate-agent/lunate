---
name: openspec-git-discipline
description: Use when running OpenSpec propose, apply, verify, sync or archive workflows in this repository, where one implementation pull request per card, archive timing, reworks and spec conflicts decide what the reviewer sees.
license: MIT
compatibility: Requires git and OpenSpec workflow artifacts.
---

# OpenSpec Git Discipline

## Core Rule

A card is **one implementation pull request**: proposal, apply and archive land in it together, never in
separate pull requests and never as a follow-up archive PR.

- **The archive is the last commit, and it lands before review**, so the reviewer reads a tree whose
  change is already archived and its delta specs already synced.
- **A rework that a review requests redoes the archive**, or its update, as the last commit again.
- **The reviewer reads the proposal delta** under `openspec/changes/<change>/` first, before the
  implementation, so a mismatch between intent and code is a review finding, not a merge-order accident.
- **Never rewrite a commit.** The lane pushes your first commit while you work, so no commit is private:
  no `git commit --amend`, no `git rebase`, no `git reset` to an earlier commit, no squash. Fix a mistake
  with a new commit.

## The Four Moments

| Moment | What happens |
| --- | --- |
| Propose | `openspec new change` and the proposal artifacts, on the card's branch and in the card's pull request. There is no separate proposal pull request and no wait for `main`. |
| Apply | Implement `tasks.md` on that same branch, test-first, ticking tasks as they land; commit as you go. |
| Verify | `openspec-verify-change`, the fast gate green, then `openspec validate <change> --type change --strict`. |
| Archive | Sync the delta specs (the `openspec-sync-specs` workflow), then `openspec archive <change> --yes` as the **last** commit, **before** the pull request is handed to review. Nothing follows it: not a formatting run, not a test fix. |

## Unattended Runs

Nobody answers an unattended run. Decide inside the card's scope, commit on your branch, and put the
question in the pull request for the reviewer instead of stopping to ask.

- **Do not ask the user.** Neither this skill nor `AGENTS.md` asks a worker to wait for an answer.
- **`grill-me` runs in self-answer mode.** `openspec/config.yaml` requires it for a proposal and it
  interviews a user; unattended, answer each question from the card, the specs and the code, and list
  every question with the answer you chose under the heading **Intent decisions** in the pull request
  description. The reviewer checks that list first; a missing list is a send-back.
- **The one exception is an architecture decision.** An ADR-level decision (the loop, tool contracts,
  protocols, dependencies) keeps "propose and stop": propose it and wait for the maintainer's sign-off,
  and name the open question in the pull request.

## Specs Are The CLI's Output

`openspec/specs/**` is written by the `openspec-sync-specs` workflow and `openspec archive`, never by hand.

- **A conflict under `openspec/specs/**` is never resolved by editing a spec.** `git revert` the archive
  commit, merge `origin/main`, then re-run `openspec archive <change> --yes` as the last commit again.
- A hand-edited spec is indistinguishable from a synced one until someone reads the archive — which is
  the state this repository has spent the week removing.

## Required Checks

Before archive:

1. `git status --short` — the implementation is committed, nothing is left uncommitted.
2. The fast gate is green on this branch, and `openspec validate <change> --type change --strict` passes.
3. The pull request description carries the **Intent decisions** list.

After archive:

1. The archive commit is the branch's last commit.
2. `openspec validate --all --strict` is green.
3. The pull request is ready: `scripts/verify.sh` green on the head, the archive last, the intent list
   present.

## Red Flags

- A second pull request carrying the proposal, or a proposal waiting for `main` before apply.
- An archive commit added after review, or an archive-only pull request.
- A rework that leaves the archive behind an ordinary commit.
- A spec file under `openspec/specs/**` edited by hand to resolve a conflict.
- A run that stopped to ask a user, or a pull request description without an **Intent decisions** list.
- A pushed commit rewritten (`--amend`, `rebase`, `reset`, squash).

Any of these means: stop, fix the git state with a new commit, and say what happened in the pull request.
