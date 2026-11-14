## 1. AGENTS.md

- [ ] 1.1 Replace the workflow bullet at `AGENTS.md:12` with the lane discipline: one implementation pull request per card (proposed, applied and archived together), the archive as the last commit landing before review, a rework redoing the archive last, and the reviewer reading the proposal delta first.
- [ ] 1.2 State the unattended rule and the ADR exception: nobody to ask, so decide in scope, commit on the branch and put the question in the pull request; an ADR-level decision keeps propose-and-stop. Record the `grill-me` self-answer rule and the **Intent decisions** heading the reviewer checks.
- [ ] 1.3 State the `openspec/specs/**` conflict rule: `git revert` the archive commit, merge `origin/main`, re-archive last — never edit a spec by hand.

## 2. The git-discipline skill

- [ ] 2.1 Rewrite `.agents/skills/openspec-git-discipline/SKILL.md` around the four lane moments, using the same words as `AGENTS.md` for "the archive is the last commit and lands before review" and for a rework redoing it.
- [ ] 2.2 Remove every instruction to ask the user from the skill's gate table, checks and red flags; name the ADR exception explicitly, add the **Intent decisions** rule and the revert-and-re-archive rule to its gates and red flags.

## 3. Contributor page and specs

- [ ] 3.1 Correct `CONTRIBUTING.md`'s OpenSpec workflow section: no proposal waiting on `main`, the archive before review rather than after it.
- [ ] 3.2 Land the `repo-quality` delta spec (archiving scenario, reviewable intent interview, specs only through the CLI) and run `openspec validate align-lane-workflow-discipline --type change --strict`.

## 4. Gate and release note

- [ ] 4.1 Run the docs profile of the gate (`bash scripts/verify.sh --profile docs`) and `bash scripts/check-openspec-skills.sh`; fix what they report.
- [ ] 4.2 Write `changes/<pr-number>.md` with the release-note line for this change.
- [ ] 4.3 Run `openspec validate --all --strict`, sync the specs and archive the change as the last commit.
