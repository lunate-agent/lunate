# Tasks — documentation-check-contract

## 1. The delta specs

- [x] 1.1 `specs/documentation/spec.md`: one added requirement, "Documentation links, anchors,
      redirects and navigation", covering the manifest shape, the navigation rules, link and anchor
      resolution, redirect stubs versus maintainer renames, the runnable markers and `proseOnly`,
      `--run`, the self-test and the gate wiring, with one scenario per failure class. It creates the
      `documentation` capability; `repo-quality` is left untouched because #820 rescopes that spec.
- [x] 1.2 `specs/ci-pipeline/spec.md`: the per-commit verification matrix copied whole from
      `openspec/specs/ci-pipeline/spec.md`, with `scripts/docs-check --check` named in the
      OS-independent set and in the documentation-only push sentence, plus the scenario "A broken
      documentation link cannot land". No other clause of that requirement is changed.

## 2. Every recorded clause against the code it describes

- [x] 2.1 Manifest and navigation: `scripts/docs_check/checks.py` (navigation paths exist, once, and
      are user-path pages; user-path pages appear in `navigation`), `scripts/docs_check/manifest.py`
      (`flatten_navigation`), `scripts/docs_check/paths.py` (maintainer and record sets,
      `normalize_docs_path`), `scripts/docs_check/constants.py` (the manifest path).
- [x] 2.2 Links and anchors: `scripts/docs_check/links.py` (self-anchors, external schemes, escapes,
      missing targets, fragments) and `scripts/docs_check/text.py` (`without_fences`, `anchors_of`,
      `slugify`, `links_of`).
- [x] 2.3 Redirects: `scripts/docs_check/manifest.py` (`check_stub`: four lines, marker, heading,
      sentence, target) and the `docs/dev/` rename branch in `checks.py`.
- [x] 2.4 Markers and `proseOnly`: `scripts/docs_check/markers.py` (`scan_markers`,
      `validate_config`, unknown kind, `run: no` reason, marker above no fence) and the `proseOnly`
      loop at the end of `checks.py`.
- [x] 2.5 `--run`: `scripts/docs_check/run_examples.py` (isolated `HOME`/`LUNATE_HOME`, temporary
      working directory, `offline` and `help` only, binary from `--binary` or `LUNATE_BIN`).
- [x] 2.6 Self-test and wiring: `scripts/docs_check/self_test.py` (eighteen trees, fourteen of them
      failure paths, plus a succeeding and a failing `run` block), `scripts/docs_check/cli.py`
      (`--check`, `--run`, `--self-test`, exit 2 on a usage error), `scripts/verify.sh` (docs
      profile, `platform_checks`, `--run` after publish), `.github/workflows/process.yml`,
      `.github/workflows/tree-checks.yml` and `.github/workflows/docs-lint.yml`.

## 3. The gate on this tree

- [x] 3.1 `openspec validate documentation-check-contract --strict`, then `openspec validate --all
      --strict`.
- [x] 3.2 `python3 scripts/docs-check --check` and `python3 scripts/docs-check --self-test`.
- [x] 3.3 `python3 tests/scripts/test_docs_check.py`.
- [x] 3.4 `python3 scripts/docs-sync --check` and `npx --yes markdownlint-cli2@0.23.3`.
- [x] 3.5 `git diff --stat origin/main...HEAD`: no page, script, workflow or `docs/docs.json` change
      other than the change folder, and no change to `openspec/specs/repo-quality/spec.md`, which
      #820 owns while it is open.

## 4. Sync and archive

- [x] 4.1 Sync both delta specs into `openspec/specs/`: `openspec archive` applies the deltas, so
      the created `documentation` spec — given its real purpose in the archive commit — and the
      replaced `ci-pipeline` matrix are read back from the main specs after the archive and diffed
      against the deltas.
- [x] 4.2 `openspec archive documentation-check-contract --yes` as the final commit of this branch,
      after `openspec validate --all --strict`.
