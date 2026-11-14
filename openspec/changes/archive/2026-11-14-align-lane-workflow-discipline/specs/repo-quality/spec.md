## ADDED Requirements

### Requirement: The intent interview is reviewable

`openspec/config.yaml` requires the `grill-me` skill for a proposal, and that skill interviews a user. An
unattended worker has nobody to interview, so it SHALL run the interview in self-answer mode: it answers
every question the interview would ask from the card, the specs and the code, and lists every question
with the answer it chose under a heading **Intent decisions** in its pull request description. The
reviewer SHALL check that list first and SHALL send the pull request back when it is missing. Neither
`AGENTS.md` nor `.agents/skills/openspec-git-discipline/SKILL.md` SHALL instruct an unattended worker to
wait for a user's answer: it SHALL decide within the card's scope, commit on its branch, and put the
question in the pull request for the reviewer. The one exception SHALL be an architecture decision, which
keeps propose-and-stop until the maintainer signs it off.

#### Scenario: Unattended intent is reviewable

- **GIVEN** an unattended worker implementing a card
- **WHEN** it runs the proposal's `grill-me` interview
- **THEN** it answers each question itself instead of stalling, and the pull request description carries
  its answers under **Intent decisions** for the reviewer to check

#### Scenario: A missing list is a send-back

- **WHEN** a pull request's description carries no **Intent decisions** heading
- **THEN** the reviewer sends it back, because an unanswered interview leaves the intent unreviewed

#### Scenario: An architecture decision still waits

- **WHEN** the card needs an ADR-level decision
- **THEN** the worker proposes it and stops for the maintainer's sign-off instead of deciding, and the
  open question is named in the pull request

### Requirement: Specs change only through the CLI

A conflict under `openspec/specs/**` SHALL be resolved by `git revert` of the archive commit, a merge of
`origin/main` and a re-run of `openspec archive <change> --yes` as the last commit again. A file under
`openspec/specs/**` SHALL NOT be edited by hand to resolve a conflict, because a hand-edited spec is
indistinguishable from a synced one until someone reads the archive. `AGENTS.md` and
`.agents/skills/openspec-git-discipline/SKILL.md` SHALL state both rules.

#### Scenario: A spec conflict is reverted, not edited

- **GIVEN** an archive commit that conflicts under `openspec/specs/**`
- **WHEN** the worker resolves it
- **THEN** it reverts the archive commit, merges `origin/main` and re-archives as the last commit, and
  the spec files it commits are the CLI's output

#### Scenario: The documents refuse a hand edit

- **WHEN** a reader looks for how to resolve a spec conflict
- **THEN** both `AGENTS.md` and the `openspec-git-discipline` skill say to revert and re-archive, and
  neither suggests editing a spec by hand

## MODIFIED Requirements

### Requirement: The gate has two stages

The repository SHALL define two gates in `AGENTS.md`: a fast gate for iteration (`dotnet csharpier check .`, `openspec validate <change> --strict`, a Release build and the test classes covering the change) and the full gate (`scripts/verify.sh`, `scripts/verify.ps1` on Windows). The fast gate SHALL be the condition for committing and pushing during a change; the full gate SHALL be the condition for marking a change ready for review and for calling it done; `ci-ok` SHALL be green before a merge. The full gate's contents SHALL NOT change with this definition. A card's implementation pull request SHALL carry its proposal, its implementation and its archive together; `openspec archive <change> --yes` SHALL be its final commit and SHALL land before review, so the reviewer reads a tree whose change is already archived and its delta specs already synced. A rework that a review requests SHALL redo the archive, or its update, as the last commit again. The reviewer SHALL read the proposal delta under `openspec/changes/<change>/` before the implementation, so a mismatch between intent and code is a review finding rather than a merge-order accident.

#### Scenario: An intermediate commit

- **WHEN** an agent has a compiling change with its focused tests green and CSharpier clean
- **THEN** it may commit and push without running the full gate

#### Scenario: Asking for review

- **WHEN** a change is proposed for review
- **THEN** `scripts/verify.sh` has passed on the head being reviewed

#### Scenario: Archiving

- **WHEN** a change's implementation and the fast gate are green on the change's branch
- **THEN** `openspec archive <change> --yes` is the last commit, before review, so the pull request the
  reviewer reads already carries the archived change and its synced specs

#### Scenario: A review requests a rework

- **WHEN** a review asks for changes
- **THEN** the rework ends with the archive, or its update, as the last commit again, and the pull
  request that merges carries that commit last

#### Scenario: Merging

- **WHEN** a pull request is merged
- **THEN** the `ci-ok` check is green
