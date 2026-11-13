# Design — record the documentation checker's contract

## Context

`scripts/docs-check` and `docs/docs.json` merged with #801 and are live in four places: the
`Process` job (every pull request), `docs-lint.yml` and `tree-checks.yml` (every push to `main`),
and `scripts/verify.sh` (the `platform` job, the `docs` profile, and `--run` after the binary is
published). The specs do not mention it. `ci-pipeline`'s per-commit matrix enumerates the
OS-independent checks and names markdownlint among them; `repo-quality` owns the documentation
conventions and stops at the lint. A session that reads only the specs would conclude the link,
anchor, redirect and navigation check is optional.

This change is a spec correction: it writes down behaviour that already exists and is already pinned
by `tests/scripts/test_docs_check.py` (eight cases) and the checker's own `--self-test` (eighteen
small trees, fourteen of them failure paths, plus a succeeding and a failing `run` block). No code, script, workflow or page changes.

## Diagram

Container view of the check inside the existing gate; nothing new is deployed.

```mermaid
flowchart LR
  subgraph Docs[Documentation tree]
    Pages[docs/**/*.md plus the root docs]
    Manifest[docs/docs.json]
  end
  Checker[scripts/docs-check]
  Subgraph Scripts[Repository scripts]
    Sync[scripts/docs-sync]
  end
  Gate[scripts/verify.sh: platform job and docs profile]
  Jobs[Process job, tree-checks, docs-lint]
  Binary[Published lunate binary]

  Pages --> Checker
  Manifest --> Checker
  Sync --> Pages
  Checker -->|--check and --self-test| Gate
  Checker -->|--check| Jobs
  Checker -->|--run| Binary
```

Boundaries: the documentation tree and its manifest are the only inputs; the checker is a script in
the same container as `docs-sync` and the gate; the published binary is the one external input, used
only by `--run`. The jobs and the gate are the containers that surface its verdict; the change adds
no container, no dependency and no runtime path of the product.

## Decisions

### Record the behaviour in the two owning capabilities, not in a new one

The check has two obligations and they belong to two capabilities: *what* it enforces is a
documentation convention (`repo-quality`), and *when* it runs is the verification matrix
(`ci-pipeline`). A new capability (`documentation-gate`) would duplicate both and split the fact
from its home, which the documentation architecture's first principle forbids. The `repo-quality`
delta is one added requirement; the `ci-pipeline` delta is one modified requirement, copied whole so
the archive does not lose its scenarios.

### Name the check inside the enumeration, rather than in a parallel sentence

The matrix requirement decides what the single Linux job runs. A checker mentioned only in a
separate paragraph leaves the enumeration authoritative, so removing the step from `verify.sh` would
still satisfy the old text. Naming `scripts/docs-check --check` in the same list makes deleting it a
spec change, which is the point of writing it down.

### No code change, and therefore no new test

The change adds no assertion the repository does not already make. `tests/scripts/test_docs_check.py`
pins the self-test, the repository check, the anchor-inside-a-fence case, the usage error, the three
wiring assertions (docs and platform profiles, `--run`, the three always-on jobs) and the file-size
guidance; the checker's self-test pins the failure paths clause by clause. The apply step therefore
verifies each sentence of the delta against a named code path and a named test instead of adding a
second copy of the same coverage.

### `--run` is recorded as a profile behaviour, not a network exception

The docstring-promised "no network command runs in CI" is true because today's markers are all
`run: no` with a reason; the checker still implements `offline` and `help`. The requirement records
both halves so a later slice that adds an `offline` block does not have to amend the spec, and so a
`no` block can never be executed quietly.

## Risks

- The matrix requirement already carries one stale clause (`changes/*.md` never reached its prose
  list in #813). This change copies the existing text and touches only what the documentation check
  needs; correcting the fragment clause belongs to #813's card, not here.
- The `repo-quality` purpose paragraph stays as it is. A delta cannot rewrite a purpose statement,
  and the added requirement is where a reader looks for the rule.

## Related

- [Documentation architecture for 0.4.0](../../../docs/dev/information-architecture.md) — S1 and the
  checker's four layers
- [Documentation map](../../../docs/README.md) — the same rule in prose
