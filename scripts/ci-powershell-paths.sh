#!/usr/bin/env bash
# Decides whether the PowerShell leg has to run for a change, so the Windows-only gate can stop
# running on every pull request while still covering everything that can break it.
#
# The rule is deny-by-default: a path runs the leg unless it is provably unrelated. It answers a
# narrower question than ci-plan.sh -- that script picks the whole gate, this one picks one leg.
#
# Usage:
#   ci-powershell-paths.sh --paths-file changed.txt     # prints powershell=true|false
#   ci-powershell-paths.sh --self-test
#
# changed.txt is `git diff --name-status -M` output (status<TAB>path, or status<TAB>old<TAB>new).
# Reads the changed files from the working tree, so the checkout must be the change under test.
#
# The Windows-specific source list is derived from the code, not from a hand-kept list: a changed
# .cs/.csproj/.props/.targets file is "Windows-specific" when it contains one of the markers below.
# Everything with a Windows branch in it is therefore covered, including a new file that nobody
# remembered to add to a list.

set -euo pipefail

SCRIPT_DIR="${CI_PATHS_SCRIPT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)}"
REPO_ROOT="${CI_PATHS_REPO_ROOT:-$(cd "${SCRIPT_DIR}/.." && pwd)}"

# Markers that make a source file Windows-specific. Keep this list short and literal: a marker that
# never appears in the code is noise, and a marker that appears everywhere (e.g. "Windows") would
# make every change run the leg.
MARKERS='OperatingSystem\.IsWindows|OSPlatform\.Windows|SupportedOSPlatform\("windows"\)|RuntimeInformation\.IsOSPlatform|#if WINDOWS|net10\.0-windows|RuntimeIdentifier>[[:space:]]*(win|windows)'

# Paths that always run the leg, whatever their content.
always_path() {
    case "$1" in
        install.ps1 | scripts/*.ps1 | scripts/**/*.ps1 | */scripts/*.ps1) return 0 ;;
        packaging/* | .github/workflows/*) return 0 ;;
        *.psm1) return 0 ;;
    esac
    return 1
}

windows_source() {
    case "$1" in
        *.cs | *.csproj | *.props | *.targets) ;;
        *) return 1 ;;
    esac
    [ -f "${REPO_ROOT}/$1" ] || return 1
    grep -Eq "$MARKERS" "${REPO_ROOT}/$1" 2>/dev/null
}

needs_powershell() {
    local status path old new
    while IFS=$'\t' read -r status path old new; do
        [ -n "${status:-}" ] || continue
        # A rename carries the destination in the third field; everything else in the second.
        case "$status" in
            R* | C*) path="${new:-$old}" ;;
        esac
        if always_path "$path"; then
            echo "powershell: ${path} is a path the leg always covers" >&2
            return 0
        fi
        if windows_source "$path"; then
            echo "powershell: ${path} carries a Windows-specific branch" >&2
            return 0
        fi
    done <"$1"
    return 1
}

self_test() {
    local cases=0 failures=0 tmp
    tmp="$(mktemp -d "${TMPDIR:-/tmp}/ci-ps-paths.XXXXXX")"
    trap 'rm -rf "$tmp"' RETURN

    mkdir -p "$tmp/repo/src/Lunate.Ai" "$tmp/repo/src/Lunate.Coding" "$tmp/repo/scripts" \
        "$tmp/repo/packaging/scoop" "$tmp/repo/.github/workflows" "$tmp/repo/docs"
    printf 'if (OperatingSystem.IsWindows()) { }\n' >"$tmp/repo/src/Lunate.Coding/ProcessRunner.cs"
    printf 'if (OperatingSystem.IsWindows()) { }\n' >"$tmp/repo/src/Lunate.Coding/Moved.cs"
    printf 'public static class Plain { }\n' >"$tmp/repo/src/Lunate.Ai/Plain.cs"
    printf 'name: CI\n' >"$tmp/repo/.github/workflows/ci.yml"
    printf '# prose\n' >"$tmp/repo/docs/guide.md"
    printf 'param()\n' >"$tmp/repo/scripts/install-test.ps1" 2>/dev/null || true
    mkdir -p "$tmp/repo/packaging/scoop"
    printf '{"name": "lunate"}\n' >"$tmp/repo/packaging/scoop/lunate.json"

    _case() {
        local name="$1" expected="$2" content="$3"
        printf '%b' "$content" >"$tmp/changed.txt"
        cases=$((cases + 1))
        local out
        out="$(CI_PATHS_REPO_ROOT="$tmp/repo" CI_PATHS_SCRIPT_DIR="$tmp/repo/scripts" \
            bash "${BASH_SOURCE[0]}" --paths-file "$tmp/changed.txt" 2>/dev/null || true)"
        case "$out" in
            *powershell=true*) got=true ;;
            *powershell=false*) got=false ;;
            *) got="no-verdict" ;;
        esac
        if [ "$got" != "$expected" ]; then
            echo "self-test: FAIL: ${name}: expected ${expected}, got ${got}" >&2
            failures=$((failures + 1))
        fi
    }

    _case "a PowerShell script" true 'A\tscripts/verify-test-runner.tests.ps1\n'
    _case "install.ps1" true 'A\tinstall.ps1\n'
    _case "a workflow change" true 'M\t.github/workflows/ci.yml\n'
    _case "a packaging change" true 'M\tpackaging/scoop/lunate.json\n'
    _case "a source file with a Windows branch" true 'M\tsrc/Lunate.Coding/ProcessRunner.cs\n'
    _case "a source file without one" false 'M\tsrc/Lunate.Ai/Plain.cs\n'
    _case "prose only" false 'M\tdocs/guide.md\n'
    _case "a rename into a Windows branch" true 'R100\tsrc/Old.cs\tsrc/Lunate.Coding/Moved.cs\n'
    _case "an empty change" false ''

    if [ "$failures" -ne 0 ]; then
        echo "self-test: FAIL: ${failures} of ${cases} checks failed" >&2
        return 1
    fi
    echo "self-test: OK (${cases} checks)"
}

mode=""
paths_file=""
while [ $# -gt 0 ]; do
    case "$1" in
        --self-test)
            mode="self-test"
            shift
            ;;
        --paths-file)
            paths_file="${2:-}"
            shift 2
            ;;
        --paths-file=*)
            paths_file="${1#*=}"
            shift
            ;;
        *)
            echo "ci-powershell-paths: unknown argument '$1'" >&2
            exit 2
            ;;
    esac
done

if [ "$mode" = "self-test" ]; then
    self_test
    exit $?
fi

if [ -z "$paths_file" ] || [ ! -f "$paths_file" ]; then
    echo "ci-powershell-paths: --paths-file is required and must exist" >&2
    exit 2
fi

if needs_powershell "$paths_file"; then
    echo "powershell=true"
else
    echo "powershell=false"
fi
