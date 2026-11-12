# Tasks — changelog-fragments

## 1. Fragments and their rule

- [x] 1.1 `changes/README.md`: the five-line rule, the accepted headings and the file name.
- [x] 1.2 `changes/813.md`: this change's own fragment under `### Added`.
- [x] 1.3 `.markdownlint-cli2.yaml`: lint `changes/**/*.md` so a fragment is held to the changelog's
      own Markdown rules.

## 2. The assembler

- [x] 2.1 `scripts/changelog-assemble`: stdlib Python, `--check` and `--write`, fragment validation
      (name, heading, bullets, duplicate number, text outside the heading), per-heading marker
      regions, number order within a heading and the Added/Changed/Fixed heading order.
- [x] 2.2 `tests/scripts/test_changelog_assemble.py`: the missing fragment, malformed fragment, two
      fragments for one number, unknown heading, number order, idempotence, untouched legacy content
      and untouched released history.
- [x] 2.3 Prove the assembler on a copy of the tree: `--check` fails naming the missing fragment,
      `--write` inserts the marker region and passes, and a second `--write` changes nothing. The
      repository's `CHANGELOG.md` is left unedited so this pull request ships fragment-only.

## 3. The gate

- [x] 3.1 `scripts/changelog-guard`: paths-file decision, `release/<version>` and `chore(release)`
      exemptions, a message naming `changes/<number>.md`, and `--self-test`.
- [x] 3.2 `tests/scripts/test_changelog_guard.py`: fragment-only passes, a changelog edit fails and
      names the fragment path, both release signals pass, a rename out of `CHANGELOG.md` is caught.
- [x] 3.3 `.github/workflows/ci.yml`: a `changelog` job on every non-draft pull request, `--check`
      when the diff includes `CHANGELOG.md`; add it to `ci-ok`'s `needs`.
- [x] 3.4 `scripts/ci-ok.sh`: take the job's result, fail closed on a pull request, accept a skip
      only outside a pull-request event, plus self-test cases.
- [x] 3.5 `scripts/ci-plan.sh`: `changes/*.md` selects the docs profile, with a self-test case.
- [x] 3.6 `.github/workflows/release.yml`: `scripts/changelog-assemble --check` in the gate job.

## 4. Documentation

- [x] 4.1 `CONTRIBUTING.md`: the fragment rule in the pull-request section and the docs profile note.
- [x] 4.2 `docs/distribution.md`: the assembly step in the release runbook, including consuming the
      fragments.
- [x] 4.3 `docs/README.md`: the fragment rule in the map or the guideline, with one home.
- [x] 4.4 `design.md` carries the exact proposed `AGENTS.md` bullet; `AGENTS.md` itself is unchanged.

## 5. Verification and bookkeeping

- [x] 5.1 Python suites for both scripts, `scripts/ci-ok.sh --self-test`, `scripts/ci-plan.sh
      --self-test`, `scripts/changelog-assemble --check` on a copied tree, and `openspec validate
      <change> --strict`.
- [x] 5.2 `dotnet csharpier check .` and markdownlint stay green (no C# is touched).
- [x] 5.3 `openspec archive changelog-fragments --yes` as the final commit.
