## 1. The decision script

- [x] 1.1 `scripts/pre-ready.py`: stdlib Python; git inputs are files when supplied, otherwise the
      `git diff`/`git show`/`git ls-tree` command; pull-request inputs are `--pr-json`,
      `--comments-json` and `--issues-dir`.
- [x] 1.2 Check 1: unarchived change directories, the `HEAD` tree, the `HEAD` commit's delete or
      rename out of the change, and the archive directory's `<YYYY-MM-DD>-<name>` shape.
- [x] 1.3 Check 2: the release signals, the inert-until-`changes/README.md` rule, a direct
      `CHANGELOG.md` edit, and `changes/<pr-number>.md` in the diff and at `HEAD`.
- [x] 1.4 Check 3: a spec change requires an archive path in the same diff, matching
      `scripts/check-spec-archive.py`.
- [x] 1.5 Check 4: linked issue numbers from title and body, the `Acceptance` list, answer blocks
      from the body and comments (last answer wins), `met`/`not met`, and `file:line` or test-name
      evidence.
- [x] 1.6 Output: one `ok` line per check; every failure names the check, the path or issue and the
      action, and all findings are printed before the exit.

## 2. Tests

- [x] 2.1 `tests/scripts/test_pre_ready.py`: a pull request that satisfies all four passes.
- [x] 2.2 The four real findings fail: an unarchived change, a missing fragment, a hand-edited
      spec, an unanswered acceptance check.
- [x] 2.3 The archive-not-last-commit case, the changelog edit, a `not met` answer and a `met`
      answer without evidence each fail by name.
- [x] 2.4 The release exemption, the inert fragment rule, answers in a comment, and the
      `check-spec-archive.py` agreement are covered.

## 3. The workflow

- [x] 3.1 `.github/workflows/pre-ready.yml`: the `pre-ready` job on `opened`, `synchronize`,
      `reopened`, `ready_for_review` and `edited`, checking out the pull request head with full
      history.
- [x] 3.2 The job reads the pull request, its comments and its linked issues through `gh api` with
      `contents: read`, `pull-requests: read` and `issues: read`; a failed issue read is named by the
      script rather than a bare `gh` exit.
- [x] 3.3 The job is not added to the ruleset and not added to `ci-ok`'s `needs`, so it stays
      advisory until a maintainer enables it.

## 4. Wiring and documentation

- [x] 4.1 `scripts/verify.sh`: run `tests/scripts/test_pre_ready.py` in the docs profile and the
      full profile's scripts step.
- [x] 4.2 `CONTRIBUTING.md`: the job, the four checks, the answer shape, and the advisory phase.
- [x] 4.3 `changes/<pr-number>.md`: this change's release-note line, under `### Added`.

## 5. Verification and bookkeeping

- [x] 5.1 `python3 tests/scripts/test_pre_ready.py` green, including the four real findings.
- [x] 5.2 `dotnet csharpier check .`, markdownlint and `openspec validate --all --strict` green.
- [x] 5.3 `openspec archive pre-ready-gate --yes` as the final commit of this pull request.
