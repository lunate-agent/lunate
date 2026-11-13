#!/usr/bin/env bash
# Decides the one check branch protection requires: `ci-ok`.
#
# A skipped required check reads as satisfied to branch protection. A draft pull request used to
# skip `ci-ok` too, so a head whose only run was the draft run could show "All checks have passed"
# and a live merge button with no gate behind it. A failing check cannot satisfy a required
# context, so the aggregate now always runs and reports a named failure while the head is a draft,
# while `plan` keeps the draft gate -- a draft push still costs no matrix.
#
# A pull-request verdict comes from the live head, not from this run's payload: ci-ok.sh reads the
# pull request (for its current draft state and head SHA) and that head's check runs through the
# GitHub API, then judges the newest attempt of each required job. A draft-payload run never gated
# and a cancelled attempt does not count, so rerunning an old run cannot turn a ready green red
# (#815) and a cancelled draft run cannot override the ready run (#816). While the live gate's
# required jobs are still queued or running, it fails with "gate still running" instead of deciding
# from this run's stale `needs` values.
#
# Usage:
#   ci-ok.sh            # decide from the environment (the CI step's env block)
#   ci-ok.sh --self-test
#
# A pull request reads through the API (GITHUB_REPOSITORY, PR_NUMBER and GH_TOKEN), or from fixture
# files when CI_OK_PR_JSON and CI_OK_CHECK_RUNS_JSON are set (the self-test does this). Every other
# event keeps the payload decision:
#
#   EVENT_NAME   github.event_name
#   DRAFT        github.event.pull_request.draft ("true"/"false"; empty off a pull request)
#   PLAN         needs.plan.result
#   PROFILE      needs.plan.outputs.profile
#   MACOS        needs.plan.outputs.macos ("true"/"false"): whether the verify matrix has macOS legs
#   VERIFY       needs.verify.result
#   POWERSHELL   needs.verify-powershell.result
#   PS_REQUIRED  needs.changes.outputs.powershell ("true"/"false")
#   DOCS         needs.verify-docs.result
#   PERF         needs.perf.result
#   PLATFORM     needs.platform.result
#   FRAGMENTS    needs.changelog.result
#   PR_NUMBER    github.event.pull_request.number (named in the changelog failure)
#
# Exits 0 only when the run that actually happened is the gate the profile selected. Every other
# outcome -- a draft, a skipped plan, a failed or skipped leg, a leg the profile did not select, an
# unreadable profile -- exits 1 with a named reason.
#
# The macOS verify legs run only where a merge follows (#831): a pull-request run has none and
# passes without them, while a merge group, a push to main, the nightly and a manual dispatch
# must have had them. An unreadable decision (MACOS empty) is red.
#
# The PowerShell leg is path-filtered: skipped is a pass when `changes` said its paths did not
# match, and a failure when nothing said so. An unreadable decision (PS_REQUIRED empty) is red, not
# "probably fine".

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
WORKFLOW="${REPO_ROOT}/.github/workflows/ci.yml"

fail() {
    echo "::error::ci-ok: $1"
    exit 1
}

decide() {
    local event="${EVENT_NAME:-}" draft="${DRAFT:-}" plan="${PLAN:-}"
    local profile="${PROFILE:-}" verify="${VERIFY:-}" powershell="${POWERSHELL:-}"
    local ps_required="${PS_REQUIRED:-}" docs="${DOCS:-}" perf="${PERF:-}" platform="${PLATFORM:-}"
    local fragments="${FRAGMENTS:-}" pull_request="${PR_NUMBER:-}" macos="${MACOS:-}"

    echo "ci-ok: event=$event draft=$draft plan=$plan profile=$profile macos=$macos verify=$verify verify-powershell=$powershell ps-required=$ps_required verify-docs=$docs perf=$perf platform=$platform changelog=$fragments"

    if [ "$event" = "pull_request" ] && [ "$draft" != "false" ]; then
        fail "the head is a draft (draft=$draft), so no gate ran; a draft push costs no matrix, but the required check still has to report. Mark the pull request ready for review and this check runs again."
    fi
    if [ "$plan" != "success" ]; then
        fail "the plan job did not succeed (plan=$plan)"
    fi
    # The changelog guard runs on every non-draft pull request, whichever profile it selects. A
    # pull-request run without its success is red; outside a pull request it must not have run.
    case "$event" in
        pull_request)
            if [ "$fragments" != "success" ]; then
                fail "the changelog guard did not succeed (changelog=$fragments); a pull request other than the release pull request must not edit CHANGELOG.md. Write the line in changes/${pull_request:-<pull-request-number>}.md instead"
            fi
            ;;
        *)
            if [ "$fragments" != "skipped" ]; then
                fail "the changelog guard ran although only a pull request needs it (changelog=$fragments)"
            fi
            ;;
    esac
    case "$profile" in
        docs)
            if [ "$verify" != "skipped" ] || [ "$powershell" != "skipped" ] || [ "$perf" != "skipped" ] || [ "$platform" != "skipped" ]; then
                fail "profile=docs, yet a full leg ran (verify=$verify, verify-powershell=$powershell, perf=$perf, platform=$platform)"
            fi
            if [ "$docs" != "success" ]; then
                fail "the docs profile did not succeed (verify-docs=$docs)"
            fi
            ;;
        full)
            # The dedicated macOS perf leg is a nightly trend as of #724: a release tag is owned by
            # the Release workflow, not this one. A pull request sees it skipped.
            local perf_gate="$perf"
            case "$event" in
                pull_request | merge_group)
                    # Neither is a release: the dedicated perf leg is release-only, so its absence
                    # must not fail the gate. A merge group that required it was ejected from the
                    # queue by a timing measurement on a loaded runner.
                    [ "$perf" = "skipped" ] && perf_gate="success"
                    ;;
                push)
                    # A push that reaches this workflow is a push to main: tags belong to
                    # release.yml. A shared runner varies by a few percent, so on a branch push its
                    # result does not feed this check at all.
                    perf_gate="success"
                    ;;
            esac
            if [ "$verify" != "success" ] || [ "$perf_gate" != "success" ] || [ "$platform" != "success" ]; then
                fail "a required job did not succeed (verify=$verify, perf=$perf, platform=$platform)"
            fi
            # The verify result covers the legs the plan put in the matrix; `macos` says whether
            # macOS was one of them. A pull request leaves macOS to its merge-queue group (#831).
            case "$event:$macos" in
                pull_request:true | pull_request:false) ;;
                *:true) ;;
                *:false)
                    fail "the macOS verify legs did not run on a $event run (macos=false); only a pull request may leave them to the merge queue"
                    ;;
                *)
                    fail "the plan's macOS decision is unreadable (macos='$macos'); refusing to pass on an unknown state"
                    ;;
            esac
            case "$ps_required" in
                true)
                    if [ "$powershell" != "success" ]; then
                        fail "the PowerShell leg was required by the changed paths but did not run (verify-powershell=$powershell); a skipped or cancelled leg is not a pass"
                    fi
                    ;;
                false)
                    if [ "$powershell" != "skipped" ] && [ "$powershell" != "success" ]; then
                        fail "the PowerShell leg was not required and did not pass either (verify-powershell=$powershell)"
                    fi
                    ;;
                *)
                    fail "the path decision for the PowerShell leg is unreadable (ps-required='$ps_required'); refusing to pass on an unknown state"
                    ;;
            esac
            if [ "$docs" != "skipped" ]; then
                fail "profile=full, yet the docs leg ran (verify-docs=$docs)"
            fi
            ;;
        *)
            fail "the plan produced no usable profile ('$profile'); refusing to pass"
            ;;
    esac
    echo "ci-ok: the $profile gate succeeded"
}

# Fetch the live pull request and its live head's check runs. The fixture variables exist so the
# self-test can exercise the same path without the network; in CI, `gh api` reads the API with the
# job's GITHUB_TOKEN.
_live_state() {
    local pr_json="${CI_OK_PR_JSON:-}" runs_json="${CI_OK_CHECK_RUNS_JSON:-}" tmp=""
    if [ -z "$pr_json" ] || [ -z "$runs_json" ]; then
        command -v gh >/dev/null 2>&1 || fail "gh is not installed; cannot read the head's live state"
        [ -n "${GITHUB_REPOSITORY:-}" ] || fail "GITHUB_REPOSITORY is not set; cannot read the head's live state"
        [ -n "${PR_NUMBER:-}" ] || fail "PR_NUMBER is not set; cannot read the head's live state"
        tmp="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/ci-ok.XXXXXX")"
        if ! gh api "repos/${GITHUB_REPOSITORY}/pulls/${PR_NUMBER}" >"${tmp}/pr.json"; then
            rm -rf "$tmp"
            fail "could not read pull request #${PR_NUMBER} from the API; refusing to decide from this run's payload"
        fi
        local head_sha
        head_sha="$(python3 -c 'import json, sys; print(json.load(open(sys.argv[1], encoding="utf-8"))["head"]["sha"])' "${tmp}/pr.json" 2>/dev/null)" || head_sha=""
        if [ -z "$head_sha" ]; then
            rm -rf "$tmp"
            fail "could not read the pull request's head SHA from the API"
        fi
        if ! gh api --paginate --slurp \
            "repos/${GITHUB_REPOSITORY}/commits/${head_sha}/check-runs?per_page=100&filter=all" \
            >"${tmp}/check-runs.json"; then
            rm -rf "$tmp"
            fail "could not read the check runs for head ${head_sha}; refusing to decide from this run's payload"
        fi
        pr_json="${tmp}/pr.json"
        runs_json="${tmp}/check-runs.json"
    fi

    local state status=0
    state="$(python3 "${SCRIPT_DIR}/ci-ok-state" --pr-json "$pr_json" --check-runs-json "$runs_json")" || status=$?
    [ -n "$tmp" ] && rm -rf "$tmp"
    if [ "$status" -ne 0 ]; then
        fail "could not read the head's live state (ci-ok-state exit ${status})"
    fi
    printf '%s\n' "$state"
}

# The pull-request path: the live state replaces every payload-derived value, then the same decision
# rules run. A live draft still reports red -- the draft has not been reviewed -- while a live ready
# head with running jobs says so by name.
_decide_live() {
    local state
    state="$(_live_state)"
    eval "$state"
    echo "ci-ok: live draft=$DRAFT plan=$PLAN profile=$PROFILE macos=$MACOS verify=$VERIFY verify-powershell=$POWERSHELL ps-required=$PS_REQUIRED verify-docs=$DOCS perf=$PERF platform=$PLATFORM changelog=$FRAGMENTS"
    if [ "$DRAFT" = "true" ]; then
        decide
    fi
    if [ -n "${GATE_RUNNING:-}" ]; then
        fail "gate still running: ${GATE_RUNNING}; wait for those jobs to finish, then rerun ci-ok"
    fi
    decide
}

# One job's block from the workflow, at the two-space indent of a job key under `jobs:`.
_job_block() {
    python3 - "$WORKFLOW" "$1" <<'PY'
import re
import sys

workflow_path, job = sys.argv[1:]
inside = False
with open(workflow_path, encoding="utf-8") as workflow:
    for line in workflow:
        line = line.rstrip("\r\n")
        if line == f"  {job}:":
            inside = True
            continue
        if inside and re.fullmatch(r"  [A-Za-z0-9_-]+:", line):
            break
        if inside:
            print(line)
PY
}

_run_from_env() {
    local fragments="${11:-}" macos="${12-default}"
    if [ -z "$fragments" ]; then
        case "$1" in
            pull_request) fragments="success" ;;
            *) fragments="skipped" ;;
        esac
    fi
    # The plan's decision unless a case names one: no macOS legs on a pull request, macOS legs on
    # every other event.
    if [ "$macos" = "default" ]; then
        case "$1" in
            pull_request) macos="false" ;;
            *) macos="true" ;;
        esac
    fi
    env CI_OK_FROM_ENV=1 EVENT_NAME="$1" DRAFT="$2" PLAN="$3" PROFILE="$4" \
        VERIFY="$5" POWERSHELL="$6" DOCS="$7" PERF="$8" PLATFORM="$9" PS_REQUIRED="${10}" \
        FRAGMENTS="$fragments" MACOS="$macos" \
        bash "${SCRIPT_DIR}/ci-ok.sh"
}

_self_case() {
    local name="$1" expected="$2" needle="$3"
    shift 3
    local out status=0
    if out="$(_run_from_env "$@" 2>&1)"; then
        status=0
    else
        status=$?
    fi
    CASES=$((CASES + 1))
    if [ "$status" -ne "$expected" ]; then
        echo "self-test: FAIL: ${name}: expected exit ${expected}, got ${status}" >&2
        printf '%s\n' "$out" | sed 's/^/    /' >&2
        FAILURES=$((FAILURES + 1))
        return 0
    fi
    case "$out" in
        *"$needle"*) ;;
        *)
            echo "self-test: FAIL: ${name}: output does not mention '${needle}'" >&2
            printf '%s\n' "$out" | sed 's/^/    /' >&2
            FAILURES=$((FAILURES + 1))
            ;;
    esac
}

# A live-head fixture case: the pull request and all-attempts check runs are JSON under
# tests/fixtures/ci-ok/<name>/, and the same production path decides them.
_self_live_case() {
    local name="$1" expected="$2" needle="$3" fixture="$4"
    local out status=0
    if out="$(env EVENT_NAME=pull_request PR_NUMBER=1234 \
        CI_OK_PR_JSON="${CI_OK_FIXTURES}/${fixture}/pr.json" \
        CI_OK_CHECK_RUNS_JSON="${CI_OK_FIXTURES}/${fixture}/check-runs.json" \
        bash "${SCRIPT_DIR}/ci-ok.sh" 2>&1)"; then
        status=0
    else
        status=$?
    fi
    CASES=$((CASES + 1))
    if [ "$status" -ne "$expected" ]; then
        echo "self-test: FAIL: ${name}: expected exit ${expected}, got ${status}" >&2
        printf '%s\n' "$out" | sed 's/^/    /' >&2
        FAILURES=$((FAILURES + 1))
        return 0
    fi
    case "$out" in
        *"$needle"*) ;;
        *)
            echo "self-test: FAIL: ${name}: output does not mention '${needle}'" >&2
            printf '%s\n' "$out" | sed 's/^/    /' >&2
            FAILURES=$((FAILURES + 1))
            ;;
    esac
}

# A workflow invariant, so the required context cannot regress to a skip: `present` is 1 when the
# pattern must appear in the job block, 0 when it must not.
_self_workflow() {
    local name="$1" job="$2" pattern="$3" present="$4" found=0
    if _job_block "$job" | grep -Eq "$pattern"; then
        found=1
    fi
    CASES=$((CASES + 1))
    if [ "$found" = "$present" ]; then
        return 0
    fi
    echo "self-test: FAIL: ${name}" >&2
    FAILURES=$((FAILURES + 1))
}

# Evaluate a workflow's concurrency block for one event scenario and print "<group>|<cancel>".
# Only the expression forms the workflows use are understood; anything else is an error, so a new
# shape cannot slip past the assertions below.
_concurrency_decision() {
    python3 - "$1" "$2" <<'PY'
import re
import sys

workflow_path, scenario = sys.argv[1:]
scenarios = {
    "push-main": ("push", "refs/heads/main", False),
    "push-tag": ("push", "refs/tags/v1.2.3", False),
    "pull_request": ("pull_request", "refs/pull/42/merge", False),
    "pull_request-draft": ("pull_request", "refs/pull/42/merge", True),
    "pull_request-ready": ("pull_request", "refs/pull/42/merge", False),
    "merge_group": ("merge_group", "refs/heads/gh-readonly-queue/main/pr-42-abc", False),
    "schedule": ("schedule", "refs/heads/main", False),
    "workflow_dispatch": ("workflow_dispatch", "refs/heads/main", False),
}
if scenario not in scenarios:
    sys.exit(f"unknown scenario: {scenario}")
event, ref, draft = scenarios[scenario]

with open(workflow_path, encoding="utf-8") as handle:
    lines = [line.rstrip("\r\n") for line in handle]

concurrency = {}
inside = False
for line in lines:
    if line == "concurrency:":
        inside = True
        continue
    if not inside:
        continue
    if not line.startswith(" "):
        break
    match = re.fullmatch(r"  ([A-Za-z-]+):\s*(.*)", line)
    if match:
        concurrency[match.group(1)] = match.group(2).strip()

if "group" not in concurrency or "cancel-in-progress" not in concurrency:
    sys.exit(f"{workflow_path}: no usable concurrency block")


def unquote(value):
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "'\"":
        return value[1:-1]
    return value


group_expr = unquote(concurrency["group"])
cancel_expr = unquote(concurrency["cancel-in-progress"])


def evaluate_group(expr):
    expr = expr.replace("${{ github.ref }}", ref)
    expr = re.sub(
        r"\$\{\{\s*github\.event_name == '([^']*)' && '([^']*)' \|\| ''\s*\}\}",
        lambda match: match.group(2) if event == match.group(1) else "",
        expr,
    )
    expr = re.sub(
        r"\$\{\{\s*github\.event_name == '([^']*)' && github\.event\.pull_request\.draft(?: == true)? && '([^']*)' \|\| ''\s*\}\}",
        lambda match: match.group(2) if event == match.group(1) and draft else "",
        expr,
    )
    if "${{" in expr:
        raise ValueError(f"unsupported concurrency group expression: {group_expr}")
    return expr


def evaluate_cancel(expr):
    if expr in ("true", "false"):
        return expr
    equal = re.fullmatch(r"\$\{\{\s*github\.event_name == '([^']*)'\s*\}\}", expr)
    if equal:
        return "true" if event == equal.group(1) else "false"
    unequal = re.fullmatch(r"\$\{\{\s*github\.event_name != '([^']*)'\s*\}\}", expr)
    if unequal:
        return "true" if event != unequal.group(1) else "false"
    raise ValueError(f"unsupported cancel-in-progress expression: {cancel_expr}")


try:
    print(f"{evaluate_group(group_expr)}|{evaluate_cancel(cancel_expr)}")
except ValueError as error:
    sys.exit(f"{workflow_path}: {error}")
PY
}

# A concurrency invariant: the group and cancellation decision this workflow resolves to for one
# event scenario must be the expected ones. A workflow whose expression cannot be evaluated fails.
_self_concurrency() {
    local name="$1" workflow="$2" scenario="$3" want_group="$4" want_cancel="$5"
    local out status=0
    if ! out="$(_concurrency_decision "$workflow" "$scenario" 2>&1)"; then
        status=$?
    fi
    CASES=$((CASES + 1))
    if [ "$status" -ne 0 ]; then
        echo "self-test: FAIL: ${name} (${scenario}): ${out}" >&2
        FAILURES=$((FAILURES + 1))
        return 0
    fi
    if [ "$out" != "${want_group}|${want_cancel}" ]; then
        echo "self-test: FAIL: ${name}: ${scenario} expected '${want_group}|${want_cancel}', got '${out}'" >&2
        FAILURES=$((FAILURES + 1))
    fi
}

# Evaluate whether a workflow's `on.push` filters select one push scenario, and print `runs` or
# `does-not-run`. Only the literal list filters the workflows use are understood; anything else is
# an error, so a new trigger shape cannot slip past the assertions below.
_push_trigger_decision() {
    python3 - "$1" "$2" <<'PY'
import fnmatch
import re
import sys

workflow_path, scenario = sys.argv[1:]
scenarios = {
    "push-main": ("branch", "main"),
    "push-tag": ("tag", "v1.2.3"),
}
if scenario not in scenarios:
    sys.exit(f"unknown scenario: {scenario}")
ref_type, name = scenarios[scenario]


def unquote(value):
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "'\"":
        return value[1:-1]
    return value


def list_value(value):
    value = value.strip()
    if not value:
        return []
    if value.startswith("[") and value.endswith("]"):
        inner = value[1:-1].strip()
        if not inner:
            return []
        return [unquote(part) for part in inner.split(",") if part.strip()]
    return [unquote(value)]


with open(workflow_path, encoding="utf-8") as handle:
    lines = [line.rstrip("\r\n") for line in handle]

filters = {}
inside_on = False
inside_push = False
current_key = None
for line in lines:
    if not line.strip() or line.lstrip().startswith("#"):
        continue
    indent = len(line) - len(line.lstrip(" "))
    stripped = line.strip()
    if indent == 0:
        inside_on = stripped == "on:"
        inside_push = False
        current_key = None
        continue
    if not inside_on:
        continue
    if indent == 2:
        inside_push = stripped == "push:"
        current_key = None
        continue
    if not inside_push:
        continue
    if stripped.startswith("- ") and current_key:
        filters.setdefault(current_key, []).append(unquote(stripped[2:]))
        continue
    match = re.fullmatch(r"([A-Za-z-]+):\s*(.*)", stripped)
    if match:
        current_key = match.group(1)
        filters[current_key] = list_value(match.group(2))

branch_filtered = any(key in filters for key in ("branches", "branches-ignore"))
tag_filtered = any(key in filters for key in ("tags", "tags-ignore"))


def matches(key):
    return any(fnmatch.fnmatchcase(name, pattern) for pattern in filters.get(key, []))


if ref_type == "branch":
    if not branch_filtered and not tag_filtered:
        runs = True
    elif not branch_filtered:
        runs = False
    else:
        runs = ("branches" not in filters or matches("branches")) and not matches(
            "branches-ignore"
        )
else:
    if not branch_filtered and not tag_filtered:
        runs = True
    elif not tag_filtered:
        runs = False
    else:
        runs = ("tags" not in filters or matches("tags")) and not matches("tags-ignore")

print("runs" if runs else "does-not-run")
PY
}

# A trigger invariant: the event this workflow must (or must not) run for. A workflow whose trigger
# cannot be evaluated fails the self-test rather than passing vacuously.
_self_push_trigger() {
    local name="$1" workflow="$2" scenario="$3" want="$4"
    local out status=0
    if ! out="$(_push_trigger_decision "$workflow" "$scenario" 2>&1)"; then
        status=$?
    fi
    CASES=$((CASES + 1))
    if [ "$status" -ne 0 ]; then
        echo "self-test: FAIL: ${name} (${scenario}): ${out}" >&2
        FAILURES=$((FAILURES + 1))
        return 0
    fi
    if [ "$out" != "$want" ]; then
        echo "self-test: FAIL: ${name}: ${scenario} expected '${want}', got '${out}'" >&2
        FAILURES=$((FAILURES + 1))
    fi
}

self_test() {
    CASES=0
    FAILURES=0
    local CI_OK_FIXTURES="${REPO_ROOT}/tests/fixtures/ci-ok"

    # The live-head decision (#830): the payload of the run that woke ci-ok never decides a pull
    # request. A rerun of a draft payload sees the ready run that actually gated, a cancelled run
    # does not override it, a running gate says so by name, and a live draft stays red.
    _self_live_case "a draft-payload rerun on a passed head is green (#815)" 0 \
        "the full gate succeeded" "815-draft-rerun-green"
    _self_live_case "a draft-payload rerun on a failed head is red (#815)" 1 \
        "did not succeed" "815-ready-failed-red"
    _self_live_case "cancelled draft legs do not override the ready run (#816)" 0 \
        "the full gate succeeded" "816-cancelled-legs-green"
    _self_live_case "a ready gate with running jobs names them" 1 \
        "gate still running" "ready-still-running"
    _self_live_case "a live draft is red even when an older run passed" 1 \
        "the head is a draft (draft=true)" "live-draft-red"

    # The required context must never be skipped on a head that can be merged.
    _self_case "a draft pull request fails instead of skipping" 1 "draft" \
        pull_request true skipped "" skipped skipped skipped skipped "" ""
    _self_case "a draft with no job results still fails for the draft" 1 "draft" \
        pull_request true skipped "" "" "" "" "" "" ""
    _self_case "a pull request whose draft flag did not render fails closed" 1 "draft" \
        pull_request "" success full success success success skipped success success true

    # The profiles that can pass.
    _self_case "a full gate that passed" 0 "the full gate succeeded" \
        pull_request false success full success success skipped success success true
    _self_case "a full gate whose PowerShell leg was not required" 0 "the full gate succeeded" \
        pull_request false success full success skipped skipped success success false
    _self_case "a full gate whose PowerShell leg ran without being required" 0 "the full gate succeeded" \
        pull_request false success full success success skipped success success false
    _self_case "a docs gate that passed" 0 "the docs gate succeeded" \
        pull_request false success docs skipped skipped success skipped skipped ""
    _self_case "a push to main" 0 "the full gate succeeded" \
        push "" success full success success skipped success success true
    _self_case "a manual dispatch" 0 "the full gate succeeded" \
        workflow_dispatch "" success full success success skipped success success true

    # Every way the run that happened can fail to be the gate:
    _self_case "a failed verify leg" 1 "did not succeed" \
        pull_request false success full failure success skipped success success true
    _self_case "a failed windows leg" 1 "was required by the changed paths" \
        pull_request false success full success failure skipped success success true
    _self_case "a failed perf leg" 1 "did not succeed" \
        pull_request false success full success success skipped failure success true
    _self_case "a failed platform leg" 1 "did not succeed" \
        pull_request false success full success success skipped success failure true
    _self_case "a failed docs leg" 1 "did not succeed" \
        pull_request false success docs skipped skipped failure skipped skipped ""
    _self_case "a skipped verify leg on the full profile" 1 "did not succeed" \
        pull_request false success full skipped success skipped success success true
    _self_case "a skipped perf leg on a pull request" 0 "the full gate succeeded" \
        pull_request false success full success success skipped skipped success true
    _self_case "a skipped perf leg on a branch push is not required" 0 "the full gate succeeded" \
        push "" success full success success skipped skipped success true
    _self_case "a failed perf leg on a branch push never feeds this check" 0 "the full gate succeeded" \
        push "" success full success success skipped failure success true
    _self_case "a failed perf leg on the nightly is the nightly verdict" 1 "did not succeed" \
        schedule "" success full success success skipped failure success true
    _self_case "a skipped perf leg on a merge group" 0 "the full gate succeeded" \
        merge_group "" success full success success skipped skipped success true
    _self_case "a failed perf leg on a merge group is still a failure" 1 "did not succeed" \
        merge_group "" success full success success skipped failure success true
    _self_case "a skipped platform leg on the full profile" 1 "did not succeed" \
        pull_request false success full success success skipped success skipped true

    # The macOS legs (#831): a pull request passes without them; every event a merge follows
    # requires them.
    _self_case "a pull-request run without macOS legs passes" 0 "the full gate succeeded" \
        pull_request false success full success success skipped success success true "" false
    _self_case "a pull-request run that had macOS legs still passes" 0 "the full gate succeeded" \
        pull_request false success full success success skipped success success true "" true
    _self_case "a merge group with its macOS legs passes" 0 "the full gate succeeded" \
        merge_group "" success full success success skipped skipped success true "" true
    _self_case "a merge group without macOS legs fails" 1 "macOS verify legs did not run on a merge_group run" \
        merge_group "" success full success success skipped skipped success true "" false
    _self_case "a push to main without macOS legs fails" 1 "macOS verify legs did not run on a push run" \
        push "" success full success success skipped success success true "" false
    _self_case "a nightly without macOS legs fails" 1 "macOS verify legs did not run on a schedule run" \
        schedule "" success full success success skipped success success true "" false
    _self_case "a merge group with an unreadable macOS decision fails" 1 "macOS decision is unreadable" \
        merge_group "" success full success success skipped skipped success true "" ""
    _self_case "a pull request with an unreadable macOS decision fails" 1 "macOS decision is unreadable" \
        pull_request false success full success success skipped success success true "" ""
    _self_case "a docs gate does not consult the macOS decision" 0 "the docs gate succeeded" \
        merge_group "" success docs skipped skipped success skipped skipped "" "" ""

    # The path-filtered PowerShell leg: skipped is a pass only when the change said so.
    _self_case "a PowerShell leg skipped although the paths required it" 1 "was required by the changed paths" \
        pull_request false success full success skipped skipped success success true
    _self_case "a PowerShell leg skipped with an unreadable decision" 1 "unreadable" \
        pull_request false success full success skipped skipped success success ""
    _self_case "a PowerShell leg cancelled although required" 1 "was required by the changed paths" \
        pull_request false success full success cancelled skipped success success true
    _self_case "a PowerShell leg cancelled although not required" 1 "did not pass either" \
        pull_request false success full success cancelled skipped success success false

    # Legs that ran although the profile did not select them.
    _self_case "the docs leg ran on a full profile" 1 "docs leg ran" \
        pull_request false success full success success success success success true
    _self_case "the matrix ran on a docs profile" 1 "full leg ran" \
        pull_request false success docs success skipped success success skipped ""
    _self_case "the perf leg ran on a docs profile" 1 "full leg ran" \
        pull_request false success docs skipped skipped success success skipped ""
    _self_case "the platform leg ran on a docs profile" 1 "full leg ran" \
        pull_request false success docs skipped skipped skipped success success ""
    _self_case "a skipped plan on a non-draft head" 1 "plan job did not succeed" \
        pull_request false skipped "" skipped skipped skipped skipped "" ""
    _self_case "an unreadable profile" 1 "no usable profile" \
        pull_request false success "" skipped skipped skipped skipped "" ""

    # The changelog guard: required on every pull request, absent outside one.
    _self_case "a pull request whose changelog guard failed" 1 "changelog guard did not succeed" \
        pull_request false success full success success skipped success success true failure
    PR_NUMBER=1234 _self_case "a failed changelog guard names the fragment path" 1 "changes/1234.md" \
        pull_request false success docs skipped skipped success skipped skipped "" failure
    _self_case "a pull request whose changelog guard was skipped" 1 "changelog guard did not succeed" \
        pull_request false success docs skipped skipped success skipped skipped "" skipped
    _self_case "a pull request whose changelog guard passed" 0 "the docs gate succeeded" \
        pull_request false success docs skipped skipped success skipped skipped "" success
    _self_case "a main push on which the changelog guard ran" 1 "only a pull request needs it" \
        push "" success full success success skipped success success true success

    # The event this workflow runs for: a main push runs the required gate, while a tag push belongs
    # to release.yml and must never leave a second ci-ok verdict on the release commit (#724).
    _self_push_trigger "ci: a push to main runs the required gate" "$WORKFLOW" push-main runs
    _self_push_trigger "ci: a tag push does not run this workflow" "$WORKFLOW" push-tag does-not-run
    _self_push_trigger "release: a v* tag still runs the release workflow" \
        "${REPO_ROOT}/.github/workflows/release.yml" push-tag runs

    # The wiring this script's decisions rely on.
    _self_workflow "ci-ok must not gate itself on draft (a skip reads as satisfied)" \
        ci-ok '^    if:.*draft' 0
    _self_workflow "ci-ok must still run when a leg failed" \
        ci-ok '^    if: always\(\)' 1
    _self_workflow "ci-ok must decide through scripts/ci-ok.sh" \
        ci-ok 'scripts/ci-ok\.sh' 1
    _self_workflow "plan must keep the draft gate, so a draft push costs no matrix" \
        plan '^    if:.*draft' 1
    _self_workflow "ci-ok must take every gate leg as a needed job" \
        ci-ok '^    needs: \[plan, changes, verify, verify-powershell, platform, verify-docs, perf, changelog\]' 1
    _self_workflow "the verify matrix must be the plan's leg decision" \
        verify 'matrix: \$\{\{ fromJSON\(needs\.plan\.outputs\.verify_matrix\) \}\}' 1
    _self_workflow "the plan must hand ci-ok its macOS decision" \
        plan 'macos: \$\{\{ steps\.legs\.outputs\.macos \}\}' 1
    _self_workflow "ci-ok must read the plan's macOS decision" \
        ci-ok 'MACOS: \$\{\{ needs\.plan\.outputs\.macos \}\}' 1
    _self_workflow "the legs must cover Ubuntu and Windows on every event" \
        plan '^          leg (ubuntu|windows)-latest .*"\$\{SHARDS\}"' 1
    _self_workflow "the macOS legs must be left out of a pull-request run only" \
        plan '^            pull_request\) macos=false ;;' 1
    _self_workflow "the macOS legs must run two shards" \
        plan 'macos_shards=\$\(\(SHARDS < 2 \? SHARDS : 2\)\)' 1
    _self_workflow "Ubuntu and Windows must use the default four-shard count" \
        plan "SHARDS: \\\$\\{\\{ inputs\\.verify_shard_count \\|\\| '4' \\}\\}" 1
    _self_workflow "each verify shard must record its wall time" \
        verify 'verify-timing' 1
    _self_workflow "Linux hyperfine must run only on Linux shard 1" \
        verify "if: runner\.os == 'Linux' && matrix\.shard == 1" 1
    _self_workflow "the verify matrix must run the gate per shard, so no shard can be dropped from the matrix" \
        verify 'scripts/verify\.sh --shard' 1
    _self_workflow "manual dispatch must support the unsharded baseline" \
        plan 'inputs\.verify_shard_count' 1
    _self_workflow "the verify leg must run on the full profile only" \
        verify "^    if: needs\.plan\.outputs\.profile == 'full'" 1
    _self_workflow "the OS legs must skip the platform checks, so one Linux job owns them" \
        verify 'VERIFY_SKIP_PLATFORM_CHECKS' 1
    _self_workflow "the platform job must run the platform profile" \
        platform 'scripts/verify\.sh --profile platform' 1
    _self_workflow "the platform job must run on the full profile only" \
        platform "^    if: needs\.plan\.outputs\.profile == 'full'" 1
    _self_workflow "the perf leg must run the dedicated startup measurement" \
        perf 'scripts/perf\.sh' 1
    _self_workflow "the perf leg must run on the full profile only" \
        perf "^    if: needs\.plan\.outputs\.profile == 'full'" 1
    _self_workflow "the perf leg must run only on the nightly" \
        perf "github\.event_name == 'schedule'" 1
    _self_workflow "the perf leg must not run for a release tag" \
        perf "github\.ref_type == 'tag'" 0
    _self_workflow "the PowerShell leg must be gated on the changed paths" \
        verify-powershell "needs\.changes\.outputs\.powershell == 'true'" 1
    _self_workflow "the changes job must decide through scripts/ci-powershell-paths.sh" \
        changes 'scripts/ci-powershell-paths\.sh' 1
    _self_workflow "the changelog job must run on pull requests only" \
        changelog "github\.event_name == 'pull_request'" 1
    _self_workflow "the changelog job must not run on a draft" \
        changelog 'draft == false' 1
    _self_workflow "the changelog job must run the committed guard" \
        changelog 'scripts/changelog-guard' 1
    _self_workflow "the changelog rule must stay inert until changes/ is on the base" \
        changelog 'git cat-file -e .*changes/README\.md' 1

    # Run concurrency: only a pull request may supersede its own older run, and the nightly is a
    # separate obligation from the push on the same branch. These assertions read the expression and
    # evaluate it per event, so a regression names the workflow and the event instead of only
    # comparing text.
    _self_concurrency "ci: a push to main is never cancelled" "$WORKFLOW" push-main \
        "ci-refs/heads/main" false
    _self_concurrency "ci: a pull request supersedes its own older run" "$WORKFLOW" pull_request \
        "ci-refs/pull/42/merge" true
    _self_concurrency "ci: a draft pull request has its own group" "$WORKFLOW" pull_request-draft \
        "ci-refs/pull/42/merge-draft" true
    _self_concurrency "ci: a ready pull request uses the ready group" "$WORKFLOW" pull_request-ready \
        "ci-refs/pull/42/merge" true
    _self_concurrency "ci: a merge group is never cancelled" "$WORKFLOW" merge_group \
        "ci-refs/heads/gh-readonly-queue/main/pr-42-abc" false
    _self_concurrency "ci: the nightly has its own group and cancels nothing" "$WORKFLOW" schedule \
        "ci-refs/heads/main-nightly" false
    _self_concurrency "ci: a manual dispatch is never cancelled" "$WORKFLOW" workflow_dispatch \
        "ci-refs/heads/main" false

    local docs_lint="${REPO_ROOT}/.github/workflows/docs-lint.yml"
    _self_concurrency "docs-lint: a push to main is never cancelled" "$docs_lint" push-main \
        "docs-lint-refs/heads/main" false
    _self_concurrency "docs-lint: only a pull request could supersede its own run" "$docs_lint" pull_request \
        "docs-lint-refs/pull/42/merge" true
    _self_concurrency "docs-lint: schedule cancels nothing" "$docs_lint" schedule \
        "docs-lint-refs/heads/main" false

    local pack="${REPO_ROOT}/.github/workflows/pack.yml"
    _self_concurrency "pack: a push to main is never cancelled" "$pack" push-main \
        "pack-refs/heads/main" false
    _self_concurrency "pack: a pull request supersedes its own older run" "$pack" pull_request \
        "pack-refs/pull/42/merge" true
    _self_concurrency "pack: schedule cancels nothing" "$pack" schedule \
        "pack-refs/heads/main" false

    if [ "$FAILURES" -ne 0 ]; then
        echo "self-test: FAIL: ${FAILURES} of ${CASES} checks failed" >&2
        return 1
    fi
    echo "self-test: OK (${CASES} checks)"
}

if [ "${1:-}" = "--self-test" ]; then
    self_test
    exit $?
fi

# The old env-only cases exercise the decision rules directly; production pull requests go through
# the live API path.
if [ "${CI_OK_FROM_ENV:-}" = "1" ]; then
    decide
    exit
fi

case "${EVENT_NAME:-}" in
    pull_request) _decide_live ;;
    *) decide ;;
esac
