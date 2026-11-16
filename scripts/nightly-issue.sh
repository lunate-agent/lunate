#!/usr/bin/env bash
# Reports a red CI run on `main` as one card per failing job.
#
# A push to `main` and the nightly are the two events that run the full three-OS matrix, the PowerShell
# twin and the perf measurement (#831), so a red run there must not wait to be noticed by hand (#888
# failed on a macOS push leg and filed nothing). This script reads the run's jobs from the API, names
# every job whose conclusion is red, and files one `kind/bug` card per job, or comments on the open card
# for that job: a week of red runs for the same job is one card with comments, not seven cards.
#
# The card takes the lowest open version milestone (`0.5.0`, never `backlog`), read from the GitHub API
# so it works on a runner that cannot see the lane's state; a release closes its milestone and the next
# version becomes the target. The area label comes from the failing test's project, and only when it
# decides it: `scripts/ci-issue-triage` owns that decision and is covered by the same self-test.
#
# `--jobs-json` is the GitHub Actions jobs endpoint for one run: an object with a `jobs` list, the
# `--paginate --slurp` list of page objects, or a bare job list. The decisions are pure (read the files,
# pick the red jobs, decide whether the red is a capacity red, pick the milestone and the area); the only
# side effects are the `gh` calls.
#
# A **capacity red** (#923) is a run whose every red job was cancelled before its first step, because
# GitHub never acquired a hosted runner for it -- run 43904858046's `verify (macos-latest, 1/2)` and
# `perf (macos-latest)`. Nothing the repository builds or tests ran, so a card would send a session after
# a step that never executed, and no change in this repository acquires a hosted runner: a job that never
# started is not filed here and is not commented on (#922). The reporter names the abandoned leg in the
# run's job summary -- also in an ordinary red, where another job that ran is filed as its own card, so a
# run never loses sight of a leg it did not file. A capacity red fails, so that
# `.github/workflows/ci-capacity-retry.yml` reruns the run's failed
# jobs -- a succeeded job is not rerun, which is why this job has to be one of the failures. The attempt
# the retry budget does not cover says so in the same summary and exits clean, with the rerun as the
# remedy. `ci-ok` does not need this job, so the gate's verdict is unchanged either way.
#
# Usage:
#   nightly-issue.sh --jobs-json <file> --repo <owner/name> --run-url <url> [--commit <sha>] [--event <name>] [--attempt <n>]
#   nightly-issue.sh --self-test
#
# `--attempt` defaults to `GITHUB_RUN_ATTEMPT` (then 1) and is the run's attempt number: at or above the
# retry budget a capacity red is named in the summary and exits clean instead of deferring.
#
# `--self-test` stubs `gh` on PATH and asserts create/comment/no-op, the milestone, the area label and the
# capacity deferral for synthetic runs.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
TRIAGE="${SCRIPT_DIR}/ci-issue-triage"

LABEL_KIND="kind/bug"
TITLE_PREFIX="CI failed: "
# One retry of a capacity red: below this attempt the reporter defers to the retry, and at it the run's
# summary says the retry is spent and a rerun is the remedy.
RETRY_BUDGET=2

usage() {
    echo "usage: nightly-issue.sh --jobs-json <file> --repo <owner/name> --run-url <url> [--commit <sha>] [--event <name>] [--attempt <n>]" >&2
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
    local jobs_json="$1" repo="$2" run_url="$3" commit="$4" event="$5" attempt="$6"
    local jobs job_id job title body existing area milestone log labels tmp capacity abandoned
    capacity="$(python3 "$TRIAGE" --capacity-json "$jobs_json")"
    if [ -n "$capacity" ]; then
        defer_capacity_red "$jobs_json" "$run_url" "$attempt" || return 1
        return 0
    fi
    # An ordinary red can still hold a leg that never started while another job ran and failed. The leg
    # files no card, so the run's own summary has to name it and link the run; the card for the job that
    # ran does not speak for it (#923).
    abandoned="$(python3 "$TRIAGE" --abandoned-json "$jobs_json")"
    if [ -n "$abandoned" ]; then
        name_abandoned_legs "$abandoned" "$run_url"
    fi
    jobs="$(python3 "$TRIAGE" --jobs-json "$jobs_json")"
    if [ -z "$jobs" ]; then
        echo "nightly-issue: no red job in the run; nothing to file" >&2
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

# The `- leg` lines of a summary that names abandoned legs, or the note for a run whose only abandoned
# reds are the derived ones.
abandoned_lines() {
    local abandoned="$1" job_id job
    if [ -n "$abandoned" ]; then
        while IFS=$'\t' read -r job_id job; do
            [ -n "$job" ] || continue
            printf -- '- `%s` -- cancelled before its first step; no hosted runner was acquired\n' "$job"
        done <<<"$abandoned"
    else
        printf -- '- every red job of this run only restates the other jobs\n'
    fi
}

# An ordinary red's abandoned leg: the card for the job that ran does not cover it, so the run's summary
# names it and the run link, and says which card the run did file (#923).
name_abandoned_legs() {
    local abandoned="$1" run_url="$2"
    [ -n "${GITHUB_STEP_SUMMARY:-}" ] || return 0
    {
        printf '### Runner capacity: a leg never started, so no card is filed for it\n\n'
        printf 'GitHub cancelled these legs before their first step because it never acquired a hosted runner for them, so nothing the repository builds or tests ran for them:\n\n'
        abandoned_lines "$abandoned"
        printf '\nThe red jobs that ran are filed as their own cards.\n\n'
        printf 'Run: %s\n' "$run_url"
    } >>"$GITHUB_STEP_SUMMARY"
}

# A capacity red is named in the run, never in an issue (#922, #923). The abandoned legs never ran, so
# there is no step to triage and no change in this repository acquires a hosted runner. The reporter
# writes the legs into the run's job summary, calls nothing else, and fails while the retry budget is
# unspent: `.github/workflows/ci-capacity-retry.yml` reruns only the run's failed jobs, so this job has to
# be one of the failures to be here for the retry's outcome. At the budget the same summary says the retry
# is spent and the rerun is the remedy, and the reporter exits clean: failing again would only turn a
# runner shortage into a red reporting step. The derived `ci-ok` and this reporter are never named: they
# failed because a leg did not succeed, not because they were abandoned.
defer_capacity_red() {
    local jobs_json="$1" run_url="$2" attempt="$3"
    local abandoned job_id job
    abandoned="$(python3 "$TRIAGE" --abandoned-json "$jobs_json")"
    while IFS=$'\t' read -r job_id job; do
        [ -n "$job" ] || continue
        echo "nightly-issue: ${job}: no hosted runner was acquired; no card is filed for it" >&2
    done <<<"$abandoned"
    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
        {
            printf '### Runner capacity: no card is filed\n\n'
            printf 'GitHub cancelled these legs before their first step because it never acquired a hosted runner for them, so nothing the repository builds or tests ran:\n\n'
            abandoned_lines "$abandoned"
            printf '\n'
            if [ "$attempt" -lt "$RETRY_BUDGET" ]; then
                printf 'The run is retried once (`.github/workflows/ci-capacity-retry.yml`); rerun it if it is still red afterwards.\n\n'
            else
                printf 'The retry is spent, so the remedy is a rerun: this red is a runner shortage, not a code failure, and no card is filed for it.\n\n'
            fi
            printf 'Run: %s\n' "$run_url"
        } >>"$GITHUB_STEP_SUMMARY"
    fi
    if [ "$attempt" -lt "$RETRY_BUDGET" ]; then
        echo "nightly-issue: deferring to the capacity retry: ${run_url}" >&2
        return 1
    fi
    echo "nightly-issue: the capacity retry is spent; rerunning ${run_url} is the remedy" >&2
    return 0
}

mode=""
jobs_json=""
repo=""
run_url=""
commit=""
event="${GITHUB_EVENT_NAME:-}"
attempt="${GITHUB_RUN_ATTEMPT:-1}"
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
        --attempt)
            attempt="${2:-}"
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
case "$attempt" in
    '' | *[!0-9]* | 0)
        echo "nightly-issue: --attempt must be a positive integer, got '${attempt}'" >&2
        usage
        exit 2
        ;;
esac

report "$jobs_json" "$repo" "$run_url" "$commit" "$event" "$attempt"
