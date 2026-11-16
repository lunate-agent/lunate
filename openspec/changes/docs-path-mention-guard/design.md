## Context

`scripts/no-docs-references.sh` guards the docs profile's central assumption: nothing under `docs/` is
a build input or a test's subject, so the cheap profile can skip the build and the suite. It has three
shapes, one of them a heuristic: a test file that resolves the repository root (`RepositoryRoot`,
`FindRepositoryRoot`, `lunate.slnx`) and names a docs path is treated as a read. #910's
`TargetFrameworkTests` resolves the root and names `docs/dev/spikes` only in the list of directories its
project scan does not enter, so the heuristic reports a read that never happens, and the docs profile is
red on `main`.

Two facts make that failure survive: the guard runs in the docs profile only (`verify.sh:335`), and
`scripts/ci-plan.sh` sends every code change — and #910 was one — to the full gate, which never ran the
guard. The push to `main` runs `tree-checks.yml` and the full matrix; neither contained it either. This
is the shape `#738` recorded: a check that cannot fail on `main` is invisible exactly where `main` rots.

The c4-diagrams skill was applied: this change adds no container, component or boundary, only one more
step in two existing jobs and one narrower shape in one script, so no diagram is drawn.

## Goals / Non-Goals

**Goals:**

- `scripts/verify.sh --profile docs` is green on `main` without weakening the rule that no test reads
  the docs tree.
- A test that names a docs path without reading it can stay: the test's exclusion keeps naming the
  record location the repository documents (`AGENTS.md`, `docs/dev/README.md`) instead of inventing a
  second spelling of "a spike is data".
- The guard runs wherever a docs read can land: on a code pull request, in the merge queue and on a
  push to `main` — and still in the docs profile.
- Every allowance the guard applies is printed, so an exception is a reviewed line, not a silent pass.

**Non-Goals:**

- No change to `TargetFrameworkTests`' scan or to what it asserts; the test stays the subject, not the
  fix.
- No second parser: the guard stays a `grep`-shaped heuristic over test sources, not a C# analyzer.
- No change to the job set, the matrix sizing, the aggregate gate or the docs profile's step list.
- No new dependency, no product code, no public API.

## Decisions

**1. Teach the guard, not the test.** The alternatives were to spell the exclusion as a bare segment
name (`spikes`, `tasks`) or to move the literal into a fixture or helper file. The first silently drops
any fixture project that happens to sit in a directory of that name; the second hides the literal from
the check instead of declaring it. Keeping the documented path in the test and naming the exception in
the guard is the only option that neither weakens the scan nor hides the reference.

**2. The allowance is per file and per exact reference.** A new `ALLOWED_DOCS_MENTIONS` list holds
`<test file>|<reference>`. The guard prints one `note:` line per applied allowance — the same
convention as the reader list and the fixture path notes — and then continues with the normal checks:
a docs path in that file that is not the named reference, and a `File`/`Directory` call on the allowed
line, still fail. The reader list (`ALLOWED_DOCS_READERS`) stays a whole-file allowance because those
two tests exist to read pages; a mention is narrower because the file is not a reader.

**3. Run it where a docs read can land.** The guard joins `platform_checks` in `scripts/verify.sh`
(the `platform` job on a full-profile pull request, and every local `--profile full`) and
`scripts/tree-checks.sh` (every push to `main` and the merge queue's `tree-checks` job). It costs about
1.3 s, needs no build and no test — the tree checks' cheapness contract holds. The docs profile keeps
its own step. `tests/scripts/test_tree_checks.py` pins the shared command list so the two triggers
cannot drift from it, and `tests/scripts/test_no_docs_references.py` pins all three placements — the
docs profile, the platform checks and the tree checks — because a step that quietly disappears is
exactly the #929 failure mode.

**4. The spec delta names the guard for the first time.** The guard was introduced by an archived change
that only described it inside the matrix requirement's prose; this change adds a requirement of its own
(the three shapes, the printed allowances, the vacuity failure) and updates the matrix requirement's
platform-check and tree-check lists.

## Risks / Trade-offs

- The guard stays a heuristic: a test in the allowed file could put the allowed string in a variable and
  read that variable later. The allowance is one file and one literal, printed on every run, and a read
  on the allowed line fails; a C# analyzer is out of proportion for a docs-profile guard.
- The full gate pays ~1.3 s in one job; the tree checks pay the same on `main` and in the queue. Both
  are inside the seconds-scale budget those jobs already keep.
- A future test that legitimately names a docs path needs the same two-line allowance. That is the
  intended friction: the exception is reviewed instead of assumed.
- The `platform` job grows a step, so a red `platform` can now be the guard. Its message names the file
  and line, so the cause is not hidden behind the job name.

## Migration Plan

None. The guard, the profiles and the workflows are repository tooling; nothing ships to users and no
state moves. The change lands as one pull request; a revert restores the previous step lists.

## Open Questions

None. ADR-0037 ("the CI gate is sized by the event") already says a pull request runs the platform/docs
checks and a push to `main` and the merge queue run the tree checks; this change fills those lists, so
no ADR needs supersession.
