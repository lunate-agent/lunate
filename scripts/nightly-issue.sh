#!/usr/bin/env bash
# Reports a red CI run on `main` as one card per failing job.
#
# A push to `main` and the nightly are the two events that run the full three-OS matrix, the PowerShell
# twin and the perf measurement (#831), so a red run there must not wait to be noticed by hand (#888
# failed on a macOS push leg and filed nothing). This script reads the run's jobs from the API, names
# every job whose conclusion is red, and files one `kind/bug` card per job, or comments on the open card
# for that job: a week of red runs for the same job is one card with comments, not seven cards.
#
# A job that never started files nothing. GitHub concludes a job it never acquired a hosted runner for as
# `cancelled` with an empty step list, and no change in this repository can fix that: the remedy is a
# rerun, which an issue cannot carry out, so `scripts/ci-issue-triage` names it on stderr and this script
# says in the run log that the run is fixed by rerunning it (#922 filed a card for such a leg, and one
# for its twin on the same run).
#
# The card takes the lowest open version milestone (`0.5.0`, never `backlog`), read from the GitHub API
# so it works on a runner that cannot see the lane's state; a release closes its milestone and the next
# version becomes the target. The area label comes from the failing test's project, and only when it
# decides it: `scripts/ci-issue-triage` owns that decision and is covered by the same self-test.
#
# `--jobs-json` is the GitHub Actions jobs endpoint for one run: an object with a `jobs` list, the
# `--paginate --slurp` list of page objects, or a bare job list. The decisions are pure (read the files,
# pick the red jobs, the milestone and the area); the only side effects are the `gh` calls.
#
# Usage:
#   nightly-issue.sh --jobs-json <file> --repo <owner/name> --run-url <url> [--commit <sha>] [--event <name>]
#   nightly-issue.sh --self-test
#
# `--self-test` stubs `gh` on PATH and asserts create/comment/no-op, the milestone and the area label
# for synthetic runs.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
TRIAGE="${SCRIPT_DIR}/ci-issue-triage"

LABEL_KIND="kind/bug"
TITLE_PREFIX="CI failed: "

usage() {
    echo "usage: nightly-issue.sh --jobs-json <file> --repo <owner/name> --run-url <url> [--commit <sha>] [--event <name>]" >&2
}

issue_title() {
    printf '%s%s' "$TITLE_PREFIX" "$1"
}

event_phrase() {
    case "${1:-}" in
        schedule) printf 'nightly' ;;
        push) printf 'push to main' ;;
        "") printf 'CI' ;;
        *) printf '%s' "$1" ;;
    esac
}

issue_body() {
    local job="$1" run_url="$2" commit="$3" event="$4"
    local body
    body="The $(event_phrase "$event") \`ci.yml\` run failed in **${job}**.

Run: ${run_url}"
    if [ -n "$commit" ]; then
        body="${body}
Commit: \`${commit}\`"
    fi
    body="${body}

This job carries the full matrix, the PowerShell twin or the perf measurement that runs on \`main\` and
the nightly rather than on every pull request (#831). Triage the failing step. If it is a runner flake,
rerun the run and comment what you saw instead of opening a second card."
    printf '%s' "$body"
}

# The number of an open issue whose title is exactly this job's card, or empty.
existing_issue() {
    local repo="$1" title="$2"
    gh issue list --repo "$repo" --state open \
        --search "in:title \"${title}\"" --json number --jq '.[0].number // empty'
}

# The lowest open version milestone's title, or empty. `backlog` and other non-version titles never
# match, and a release that closes its milestone takes it out of the open list.
lowest_milestone() {
    local repo="$1" out="$2"
    if ! gh api --paginate --slurp "repos/${repo}/milestones?state=open&per_page=100" >"$out" 2>/dev/null; then
        echo "nightly-issue: could not read the open milestones; filing without one" >&2
        return 0
    fi
    python3 "$TRIAGE" --milestones-json "$out"
}

# The repository's label names, one per line, or empty when they cannot be read. A card carries an area
# label only when the repository has it: the card matters more than the label, so a missing one is a
# loud line and an unlabelled card, not a failed report.
repo_labels() {
    local repo="$1"
    gh label list --repo "$repo" --limit 200 --json name --jq '.[].name' 2>/dev/null || true
}

has_label() {
    local labels="$1" label="$2"
    [ -n "$label" ] || return 1
    printf '%s\n' "$labels" | grep -Fxq -- "$label"
}

# One red job's log, when GitHub still has it. A cancelled or expired log is not a failure: the card is
# filed without an area label.
job_log() {
    local repo="$1" job_id="$2" out="$3"
    [ -n "$job_id" ] || return 0
    gh api "repos/${repo}/actions/jobs/${job_id}/logs" >"$out" 2>/dev/null || true
}

report() {
    local jobs_json="$1" repo="$2" run_url="$3" commit="$4" event="$5"
    local jobs job_id job title body existing area milestone log labels tmp
    jobs="$(python3 "$TRIAGE" --jobs-json "$jobs_json")"
    if [ -z "$jobs" ]; then
        # Either the run was green, or every red job was one GitHub never acquired a runner for; the
        # triage names those on stderr. A rerun is the remedy for the second case and no card can
        # carry it out, so the red run says so here rather than in an issue that nothing can close.
        echo "nightly-issue: nothing to file: no job in the run ran and did not pass; a job that never started is not a red job, and a run reddened by one is fixed by rerunning ${run_url}" >&2
        return 0
    fi
    tmp="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/nightly-issue.XXXXXX")"
    milestone="$(lowest_milestone "$repo" "${tmp}/milestones.json")"
    if [ -z "$milestone" ]; then
        echo "nightly-issue: no open version milestone; the card is filed without a milestone" >&2
    fi
    labels="$(repo_labels "$repo")"
    while IFS=$'\t' read -r job_id job; do
        [ -n "$job" ] || continue
        title="$(issue_title "$job")"
        body="$(issue_body "$job" "$run_url" "$commit" "$event")"
        log="${tmp}/job-${job_id:-unknown}.log"
        job_log "$repo" "$job_id" "$log"
        area="$(python3 "$TRIAGE" --log-file "$log" --source-root "$REPO_ROOT")"
        if [ -n "$area" ] && ! has_label "$labels" "$area"; then
            echo "nightly-issue: ${job}: the repository has no '${area}' label; filing without it" >&2
            area=""
        fi
        existing="$(existing_issue "$repo" "$title")"
        if [ -n "$existing" ]; then
            echo "nightly-issue: ${job}: commenting on #${existing}" >&2
            gh issue comment "$existing" --repo "$repo" --body "$body"
        else
            echo "nightly-issue: ${job}: filing a new card" >&2
            local args=(issue create --repo "$repo" --title "$title" --body "$body" --label "$LABEL_KIND")
            if [ -n "$area" ]; then
                args+=(--label "$area")
            fi
            if [ -n "$milestone" ]; then
                args+=(--milestone "$milestone")
            fi
            gh "${args[@]}"
        fi
    done <<<"$jobs"
    rm -rf "$tmp"
}

mode=""
jobs_json=""
repo=""
run_url=""
commit=""
event="${GITHUB_EVENT_NAME:-}"
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
        --event)
            event="${2:-}"
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
    exec bash "${SCRIPT_DIR}/nightly-issue-test.sh"
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

report "$jobs_json" "$repo" "$run_url" "$commit" "$event"
