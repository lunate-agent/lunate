#!/usr/bin/env bash
# The cheap repository-wide checks: ADR numbering, the spec-archive rule, the generated docs regions,
# the docs link and navigation check, and the docs lint. They need no build and no test -- seconds of
# wall clock -- and they run from two triggers: a push to `main` (`.github/workflows/tree-checks.yml`)
# and the merge queue (`.github/workflows/ci.yml`'s `tree-checks` job, which `ci-ok` requires).
#
# One command list, two triggers. The queue is the first tree on which two pull requests meet: each
# pull request proved only its own tree against its own base, so a rule both of them break together --
# two records claiming one ADR number, for instance -- is visible only in the batch (#854). The push to
# `main` is the last tree, and the one a docs-only push reaches without running `ci.yml` at all (#738).
#
# Usage:
#   tree-checks.sh [--base <rev>]   # the revision the spec-archive rule diffs (default: origin/main)
#
# Each check is named before it runs, and `set -euo pipefail` makes the first failure the script's
# exit: a check that cannot fail is exactly the defect this script exists to rule out.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

base="origin/main"
while [ $# -gt 0 ]; do
    case "$1" in
        --base)
            base="${2:-}"
            shift 2
            ;;
        --base=*)
            base="${1#--base=}"
            shift
            ;;
        *)
            echo "tree-checks: unknown argument '$1' (usage: tree-checks.sh [--base <rev>])" >&2
            exit 2
            ;;
    esac
done
if [ -z "$base" ]; then
    echo "tree-checks: --base needs a revision to diff the spec-archive rule against" >&2
    exit 2
fi

cd "$REPO_ROOT"

step() {
    printf -- '--- %s\n' "$1"
}

step "ADR numbering"
python3 scripts/check-adr-numbers.py

step "spec changes are archived"
python3 scripts/check-spec-archive.py --base "$base"

step "generated docs regions"
python3 scripts/docs-sync --check

step "docs links and navigation"
python3 scripts/docs-check --check

step "docs lint"
npx --yes markdownlint-cli2@0.23.3

echo "tree-checks: OK"
