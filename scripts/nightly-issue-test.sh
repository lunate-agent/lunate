#!/usr/bin/env bash
# Self-test for scripts/nightly-issue.sh, run through `nightly-issue.sh --self-test`.
#
# `gh` is stubbed on PATH, so the production script runs unchanged and the assertions read what it
# called: a red nightly job and a red push job each file a card naming the leg and the run, the card
# takes the lowest open version milestone in both the bare and the `--paginate --slurp` response shape
# (numerically, never `backlog`) and is filed without one when none is open, the area label follows the
# failing test's project (`area/tui` for the theme-file failure that lived in the coding test project,
# `area/core` for a coding test with no TUI in its source, none for a job that does not decide it, and
# none when any failing test is outside the classified projects), an open card is commented on instead
# of duplicated, and a green run calls nothing at all.
#
# The capacity cases (#923) cover the red the gate cannot report: a run whose red jobs were cancelled
# before a single step started, because GitHub never acquired a hosted runner for them. A leg that never
# started is never filed and never commented on -- the remedy is a rerun (#922) -- so attempt one names
# the abandoned legs in the run summary and defers to the retry
# (`.github/workflows/ci-capacity-retry.yml`), and the attempt the retry budget does not cover says the
# rerun is the remedy and exits clean. The aggregate `ci-ok` and this reporter are derived results and
# are never named as abandoned legs. A job that ran keeps the ordinary path, so no executed failure is
# ever retried away.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPORTER="${SCRIPT_DIR}/nightly-issue.sh"

CASES=0
FAILURES=0

tmp="$(mktemp -d "${TMPDIR:-/tmp}/nightly-issue-test.XXXXXX")"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/bin" "$tmp/logs"

cat >"$tmp/bin/gh" <<'GH'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >>"${FAKE_GH_LOG}"
case "$1" in
    api)
        last=""
        for arg in "$@"; do last="$arg"; done
        case "$last" in
            */milestones*)
                if [ -n "${FAKE_GH_MILESTONES:-}" ]; then
                    cat "$FAKE_GH_MILESTONES"
                fi
                ;;
            */actions/jobs/*/logs)
                id="${last#*/actions/jobs/}"
                id="${id%/logs}"
                if [ -f "${FAKE_GH_LOGS}/${id}.log" ]; then
                    cat "${FAKE_GH_LOGS}/${id}.log"
                else
                    echo "gh: no log for job ${id}" >&2
                    exit 1
                fi
                ;;
            *)
                echo "unexpected gh api call: $*" >&2
                exit 99
                ;;
        esac
        ;;
    label)
        case "${2:-}" in
            list) cat "${FAKE_GH_LABELS}" ;;
            *)
                echo "unexpected gh call: $*" >&2
                exit 99
                ;;
        esac
        ;;
    issue)
        case "${2:-}" in
            list)
                if [ -n "${FAKE_GH_EXISTING:-}" ]; then
                    printf '%s\n' "${FAKE_GH_EXISTING}"
                fi
                ;;
            create | comment) ;;
            *)
                echo "unexpected gh call: $*" >&2
                exit 99
                ;;
        esac
        ;;
    *)
        echo "unexpected gh call: $*" >&2
        exit 99
        ;;
esac
GH
chmod +x "$tmp/bin/gh"

# The open milestones: `0.5.0` is the lowest version, `1.0.0` the highest, and `backlog` never
# matches. A release closes `0.5.0`, which is what the numeric fixture below stands in for.
cat >"$tmp/milestones.json" <<'JSON'
[{"title":"1.0.0"},{"title":"backlog"},{"title":"0.6.0"},{"title":"0.5.0"}]
JSON
cat >"$tmp/milestones-numeric.json" <<'JSON'
[{"title":"0.10.0"},{"title":"0.9.0"}]
JSON
# `gh api --paginate --slurp` answers the milestones list endpoint with one array per page, so the
# production document is a list of lists. `0.9.0` is below `0.10.0` and `1.0.0`; `backlog` never matches.
cat >"$tmp/milestones-slurp.json" <<'JSON'
[[{"title":"1.0.0"},{"title":"0.10.0"}],[{"title":"backlog"},{"title":"0.9.0"}]]
JSON
cat >"$tmp/milestones-backlog.json" <<'JSON'
[{"title":"backlog"}]
JSON

# The repository's label set, and a repository whose area labels have not been created yet.
printf 'kind/bug\narea/tui\narea/core\n' >"$tmp/labels.txt"
printf 'kind/bug\n' >"$tmp/labels-no-area.txt"

# The logs of the red jobs the cases reuse. The theme-file failure is the one #888 filed by hand: it
# lives in `Lunate.Coding.Tests` and its test names `Lunate.Tui.LiveAreaRenderer`, so it is `area/tui`.
cat >"$tmp/logs/27.log" <<'LOG'
  failed Lunate.Coding.Tests.InteractiveSessionThemeFileTests.An_unknown_token_warns_and_the_built_in_theme_stands (12s)
  total: 1, failed: 1
LOG
cat >"$tmp/logs/28.log" <<'LOG'
  failed Lunate.Coding.Tests.CanonicalJsonTests.A_canonical_form_is_stable (3ms)
LOG
cat >"$tmp/logs/29.log" <<'LOG'
  error: the build failed before any test ran
LOG
cat >"$tmp/logs/30.log" <<'LOG'
  failed Lunate.Agent.Tests.FakeFailure.Case (1ms)
LOG
cat >"$tmp/logs/31.log" <<'LOG'
  failed Lunate.Coding.Tests.CanonicalJsonTests.A_canonical_form_is_stable (3ms)
  failed Lunate.Coding.Tests.InteractiveSessionThemeFileTests.An_unknown_token_warns_and_the_built_in_theme_stands (12s)
LOG
cat >"$tmp/logs/32.log" <<'LOG'
  failed Lunate.Coding.Tests.CanonicalJsonTests.A_canonical_form_is_stable (3ms)
  failed Lunate.Agent.Tests.FakeFailure.Case (1ms)
LOG

CASE_EVENT="schedule"
CASE_MILESTONES="$tmp/milestones.json"
CASE_LOGS="$tmp/logs"
CASE_LABELS="$tmp/labels.txt"

_defaults() {
    CASE_EVENT="schedule"
    CASE_MILESTONES="$tmp/milestones.json"
    CASE_LOGS="$tmp/logs"
    CASE_LABELS="$tmp/labels.txt"
}

_run() {
    local name="$1" expected_exit="$2" jobs="$3" existing="${4:-}"
    printf '%s\n' "$jobs" >"$tmp/jobs.json"
    : >"$tmp/gh.log"
    : >"$tmp/err.log"
    : >"$tmp/summary.md"
    local status=0
    PATH="$tmp/bin:${PATH}" \
        FAKE_GH_LOG="$tmp/gh.log" \
        FAKE_GH_EXISTING="$existing" \
        FAKE_GH_MILESTONES="$CASE_MILESTONES" \
        FAKE_GH_LOGS="$CASE_LOGS" \
        FAKE_GH_LABELS="$CASE_LABELS" \
        GITHUB_STEP_SUMMARY="$tmp/summary.md" \
        bash "$REPORTER" --jobs-json "$tmp/jobs.json" --repo lunate-agent/lunate \
            --run-url https://example.test/runs/1 --commit abc123 --event "$CASE_EVENT" \
            --attempt "${CASE_ATTEMPT:-1}" \
            >/dev/null 2>"$tmp/err.log" || status=$?
    CASES=$((CASES + 1))
    if [ "$status" -ne "$expected_exit" ]; then
        echo "self-test: FAIL: ${name}: expected exit ${expected_exit}, got ${status}" >&2
        sed 's/^/    /' "$tmp/err.log" >&2
        FAILURES=$((FAILURES + 1))
    fi
}

_expect_gh() {
    local name="$1" pattern="$2"
    CASES=$((CASES + 1))
    if ! grep -Fq -- "$pattern" "$tmp/gh.log"; then
        echo "self-test: FAIL: ${name}: gh was not called with '${pattern}'" >&2
        sed 's/^/    /' "$tmp/gh.log" >&2
        FAILURES=$((FAILURES + 1))
    fi
}

_expect_no_gh() {
    local name="$1" pattern="$2"
    CASES=$((CASES + 1))
    if grep -Fq -- "$pattern" "$tmp/gh.log"; then
        echo "self-test: FAIL: ${name}: gh was called with '${pattern}'" >&2
        sed 's/^/    /' "$tmp/gh.log" >&2
        FAILURES=$((FAILURES + 1))
    fi
}

_expect_stderr() {
    local name="$1" pattern="$2"
    CASES=$((CASES + 1))
    if ! grep -Fq -- "$pattern" "$tmp/err.log"; then
        echo "self-test: FAIL: ${name}: stderr does not say '${pattern}'" >&2
        sed 's/^/    /' "$tmp/err.log" >&2
        FAILURES=$((FAILURES + 1))
    fi
}

_expect_no_stderr() {
    local name="$1" pattern="$2"
    CASES=$((CASES + 1))
    if grep -Fq -- "$pattern" "$tmp/err.log"; then
        echo "self-test: FAIL: ${name}: stderr unexpectedly says '${pattern}'" >&2
        sed 's/^/    /' "$tmp/err.log" >&2
        FAILURES=$((FAILURES + 1))
    fi
}

_expect_summary() {
    local name="$1" pattern="$2"
    CASES=$((CASES + 1))
    if ! grep -Fq -- "$pattern" "$tmp/summary.md"; then
        echo "self-test: FAIL: ${name}: the job summary does not say '${pattern}'" >&2
        sed 's/^/    /' "$tmp/summary.md" >&2
        FAILURES=$((FAILURES + 1))
    fi
}

_expect_no_summary() {
    local name="$1" pattern="$2"
    CASES=$((CASES + 1))
    if grep -Fq -- "$pattern" "$tmp/summary.md"; then
        echo "self-test: FAIL: ${name}: the job summary says '${pattern}'" >&2
        sed 's/^/    /' "$tmp/summary.md" >&2
        FAILURES=$((FAILURES + 1))
    fi
}

_defaults
_run "a red nightly job creates one card" 0 \
    '{"jobs":[{"id":11,"name":"verify (windows-latest, 2/4)","conclusion":"failure"},{"id":12,"name":"plan","conclusion":"success"},{"id":13,"name":"perf (macos-latest)","conclusion":"skipped"}]}'
_expect_gh "the card names the failing job" "CI failed: verify (windows-latest, 2/4)"
_expect_gh "the card carries the run link" "https://example.test/runs/1"
_expect_gh "the card names the nightly" "The nightly \`ci.yml\` run failed"
_expect_gh "the card is a kind/bug" "--label kind/bug"
_expect_gh "the card takes the lowest open version milestone" "--milestone 0.5.0"
_expect_no_gh "a job without a failing test names no area" "--label area/"
_expect_no_gh "the green jobs are not named" "plan"

_defaults
CASE_EVENT="push"
_run "a red push job creates one card" 0 \
    '{"jobs":[{"id":27,"name":"verify (macos-latest, 1/2)","conclusion":"failure"},{"id":12,"name":"plan","conclusion":"success"}]}'
_expect_gh "the push card names the failing leg" "CI failed: verify (macos-latest, 1/2)"
_expect_gh "the push card carries the run link" "https://example.test/runs/1"
_expect_gh "the push card names the event" "The push to main \`ci.yml\` run failed"
_expect_gh "the push card takes the milestone" "--milestone 0.5.0"
_expect_gh "the theme-file failure is area/tui" "--label area/tui"

_defaults
CASE_EVENT="push"
_run "a coding test with no TUI source is area/core" 0 \
    '{"jobs":[{"id":28,"name":"verify (ubuntu-latest, 1/4)","conclusion":"failure"}]}'
_expect_gh "the coding test is area/core" "--label area/core"

_defaults
CASE_LABELS="$tmp/labels-no-area.txt"
_run "a repository without the area label files the card unlabelled" 0 \
    '{"jobs":[{"id":27,"name":"verify (macos-latest, 1/2)","conclusion":"failure"}]}'
_expect_gh "the card is still filed" "CI failed: verify (macos-latest, 1/2)"
_expect_no_gh "a label the repository lacks is not passed" "--label area/"
_expect_stderr "the run says the label is missing" "has no 'area/tui' label"

_defaults
_run "a test outside Coding and Tui names no area" 0 \
    '{"jobs":[{"id":30,"name":"verify (ubuntu-latest, 2/4)","conclusion":"failure"}]}'
_expect_no_gh "another project's test names no area" "--label area/"

_defaults
_run "an unclassifiable test beside a coding test names no area" 0 \
    '{"jobs":[{"id":32,"name":"verify (ubuntu-latest, 3/4)","conclusion":"failure"}]}'
_expect_gh "the mixed job still files a card" "CI failed: verify (ubuntu-latest, 3/4)"
_expect_no_gh "any unclassifiable test makes the job abstain" "--label area/"

_defaults
_run "a job with no failing test names no area" 0 \
    '{"jobs":[{"id":29,"name":"verify-powershell (windows-latest)","conclusion":"failure"}]}'
_expect_gh "the failing PowerShell job still files a card" "CI failed: verify-powershell (windows-latest)"
_expect_no_gh "a build failure names no area" "--label area/"

_defaults
_run "failing tests that disagree name no area" 0 \
    '{"jobs":[{"id":31,"name":"verify (macos-latest, 2/2)","conclusion":"failure"}]}'
_expect_no_gh "disagreeing areas name no area" "--label area/"

_defaults
CASE_MILESTONES="$tmp/milestones-numeric.json"
_run "milestones are compared numerically" 0 \
    '{"jobs":[{"id":11,"name":"verify (windows-latest, 1/4)","conclusion":"failure"}]}'
_expect_gh "0.9.0 is below 0.10.0" "--milestone 0.9.0"

_defaults
CASE_MILESTONES="$tmp/milestones-slurp.json"
_run "a slurped milestone response takes the numeric lowest version" 0 \
    '{"jobs":[{"id":11,"name":"verify (windows-latest, 1/4)","conclusion":"failure"}]}'
_expect_gh "the slurped page list selects 0.9.0" "--milestone 0.9.0"

_defaults
CASE_MILESTONES="$tmp/milestones-backlog.json"
_run "a card is filed without a milestone when none is open" 0 \
    '{"jobs":[{"id":11,"name":"verify (windows-latest, 1/4)","conclusion":"failure"}]}'
_expect_gh "the card is still filed" "CI failed: verify (windows-latest, 1/4)"
_expect_no_gh "backlog is never the milestone" "--milestone"
_expect_stderr "the run says it filed without a milestone" "no open version milestone"

_defaults
_run "a paginated jobs document is read" 0 \
    '[{"jobs":[{"id":13,"name":"verify (macos-latest, 1/2)","conclusion":"timed_out"}]},{"jobs":[{"id":14,"name":"ci-ok","conclusion":"success"}]}]'
_expect_gh "the paginated job is named" "CI failed: verify (macos-latest, 1/2)"

_defaults
_run "an open card is commented on, not duplicated" 0 \
    '{"jobs":[{"id":11,"name":"ci-ok","conclusion":"failure"}]}' "42"
_expect_gh "the existing card receives a comment" "issue comment 42"
_expect_no_gh "an existing card is duplicated" "issue create"

_defaults
_run "a green run files nothing" 0 \
    '{"jobs":[{"id":12,"name":"plan","conclusion":"success"},{"id":15,"name":"verify (ubuntu-latest, 1/4)","conclusion":"skipped"}]}'
CASES=$((CASES + 1))
if [ -s "$tmp/gh.log" ]; then
    echo "self-test: FAIL: a green run called gh" >&2
    sed 's/^/    /' "$tmp/gh.log" >&2
    FAILURES=$((FAILURES + 1))
fi

_defaults
_run "a cancelled job is red" 0 \
    '{"jobs":[{"id":16,"name":"red-main-issue","conclusion":"cancelled"}]}'
_expect_gh "a cancelled job is reported" "CI failed: red-main-issue"

# A job GitHub never gave a runner is not a red job (#922): it concluded `cancelled`, holds no runner and
# its `steps` list is present and empty. A missing list is not evidence of anything, so the cancelled case
# above stays red. The leg is never filed beside a red that ran, and the run says which leg it was.
_defaults
CASE_ATTEMPT=1
_run "a leg that never started is not filed beside a red that ran" 0 \
    '{"jobs":[{"id":"301","name":"verify (macos-latest, 1/2)","conclusion":"cancelled","runner_id":0,"steps":[]},{"id":"302","name":"verify (ubuntu-latest, 2/4)","conclusion":"failure","runner_id":1000009117,"steps":[{"name":"Verify (bash)","conclusion":"failure"}]},{"id":"303","name":"ci-ok","conclusion":"failure","runner_id":1000009118,"steps":[{"name":"Every required job succeeded","conclusion":"failure"}]}]}'
_expect_gh "a run with a red leg that ran still files its cards" "CI failed: verify (ubuntu-latest, 2/4)"
_expect_no_gh "the abandoned leg is not filed beside the red that ran" "CI failed: verify (macos-latest, 1/2)"
_expect_stderr "the run names the leg that never started" "verify (macos-latest, 1/2): never started"
_expect_no_stderr "a run with a red leg that ran is not deferred" "deferring to the capacity retry"

# The run 43904858046 shape (#923): two legs GitHub never gave a runner, the aggregate that failed
# because they did not succeed, the reporter that deferred them, and everything that ran green.
CAPACITY_JOBS='{"jobs":[{"id":"101","name":"verify (macos-latest, 1/2)","conclusion":"cancelled","runner_id":0,"steps":[]},{"id":"102","name":"perf (macos-latest)","conclusion":"cancelled","runner_id":0,"steps":[]},{"id":"103","name":"verify (macos-latest, 2/2)","conclusion":"success","runner_id":1000009114,"steps":[{"name":"Verify (bash)","conclusion":"success"}]},{"id":"104","name":"ci-ok","conclusion":"failure","runner_id":1000009115,"steps":[{"name":"Every required job succeeded","conclusion":"failure"}]},{"id":"105","name":"red-main-issue","conclusion":"failure","runner_id":1000009116,"steps":[{"name":"File one card per failing job","conclusion":"failure"}]}]}'

_defaults
CASE_ATTEMPT=1
_run "a capacity red is named in the run summary, not filed" 1 "$CAPACITY_JOBS"
CASES=$((CASES + 1))
if [ -s "$tmp/gh.log" ]; then
    echo "self-test: FAIL: a capacity red called gh" >&2
    sed 's/^/    /' "$tmp/gh.log" >&2
    FAILURES=$((FAILURES + 1))
fi
_expect_no_gh "a capacity red opens no card" "issue create"
_expect_no_gh "a capacity red comments on nothing" "issue comment"
_expect_stderr "the run says no hosted runner was acquired" "no hosted runner was acquired"
_expect_stderr "the run defers to the capacity retry" "deferring to the capacity retry"
_expect_summary "the summary names the first abandoned leg" "verify (macos-latest, 1/2)"
_expect_summary "the summary names the perf leg" "perf (macos-latest)"
_expect_summary "the summary says the run is retried" "retried once"
# The aggregate and the reporter failed because a leg did not succeed; neither is an abandoned leg.
_expect_no_summary "the aggregate is not named as an abandoned leg" "ci-ok"
_expect_no_summary "the reporter is not named as an abandoned leg" "red-main-issue"
_expect_no_summary "a green leg is not named in the summary" "verify (macos-latest, 2/2)"

# An open card for such a leg gets no comment either: a leg that never started is not an event a card
# reports on, and the retry is already the answer (#922).
_defaults
CASE_ATTEMPT=1
_run "a capacity red comments on no open card" 1 "$CAPACITY_JOBS" "42"
_expect_no_gh "an open card is not commented on for a leg that never started" "issue comment"
_expect_no_gh "an open card is not duplicated" "issue create"

# The attempt the retry budget does not cover: the run says the retry is spent and names the rerun, and
# still files nothing, because a runner shortage is not a code failure to triage.
_defaults
CASE_ATTEMPT=2
_run "a capacity red that survives the retry names the rerun and files nothing" 0 "$CAPACITY_JOBS"
_expect_no_gh "the spent retry files no card" "issue create"
_expect_no_gh "the spent retry comments on nothing" "issue comment"
_expect_stderr "the run says the retry is spent" "the capacity retry is spent"
_expect_summary "the summary names the abandoned leg" "verify (macos-latest, 1/2)"
_expect_summary "the summary says the retry is spent" "The retry is spent"
_expect_no_summary "the aggregate is not named at the budget" "ci-ok"

# The aggregate and the reporter can be the run's only red jobs when every leg that ran was green and
# nothing else was requested: they are still derived, so no card names them.
_defaults
CASE_ATTEMPT=1
_run "a run whose only abandoned job is the aggregate still defers" 1 \
    '{"jobs":[{"id":"401","name":"ci-ok","conclusion":"cancelled","runner_id":0,"steps":[]}]}'
_expect_no_gh "the aggregate is never filed as an abandoned leg" "issue create"
_expect_summary "the summary says the reds only restate the others" "only restates the other jobs"

_defaults
CASE_ATTEMPT=1
_run "a job that ran is never a capacity red" 0 \
    '{"jobs":[{"id":"201","name":"verify (macos-latest, 1/2)","conclusion":"cancelled","runner_id":1000009114,"steps":[{"name":"Verify (bash)","conclusion":"cancelled"}]},{"id":"202","name":"perf (macos-latest)","conclusion":"cancelled","runner_id":0,"steps":[]}]}'
_expect_gh "a started job is filed, not deferred" "CI failed: verify (macos-latest, 1/2)"
_expect_no_gh "the leg that never started is not filed" "CI failed: perf (macos-latest)"
_expect_no_stderr "a started job is never deferred" "deferring to the capacity retry"

if [ "$FAILURES" -ne 0 ]; then
    echo "self-test: FAIL: ${FAILURES} of ${CASES} checks failed" >&2
    exit 1
fi
echo "self-test: OK (${CASES} checks)"
