#!/usr/bin/env bash
# Reports a red nightly run as one card per failing job.
#
# The nightly run is the only place the full matrix, the PowerShell twin and the perf measurement run
# after #831, so a red night must not wait for the next tag to be noticed. This script reads the run's
# jobs from the API, names every job whose conclusion is red, and files one `kind/bug` card per job in
# the milestone below. The duplicate check is the issue title: a week of red nights for the same job
# is one open card with comments, not seven cards.
#
# Usage:
#   nightly-issue.sh --jobs-json <file> --repo <owner/name> --run-url <url> [--commit <sha>]
#   nightly-issue.sh --self-test
#
# `--jobs-json` is the GitHub Actions jobs endpoint for one run: an object with a `jobs` list, the
# `--paginate --slurp` list of page objects, or a bare job list. The decision is pure (read the file,
# pick the red jobs); the only side effects are the `gh issue` calls. `--self-test` stubs `gh` on PATH
# and asserts create/comment/no-op for synthetic runs.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

LABEL_KIND="kind/bug"
MILESTONE="0.4.0"
TITLE_PREFIX="Nightly CI failed: "

usage() {
    echo "usage: nightly-issue.sh --jobs-json <file> --repo <owner/name> --run-url <url> [--commit <sha>]" >&2
}

# One job name per line for every red job in the run. A job that did not run (skipped) is not a
# failure; only a conclusion that means the job ran and did not pass (or was cancelled) is.
failing_jobs() {
    python3 - "$1" <<'PY'
import json
import sys

RED = {"failure", "timed_out", "cancelled", "action_required", "startup_failure"}


def jobs_of(data):
    if isinstance(data, dict):
        return list(data.get("jobs") or [])
    if isinstance(data, list):
        jobs = []
        for page in data:
            if isinstance(page, dict) and isinstance(page.get("jobs"), list):
                jobs.extend(page["jobs"])
            elif isinstance(page, dict) and page.get("name") is not None:
                jobs.append(page)
        return jobs
    return []


with open(sys.argv[1], encoding="utf-8") as handle:
    data = json.load(handle)

seen = set()
for job in jobs_of(data):
    name = str(job.get("name") or "").strip()
    if not name or name in seen:
        continue
    if str(job.get("conclusion") or "") in RED:
        seen.add(name)
        print(name)
PY
}

issue_title() {
    printf '%s%s' "$TITLE_PREFIX" "$1"
}

issue_body() {
    local job="$1" run_url="$2" commit="$3"
    local body
    body="The nightly \`ci.yml\` run failed in **${job}**.

Run: ${run_url}"
    if [ -n "$commit" ]; then
        body="${body}
Commit: \`${commit}\`"
    fi
    body="${body}

This job is part of the full matrix, the PowerShell twin or the perf measurement that runs on \`main\`
and the nightly rather than on every pull request (#831). Triage the failing step. If it is a runner
flake, rerun the run and comment what you saw instead of opening a second card."
    printf '%s' "$body"
}

# The number of an open issue whose title is exactly this job's card, or empty.
existing_issue() {
    local repo="$1" title="$2"
    gh issue list --repo "$repo" --state open \
        --search "in:title \"${title}\"" --json number --jq '.[0].number // empty'
}

report() {
    local jobs_json="$1" repo="$2" run_url="$3" commit="$4"
    local job title body existing
    while IFS= read -r job; do
        [ -n "$job" ] || continue
        title="$(issue_title "$job")"
        body="$(issue_body "$job" "$run_url" "$commit")"
        existing="$(existing_issue "$repo" "$title")"
        if [ -n "$existing" ]; then
            echo "nightly-issue: ${job}: commenting on #${existing}" >&2
            gh issue comment "$existing" --repo "$repo" --body "$body"
        else
            echo "nightly-issue: ${job}: filing a new card" >&2
            gh issue create --repo "$repo" --title "$title" --body "$body" \
                --label "$LABEL_KIND" --milestone "$MILESTONE"
        fi
    done < <(failing_jobs "$jobs_json")
}

self_test() {
    CASES=0
    FAILURES=0
    local tmp
    tmp="$(mktemp -d "${TMPDIR:-/tmp}/nightly-issue.XXXXXX")"
    # Bash 3.2 has no RETURN trap; the caller removes the directory once.
    _self_test_cases "$tmp" || true
    rm -rf "$tmp"
    if [ "$FAILURES" -ne 0 ]; then
        echo "self-test: FAIL: ${FAILURES} of ${CASES} checks failed" >&2
        return 1
    fi
    echo "self-test: OK (${CASES} checks)"
}

_self_test_cases() {
    local tmp="$1"
    mkdir -p "$tmp/bin"
    cat >"$tmp/bin/gh" <<'GH'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >>"${FAKE_GH_LOG}"
case "$1 $2" in
    "issue list")
        if [ -n "${FAKE_GH_EXISTING:-}" ]; then
            printf '%s\n' "${FAKE_GH_EXISTING}"
        fi
        ;;
    "issue create" | "issue comment") ;;
    *)
        echo "unexpected gh call: $*" >&2
        exit 99
        ;;
esac
GH
    chmod +x "$tmp/bin/gh"

    _run() {
        local name="$1" expected_exit="$2" jobs="$3" existing="${4:-}"
        printf '%s\n' "$jobs" >"$tmp/jobs.json"
        : >"$tmp/gh.log"
        local status=0
        PATH="$tmp/bin:${PATH}" FAKE_GH_LOG="$tmp/gh.log" FAKE_GH_EXISTING="$existing" \
            bash "${BASH_SOURCE[0]}" --jobs-json "$tmp/jobs.json" --repo lunate-agent/lunate \
                --run-url https://example.test/runs/1 --commit abc123 >/dev/null 2>&1 || status=$?
        CASES=$((CASES + 1))
        if [ "$status" -ne "$expected_exit" ]; then
            echo "self-test: FAIL: ${name}: expected exit ${expected_exit}, got ${status}" >&2
            FAILURES=$((FAILURES + 1))
        fi
    }

    _expect_gh() {
        local name="$1" pattern="$2"
        CASES=$((CASES + 1))
        if ! grep -Fq -- "$pattern" "$tmp/gh.log"; then
            echo "self-test: FAIL: ${name}: gh log does not contain '${pattern}'" >&2
            sed 's/^/    /' "$tmp/gh.log" >&2
            FAILURES=$((FAILURES + 1))
        fi
    }

    _run "a red job creates one card" 0 \
        '{"jobs":[{"name":"verify (windows-latest, 2/4)","conclusion":"failure"},{"name":"plan","conclusion":"success"},{"name":"perf (macos-latest)","conclusion":"skipped"}]}'
    _expect_gh "the card names the failing job" "Nightly CI failed: verify (windows-latest, 2/4)"
    _expect_gh "the card carries the run link" "https://example.test/runs/1"
    _expect_gh "the card is a kind/bug in the milestone" "--label kind/bug --milestone 0.4.0"

    _run "a paginated jobs document is read" 0 \
        '[{"jobs":[{"name":"verify (macos-latest, 1/2)","conclusion":"timed_out"}]},{"jobs":[{"name":"ci-ok","conclusion":"success"}]}]'
    _expect_gh "the paginated job is named" "Nightly CI failed: verify (macos-latest, 1/2)"

    _run "an open card is commented on, not duplicated" 0 \
        '{"jobs":[{"name":"ci-ok","conclusion":"failure"}]}' "42"
    _expect_gh "the existing card receives a comment" "issue comment 42"
    CASES=$((CASES + 1))
    if grep -Fq "issue create" "$tmp/gh.log"; then
        echo "self-test: FAIL: an existing card was duplicated" >&2
        FAILURES=$((FAILURES + 1))
    fi

    _run "a green night files nothing" 0 \
        '{"jobs":[{"name":"plan","conclusion":"success"},{"name":"verify (ubuntu-latest, 1/4)","conclusion":"skipped"}]}'
    CASES=$((CASES + 1))
    if [ -s "$tmp/gh.log" ]; then
        echo "self-test: FAIL: a green night called gh" >&2
        sed 's/^/    /' "$tmp/gh.log" >&2
        FAILURES=$((FAILURES + 1))
    fi

    _run "a cancelled job is red" 0 \
        '{"jobs":[{"name":"nightly-issue","conclusion":"cancelled"}]}'
    _expect_gh "a cancelled job is reported" "Nightly CI failed: nightly-issue"

    [ "$FAILURES" -eq 0 ]
}

mode=""
jobs_json=""
repo=""
run_url=""
commit=""
while [ $# -gt 0 ]; do
    case "$1" in
        --jobs-json)
            jobs_json="${2:-}"
            shift 2
            ;;
        --repo)
            repo="${2:-}"
            shift 2
            ;;
        --run-url)
            run_url="${2:-}"
            shift 2
            ;;
        --commit)
            commit="${2:-}"
            shift 2
            ;;
        --self-test)
            mode="self-test"
            shift
            ;;
        *)
            echo "nightly-issue: unknown argument '$1'" >&2
            usage
            exit 2
            ;;
    esac
done

if [ "$mode" = "self-test" ]; then
    self_test
    exit $?
fi

if [ -z "$jobs_json" ] || [ ! -f "$jobs_json" ]; then
    echo "nightly-issue: --jobs-json is required and must exist" >&2
    usage
    exit 2
fi
if [ -z "$repo" ]; then
    echo "nightly-issue: --repo is required" >&2
    usage
    exit 2
fi
if [ -z "$run_url" ]; then
    echo "nightly-issue: --run-url is required" >&2
    usage
    exit 2
fi

report "$jobs_json" "$repo" "$run_url" "$commit"
