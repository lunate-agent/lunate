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
# from this run's stale `needs` values. A verify shard the head has no check run for was never a
# pass, so the live gate is red when a leg the plan put in the matrix is missing.
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
#   CHANGES      needs.changes.result
#   DOCS         needs.verify-docs.result
#   PERF         needs.perf.result
#   PLATFORM     needs.platform.result
#   FRAGMENTS    needs.changelog.result
#   PR_NUMBER    github.event.pull_request.number (named in the changelog failure)
#
# Exits 0 only when the run that actually happened is the gate the event and profile require. Every
# other outcome -- a draft, a skipped plan, a failed or skipped leg, a leg the event did not select,
# an unreadable profile -- exits 1 with a named reason.
#
# The event decides the job set (#831):
#
#   pull_request   plan, changes, changelog, platform (full) or verify-docs (docs), and the Ubuntu
#                  verify legs. No Windows, macOS, PowerShell or perf leg is required, and a skip of
#                  one of them is a pass. The plan puts Ubuntu legs only in a pull-request matrix.
#   merge_group    plan and the Ubuntu verify legs (or plan only for a docs batch). `changes`, the
#                  changelog guard, the platform checks, the docs profile, the PowerShell twin and
#                  the perf leg must be skipped: the pull request already proved them.
#   push/schedule/ the full matrix (macOS and Windows present), plan, changes, platform, and the
#   dispatch       PowerShell twin (schedule and dispatch also require the perf leg; a push's perf
#                  result is advisory). The nightly requires perf; a manual dispatch does not run it.
#
# The PowerShell leg is conditional on the changed paths only on the events that run it. Skipped is
# a pass when `changes` said its paths did not match, and a failure when nothing said so. An
# unreadable decision (PS_REQUIRED empty) is red, not "probably fine".

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
    local ps_required="${PS_REQUIRED:-}" changes="${CHANGES:-}" docs="${DOCS:-}" perf="${PERF:-}"
    local platform="${PLATFORM:-}" fragments="${FRAGMENTS:-}" pull_request="${PR_NUMBER:-}"
    local macos="${MACOS:-}"

    echo "ci-ok: event=$event draft=$draft plan=$plan profile=$profile macos=$macos verify=$verify verify-powershell=$powershell ps-required=$ps_required changes=$changes verify-docs=$docs perf=$perf platform=$platform changelog=$fragments"

    if [ "$event" = "pull_request" ] && [ "$draft" != "false" ]; then
        fail "the head is a draft (draft=$draft), so no gate ran; a draft push costs no matrix, but the required check still has to report. Mark the pull request ready for review and this check runs again."
    fi
    if [ "$plan" != "success" ]; then
        fail "the plan job did not succeed (plan=$plan)"
    fi

    # The jobs the pull request owns, and the two the merge group must not repeat.
    case "$event" in
        pull_request)
            if [ "$changes" != "success" ]; then
                fail "the changed-path decision did not succeed on a pull request (changes=$changes); the fast gate needs it"
            fi
            if [ "$fragments" != "success" ]; then
                fail "the changelog guard did not succeed (changelog=$fragments); a pull request other than the release pull request must not edit CHANGELOG.md. Write the line in changes/${pull_request:-<pull-request-number>}.md instead"
            fi
            ;;
        merge_group)
            if [ "$changes" != "skipped" ]; then
                fail "the changed-path decision ran in a merge group (changes=$changes); its pull requests already proved it"
            fi
            if [ "$fragments" != "skipped" ]; then
                fail "the changelog guard ran in a merge group (changelog=$fragments); its pull requests already proved it"
            fi
            ;;
        push | schedule | workflow_dispatch)
            if [ "$changes" != "success" ]; then
                fail "the changed-path decision did not succeed on a $event run (changes=$changes)"
            fi
            if [ "$fragments" != "skipped" ]; then
                fail "the changelog guard ran although only a pull request needs it (changelog=$fragments)"
            fi
            ;;
        *)
            fail "the event is unreadable ('$event'); refusing to pass on a state this gate does not know"
            ;;
    esac

    case "$profile" in
        docs)
            if [ "$verify" != "skipped" ] || [ "$powershell" != "skipped" ] || [ "$perf" != "skipped" ] || [ "$platform" != "skipped" ]; then
                fail "profile=docs, yet a full leg ran (verify=$verify, verify-powershell=$powershell, perf=$perf, platform=$platform)"
            fi
            case "$event" in
                merge_group)
                    # The pull request ran the docs profile on the same tree; the queue has nothing
                    # left to lint, so the docs leg must be absent too.
                    if [ "$docs" != "skipped" ]; then
                        fail "the docs profile ran in a merge group (verify-docs=$docs); its pull request already proved it"
                    fi
                    ;;
                *)
                    if [ "$docs" != "success" ]; then
                        fail "the docs profile did not succeed (verify-docs=$docs)"
                    fi
                    ;;
            esac
            ;;
        full)
            if [ "$verify" != "success" ]; then
                fail "a required job did not succeed (verify=$verify)"
            fi

            # The platform checks run on a pull request, a push, the nightly and a dispatch; the
            # merge queue spends its time on the tests instead.
            case "$event" in
                merge_group)
                    if [ "$platform" != "skipped" ]; then
                        fail "the platform checks ran in a merge group (platform=$platform); its pull request already proved them"
                    fi
                    ;;
                *)
                    if [ "$platform" != "success" ]; then
                        fail "a required job did not succeed (platform=$platform)"
                    fi
                    ;;
            esac

            if [ "$docs" != "skipped" ]; then
                fail "profile=full, yet the docs leg ran (verify-docs=$docs)"
            fi

            # The Windows twin belongs to the full matrix, not to the fast gate or the queue.
            case "$event" in
                pull_request | merge_group)
                    if [ "$powershell" != "skipped" ]; then
                        fail "the PowerShell twin ran on a $event run (verify-powershell=$powershell); it belongs to a push to main and the nightly"
                    fi
                    ;;
                *)
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
                    ;;
            esac

            # The dedicated perf leg runs on a push and the nightly. It is the nightly's verdict; a
            # push's timing does not feed this check.
            case "$event" in
                pull_request | merge_group)
                    if [ "$perf" != "skipped" ]; then
                        fail "the dedicated perf leg ran on a $event run (perf=$perf); it belongs to a push to main and the nightly"
                    fi
                    ;;
                schedule)
                    if [ "$perf" != "success" ]; then
                        fail "a required job did not succeed (perf=$perf)"
                    fi
                    ;;
                push)
                    : # Measurement only; a shared runner varies by a few percent.
                    ;;
                workflow_dispatch)
                    if [ "$perf" != "skipped" ]; then
                        fail "the dedicated perf leg ran on a manual dispatch (perf=$perf); it belongs to a push to main and the nightly"
                    fi
                    ;;
            esac

            # The full matrix is the three-OS coverage: only the merge queue (and a pull request,
            # whose own matrix is Ubuntu-only) may leave macOS out.
            case "$event:$macos" in
                pull_request:true | pull_request:false) ;;
                merge_group:false) ;;
                merge_group:true)
                    fail "the macOS verify legs ran in a merge group (macos=true); the queue runs Ubuntu only"
                    ;;
                push:true | schedule:true | workflow_dispatch:true) ;;
                push:false | schedule:false | workflow_dispatch:false)
                    fail "the macOS verify legs did not run on a $event run (macos=false); the full matrix belongs to main and the nightly"
                    ;;
                *)
                    fail "the plan's macOS decision is unreadable (macos='$macos'); refusing to pass on an unknown state"
                    ;;
            esac
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
# head with running jobs says so by name. A head that never carried a leg the plan put in the matrix
# is red too: the missing leg's absence is not another shard's pass.
_decide_live() {
    local state
    state="$(_live_state)"
    eval "$state"
    echo "ci-ok: live draft=$DRAFT plan=$PLAN profile=$PROFILE macos=$MACOS verify=$VERIFY verify-powershell=$POWERSHELL ps-required=$PS_REQUIRED changes=$CHANGES verify-docs=$DOCS perf=$PERF platform=$PLATFORM changelog=$FRAGMENTS missing=$VERIFY_MISSING"
    if [ "$DRAFT" = "true" ]; then
        decide
    fi
    if [ -n "${GATE_RUNNING:-}" ]; then
        fail "gate still running: ${GATE_RUNNING}; wait for those jobs to finish, then rerun ci-ok"
    fi
    if [ -n "${VERIFY_MISSING:-}" ]; then
        fail "the live gate is incomplete: this head has no check run for ${VERIFY_MISSING}; the plan's matrix requires every leg, so a leg that never ran cannot count as a pass. Rerun the CI workflow for this head."
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
    local fragments="${11:-}" macos="${12-default}" changes="${13:-}"
    if [ -z "$fragments" ]; then
        case "$1" in
            pull_request) fragments="success" ;;
            *) fragments="skipped" ;;
        esac
    fi
    # The plan's decision unless a case names one: Ubuntu only on a pull request and in a merge
    # group, the full three-OS matrix on every other event.
    if [ "$macos" = "default" ]; then
        case "$1" in
            pull_request | merge_group) macos="false" ;;
            *) macos="true" ;;
        esac
    fi
    # The changed-path decision runs on a pull request and a main event, never in a merge group.
    if [ -z "$changes" ]; then
        case "$1" in
            merge_group) changes="skipped" ;;
            *) changes="success" ;;
        esac
    fi
    env CI_OK_FROM_ENV=1 EVENT_NAME="$1" DRAFT="$2" PLAN="$3" PROFILE="$4" \
        VERIFY="$5" POWERSHELL="$6" DOCS="$7" PERF="$8" PLATFORM="$9" PS_REQUIRED="${10}" \
        FRAGMENTS="$fragments" MACOS="$macos" CHANGES="$changes" \
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

# The pull-request matrix the live decision requires must be the plan's own leg list: adding,
# renaming or removing a leg in the workflow has to reach scripts/ci-ok-state, or the live verdict
# would pass over a leg that never ran. The Windows and macOS legs are not compared: a pull request
# runs Ubuntu only (#831), so a live pull-request head has none.
_self_verify_matrix() {
    local name="$1" want got
    want="$(_job_block plan | sed -nE 's/^ +leg ([a-z0-9-]+) .*/\1/p' | sed -e '/^macos-latest$/d' -e '/^windows-latest$/d' | tr '\n' ' ' | sed 's/ $//')"
    got="$(python3 "${SCRIPT_DIR}/ci-ok-state" --print-verify-oses 2>&1)"
    CASES=$((CASES + 1))
    if [ "$want" = "$got" ]; then
        return 0
    fi
    echo "self-test: FAIL: ${name}: the plan's pull-request legs are [${want}], the live decision requires [${got}]" >&2
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
    # Rerunning an older *ready* run re-executes its jobs (its plan does not skip), so the newest
    # attempt of each required job is what decides: the head's ready gate and the rerun both pass,
    # or both fail.
    _self_live_case "a passed ready rerun on a passed head is green" 0 \
        "the full gate succeeded" "rerun-ready-passed-green"
    _self_live_case "a failed ready rerun on a failed head is red" 1 \
        "did not succeed" "rerun-ready-failed-red"
    _self_live_case "a ready gate with running jobs names them" 1 \
        "gate still running" "ready-still-running"
    # A gate that never carried a leg the plan put in the matrix is not a gate: the missing leg
    # cannot count as a pass just because the other shards succeeded (#830 review).
    _self_live_case "a gate missing one required verify shard is red" 1 \
        "verify (ubuntu-latest, 3/4)" "missing-shard-red"
    _self_live_case "a gate missing a whole verify OS is red" 1 \
        "any verify (ubuntu-latest, <shard>) leg" "missing-os-red"
    _self_live_case "a live draft is red even when an older run passed" 1 \
        "the head is a draft (draft=true)" "live-draft-red"
    # The fast gate's live head carries Ubuntu legs only; the Windows and macOS legs are not
    # required, and their absence is not a missing leg (#831).
    _self_live_case "a fast-gate head with Ubuntu legs only is green" 0 \
        "the full gate succeeded" "fast-gate-green"

    # The required context must never be skipped on a head that can be merged.
    _self_case "a draft pull request fails instead of skipping" 1 "draft" \
        pull_request true skipped "" skipped skipped skipped skipped "" ""
    _self_case "a draft with no job results still fails for the draft" 1 "draft" \
        pull_request true skipped "" "" "" "" "" "" ""
    _self_case "a pull request whose draft flag did not render fails closed" 1 "draft" \
        pull_request "" success full success success success skipped success success true

    # The profiles that can pass, per event (#831). The fast gate, the merge queue and the full
    # matrix are three different job sets, and each one has its own definition of a pass.
    _self_case "a pull request that passed the fast gate" 0 "the full gate succeeded" \
        pull_request false success full success skipped skipped skipped success false
    _self_case "a pull request whose path decision says the PowerShell leg would be needed" 0 "the full gate succeeded" \
        pull_request false success full success skipped skipped skipped success true
    _self_case "a pull request that also carried macOS legs (a dispatch on the same head) passes" 0 "the full gate succeeded" \
        pull_request false success full success skipped skipped skipped success true "" true
    _self_case "a docs pull request that passed" 0 "the docs gate succeeded" \
        pull_request false success docs skipped skipped success skipped skipped ""
    _self_case "a push to main that passed the full matrix" 0 "the full gate succeeded" \
        push "" success full success success skipped success success true
    _self_case "a push whose PowerShell leg was not required" 0 "the full gate succeeded" \
        push "" success full success skipped skipped success success false
    _self_case "a manual dispatch that passed" 0 "the full gate succeeded" \
        workflow_dispatch "" success full success success skipped skipped success true
    _self_case "the nightly that passed" 0 "the full gate succeeded" \
        schedule "" success full success success skipped success success true
    _self_case "a merge group that passed the Ubuntu gate" 0 "the full gate succeeded" \
        merge_group "" success full success skipped skipped skipped skipped "" "" false
    _self_case "a docs merge group runs only the plan" 0 "the docs gate succeeded" \
        merge_group "" success docs skipped skipped skipped skipped skipped "" "" false

    # Every way the run that happened can fail to be the gate:
    _self_case "a failed verify leg on a pull request" 1 "did not succeed" \
        pull_request false success full failure skipped skipped skipped success false
    _self_case "a skipped verify leg on the full profile" 1 "did not succeed" \
        pull_request false success full skipped skipped skipped success success false
    _self_case "a failed platform leg on a pull request" 1 "did not succeed" \
        pull_request false success full success skipped skipped skipped failure false
    _self_case "a skipped platform leg on a pull request" 1 "did not succeed" \
        pull_request false success full success skipped skipped skipped skipped false
    _self_case "a failed docs leg" 1 "did not succeed" \
        pull_request false success docs skipped skipped failure skipped skipped ""
    _self_case "a failed PowerShell leg on a push" 1 "was required by the changed paths" \
        push "" success full success failure skipped success success true
    _self_case "a pull request in which the PowerShell twin ran" 1 "belongs to a push to main" \
        pull_request false success full success success skipped skipped success true
    _self_case "a merge group in which the PowerShell twin ran" 1 "belongs to a push to main" \
        merge_group "" success full success success skipped skipped skipped "" "" false
    _self_case "a merge group in which the platform checks ran" 1 "already proved them" \
        merge_group "" success full success skipped skipped skipped success "" "" false
    _self_case "a merge group in which the changed-path decision ran" 1 "already proved it" \
        merge_group "" success full success skipped skipped skipped success "" "" false success
    _self_case "a merge group in which the changelog guard ran" 1 "already proved it" \
        merge_group "" success full success skipped skipped skipped success "" success false
    _self_case "a merge group in which the docs profile ran" 1 "already proved it" \
        merge_group "" success docs skipped skipped success skipped skipped "" "" false
    _self_case "a merge group in which the perf leg ran" 1 "belongs to a push to main" \
        merge_group "" success full success skipped skipped success skipped "" "" false
    _self_case "a merge group with a macOS leg" 1 "the queue runs Ubuntu only" \
        merge_group "" success full success skipped skipped skipped skipped "" "" true
    _self_case "a push without the platform checks" 1 "did not succeed" \
        push "" success full success success skipped success skipped true
    _self_case "a push without the changed-path decision" 1 "changed-path decision did not succeed" \
        push "" success full success success skipped success success true "" true failure
    _self_case "a pull request without the changed-path decision" 1 "changed-path decision did not succeed" \
        pull_request false success full success skipped skipped skipped success false "" false failure
    _self_case "a failed perf leg on a branch push never feeds this check" 0 "the full gate succeeded" \
        push "" success full success success skipped failure success true
    _self_case "a skipped perf leg on a branch push is not required" 0 "the full gate succeeded" \
        push "" success full success success skipped skipped success true
    _self_case "a failed perf leg on the nightly is the nightly verdict" 1 "did not succeed" \
        schedule "" success full success success skipped failure success true
    _self_case "a skipped perf leg on the nightly is red" 1 "did not succeed" \
        schedule "" success full success success skipped skipped success true
    _self_case "the perf leg ran on a pull request" 1 "belongs to a push to main" \
        pull_request false success full success skipped skipped success success false
    _self_case "the perf leg ran on a manual dispatch" 1 "belongs to a push to main" \
        workflow_dispatch "" success full success success skipped success success true
    _self_case "a failed platform leg on the nightly" 1 "did not succeed" \
        schedule "" success full success success skipped success failure true

    # The full matrix belongs to main and the nightly; only the fast gate and the queue leave macOS
    # out, and the plan must say so.
    _self_case "a push to main without macOS legs fails" 1 "macOS verify legs did not run on a push run" \
        push "" success full success success skipped success success true "" false
    _self_case "a nightly without macOS legs fails" 1 "macOS verify legs did not run on a schedule run" \
        schedule "" success full success success skipped success success true "" false
    _self_case "a pull request with an unreadable macOS decision fails" 1 "macOS decision is unreadable" \
        pull_request false success full success skipped skipped skipped success false "" ""
    _self_case "a docs gate does not consult the macOS decision" 0 "the docs gate succeeded" \
        merge_group "" success docs skipped skipped skipped skipped skipped "" "" ""

    # The PowerShell path decision is consulted only where the leg runs.
    _self_case "a PowerShell leg skipped on a push although the paths required it" 1 "was required by the changed paths" \
        push "" success full success skipped skipped success success true
    _self_case "a PowerShell leg skipped on a push with an unreadable decision" 1 "unreadable" \
        push "" success full success skipped skipped success success ""
    _self_case "a PowerShell leg cancelled on a push although required" 1 "was required by the changed paths" \
        push "" success full success cancelled skipped success success true
    _self_case "a PowerShell leg cancelled on a push although not required" 1 "did not pass either" \
        push "" success full success cancelled skipped success success false

    # Legs that ran although the event and profile did not select them.
    _self_case "the docs leg ran on a full profile" 1 "docs leg ran" \
        pull_request false success full success skipped success skipped success false
    _self_case "the matrix ran on a docs profile" 1 "full leg ran" \
        pull_request false success docs success skipped skipped skipped skipped ""
    _self_case "the perf leg ran on a docs profile" 1 "full leg ran" \
        pull_request false success docs skipped skipped success success skipped ""
    _self_case "the platform leg ran on a docs profile" 1 "full leg ran" \
        pull_request false success docs skipped skipped skipped success skipped ""
    _self_case "a skipped plan on a non-draft head" 1 "plan job did not succeed" \
        pull_request false skipped "" skipped skipped skipped skipped skipped "" ""
    _self_case "an unreadable profile" 1 "no usable profile" \
        pull_request false success "" skipped skipped skipped skipped skipped "" ""
    _self_case "an unreadable event fails closed" 1 "event is unreadable" \
        "" "" success full success skipped skipped skipped success ""

    # The changelog guard: required on every pull request, absent outside one.
    _self_case "a pull request whose changelog guard failed" 1 "changelog guard did not succeed" \
        pull_request false success full success skipped skipped skipped success false failure
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
    _self_workflow "Ubuntu must be in every event's matrix" \
        plan '^          leg ubuntu-latest .*"\$\{SHARDS\}"' 1
    _self_workflow "Windows must sit in the full-matrix branch, not in the fast gate" \
        plan '^            leg windows-latest .*"\$\{SHARDS\}"' 1
    _self_workflow "the full-matrix branch must be decided by one event flag" \
        plan 'if \[ "\$\{macos\}" = true \]; then' 1
    _self_verify_matrix "the live decision must require exactly the plan's pull-request legs"
    _self_workflow "the full matrix must be left out of a pull request and a merge group" \
        plan '^            pull_request \| merge_group\) macos=false ;;' 1
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
    _self_workflow "the platform checks must not run in a merge group" \
        platform "github\.event_name != 'merge_group'" 1
    _self_workflow "the changed-path decision must not run in a merge group" \
        changes "github\.event_name != 'merge_group'" 1
    _self_workflow "the docs profile must not run in a merge group" \
        verify-docs "github\.event_name != 'merge_group'" 1
    _self_workflow "the perf leg must run the dedicated startup measurement" \
        perf 'scripts/perf\.sh' 1
    _self_workflow "the perf leg must run on the full profile only" \
        perf "^    if: needs\.plan\.outputs\.profile == 'full'" 1
    _self_workflow "the perf leg must run on the nightly and a push to main" \
        perf "github\.event_name == 'schedule' \|\| github\.event_name == 'push'" 1
    _self_workflow "the perf leg must not run for a release tag" \
        perf "github\.ref_type == 'tag'" 0
    _self_workflow "the PowerShell leg must be gated on the changed paths" \
        verify-powershell "needs\.changes\.outputs\.powershell == 'true'" 1
    _self_workflow "the PowerShell leg must not run on a pull request or a merge group" \
        verify-powershell "github\.event_name != 'pull_request'" 1
    _self_workflow "the changed-path decision must keep its own script" \
        changes 'scripts/ci-powershell-paths\.sh' 1
    _self_workflow "the changelog job must run on pull requests only" \
        changelog "github\.event_name == 'pull_request'" 1
    _self_workflow "the changelog job must not run on a draft" \
        changelog 'draft == false' 1
    _self_workflow "the changelog job must run the committed guard" \
        changelog 'scripts/changelog-guard' 1
    _self_workflow "the changelog rule must stay inert until changes/ is on the base" \
        changelog 'git cat-file -e .*changes/README\.md' 1
    _self_workflow "ci-ok must read the changed-path decision" \
        ci-ok 'CHANGES: \$\{\{ needs\.changes\.result \}\}' 1
    _self_workflow "the nightly reporter must read the run's jobs, not needs" \
        nightly-issue 'actions/runs/\$\{RUN_ID\}/jobs' 1
    _self_workflow "the nightly reporter must file through scripts/nightly-issue.sh" \
        nightly-issue 'scripts/nightly-issue\.sh' 1
    _self_workflow "the nightly reporter must run only on the nightly" \
        nightly-issue "github\.event_name == 'schedule'" 1
    _self_workflow "the nightly reporter must run even when a leg failed" \
        nightly-issue '^    if: always\(\)' 1

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
