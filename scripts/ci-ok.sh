#!/usr/bin/env bash
# Decides the one check branch protection requires: `ci-ok`.
#
# A skipped required check reads as satisfied to branch protection. A draft pull request used to
# skip `ci-ok` too, so a head whose only run was the draft run could show "All checks have passed"
# and a live merge button with no gate behind it. A failing check cannot satisfy a required
# context, so the aggregate now always runs and reports a named failure while the head is a draft,
# while `plan` keeps the draft gate -- a draft push still costs no matrix.
#
# Usage:
#   ci-ok.sh            # decide from the environment (the CI step's env block)
#   ci-ok.sh --self-test
#
# Reads, all from the environment:
#   EVENT_NAME   github.event_name
#   DRAFT        github.event.pull_request.draft ("true"/"false"; empty off a pull request)
#   PLAN         needs.plan.result
#   PROFILE      needs.plan.outputs.profile
#   VERIFY       needs.verify.result
#   POWERSHELL   needs.verify-powershell.result
#   PS_REQUIRED  needs.changes.outputs.powershell ("true"/"false")
#   DOCS         needs.verify-docs.result
#   PERF         needs.perf.result
#   PLATFORM     needs.platform.result
#
# Exits 0 only when the run that actually happened is the gate the profile selected. Every other
# outcome -- a draft, a skipped plan, a failed or skipped leg, a leg the profile did not select, an
# unreadable profile -- exits 1 with a named reason.
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

    echo "ci-ok: event=$event draft=$draft plan=$plan profile=$profile verify=$verify verify-powershell=$powershell ps-required=$ps_required verify-docs=$docs perf=$perf platform=$platform"

    if [ "$event" = "pull_request" ] && [ "$draft" != "false" ]; then
        fail "the head is a draft (draft=$draft), so no gate ran; a draft push costs no matrix, but the required check still has to report. Mark the pull request ready for review and this check runs again."
    fi
    if [ "$plan" != "success" ]; then
        fail "the plan job did not succeed (plan=$plan)"
    fi
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
            if [ "$verify" != "success" ] || [ "$perf" != "success" ] || [ "$platform" != "success" ]; then
                fail "a required job did not succeed (verify=$verify, perf=$perf, platform=$platform)"
            fi
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
    env EVENT_NAME="$1" DRAFT="$2" PLAN="$3" PROFILE="$4" \
        VERIFY="$5" POWERSHELL="$6" DOCS="$7" PERF="$8" PLATFORM="$9" PS_REQUIRED="${10}" \
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

self_test() {
    CASES=0
    FAILURES=0

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
    _self_case "a skipped perf leg on the full profile" 1 "did not succeed" \
        pull_request false success full success success skipped skipped success true
    _self_case "a skipped platform leg on the full profile" 1 "did not succeed" \
        pull_request false success full success success skipped success skipped true

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
        ci-ok '^    needs: \[plan, changes, verify, verify-powershell, platform, verify-docs, perf\]' 1
    _self_workflow "the verify matrix must cover every OS leg" \
        verify 'os: \[ubuntu-latest, macos-latest, windows-latest\]' 1
    _self_workflow "Linux hyperfine must run only on Linux shard 1" \
        verify "if: runner\.os == 'Linux' && matrix\.shard == 1" 1
    _self_workflow "the verify matrix must run the gate per shard, so no shard can be dropped from the matrix" \
        verify 'scripts/verify\.sh --shard' 1
    _self_workflow "manual dispatch must support the unsharded baseline" \
        verify 'inputs\.verify_shard_count' 1
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
    _self_workflow "the PowerShell leg must be gated on the changed paths" \
        verify-powershell "needs\.changes\.outputs\.powershell == 'true'" 1
    _self_workflow "the changes job must decide through scripts/ci-powershell-paths.sh" \
        changes 'scripts/ci-powershell-paths\.sh' 1

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

decide
