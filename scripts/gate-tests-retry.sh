#!/usr/bin/env bash
# Runs scripts/gate-tests.sh under the same one-retry flake budget as the test suites.
#
# The gate self-tests were called directly from CI, so a transient wait-for-nuget timing
# difference on Windows went straight to the aggregator with no retry and no ledger entry.
# This wrapper keeps a first failure in the log, retries once, and records a pass-on-retry
# in the flake ledger so the weekly batch can see it.
set -uo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root" || exit 1

output="$(mktemp "${TMPDIR:-/tmp}/lunate-gate-tests.XXXXXX")"
trap 'rm -f "$output"' EXIT

if bash scripts/gate-tests.sh >"$output" 2>&1; then
    cat "$output"
    exit 0
fi

status=$?
cat "$output" >&2
echo "gate-tests: failed once (exit ${status}) — retrying once under the flake budget" >&2

if bash scripts/gate-tests.sh; then
    ledger="${LUNATE_FLAKE_LEDGER:-${GITHUB_WORKSPACE:-$PWD}/.artifacts/flake-ledger.jsonl}"
    first="$(grep -m1 -E 'FAIL:|failed ' "$output" | sed -e 's/^[[:space:]]*//' || true)"
    mkdir -p "$(dirname "$ledger")"
    python3 - "$ledger" "$first" <<'PY'
import datetime
import json
import platform
import sys

path, first = sys.argv[1:3]
entry = {
    "date": datetime.date.today().isoformat(),
    "leg": platform.system().lower(),
    "step": "gate self-tests",
    "test": first or "unknown",
}
with open(path, "a", encoding="utf-8") as handle:
    handle.write(json.dumps(entry, ensure_ascii=False) + "\n")
PY
    echo "FLAKE: gate self-tests — passed on retry: ${first:-unknown}" >&2
    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
        echo "- flake: gate self-tests (${first:-unknown}) passed on retry" >>"$GITHUB_STEP_SUMMARY"
    fi
    exit 0
fi

echo "gate-tests: failed twice in a row — not a flake" >&2
exit 1
