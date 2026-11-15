#!/usr/bin/env bash
# Self-test for scripts/nightly-issue.sh, run through `nightly-issue.sh --self-test`.
#
# `gh` is stubbed on PATH, so the production script runs unchanged and the assertions read what it
# called: a red nightly job and a red push job each file a card naming the leg and the run, the card
# takes the lowest open version milestone (numerically, never `backlog`) and is filed without one when
# none is open, the area label follows the failing test's project (`area/tui` for the theme-file failure
# that lived in the coding test project, `area/core` for a coding test with no TUI in its source, none
# for a job that does not decide it), an open card is commented on instead of duplicated, and a green
# run calls nothing at all.

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
    local status=0
    PATH="$tmp/bin:${PATH}" \
        FAKE_GH_LOG="$tmp/gh.log" \
        FAKE_GH_EXISTING="$existing" \
        FAKE_GH_MILESTONES="$CASE_MILESTONES" \
        FAKE_GH_LOGS="$CASE_LOGS" \
        FAKE_GH_LABELS="$CASE_LABELS" \
        bash "$REPORTER" --jobs-json "$tmp/jobs.json" --repo lunate-agent/lunate \
            --run-url https://example.test/runs/1 --commit abc123 --event "$CASE_EVENT" \
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

if [ "$FAILURES" -ne 0 ]; then
    echo "self-test: FAIL: ${FAILURES} of ${CASES} checks failed" >&2
    exit 1
fi
echo "self-test: OK (${CASES} checks)"
