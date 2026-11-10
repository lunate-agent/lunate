#!/usr/bin/env bash
# Records one verify shard's wall time, so a later change can see which shard is the pole without
# reading the runner logs line by line.
#
# Usage:
#   shard-timing.sh record --rid <rid> --shard <i> --shard-count <n> --profile <profile> \
#     --seconds <n> --status <exit> [--out-dir <dir>] [--summary <file>]
#   shard-timing.sh --self-test
#
# The JSON file goes to <out-dir>/verify-<rid>-<shard>-of-<count>.json (default
# artifacts/verify-timing). When --summary names a file -- CI passes GITHUB_STEP_SUMMARY -- a
# Markdown row is appended there as well.

set -euo pipefail

fail() {
  printf 'shard-timing: %s\n' "$1" >&2
  exit 2
}

record() {
  local rid="" shard="" shard_count="" profile="" seconds="" status=""
  local out_dir="artifacts/verify-timing" summary=""

  while [ $# -gt 0 ]; do
    case "$1" in
      --rid)
        rid="${2:-}"
        shift 2
        ;;
      --shard)
        shard="${2:-}"
        shift 2
        ;;
      --shard-count)
        shard_count="${2:-}"
        shift 2
        ;;
      --profile)
        profile="${2:-}"
        shift 2
        ;;
      --seconds)
        seconds="${2:-}"
        shift 2
        ;;
      --status)
        status="${2:-}"
        shift 2
        ;;
      --out-dir)
        out_dir="${2:-}"
        shift 2
        ;;
      --summary)
        summary="${2:-}"
        shift 2
        ;;
      --*)
        fail "unknown argument '$1' (usage: shard-timing.sh record --rid <rid> --shard <i> --shard-count <n> --profile <profile> --seconds <n> --status <exit> [--out-dir <dir>] [--summary <file>])"
        ;;
      *)
        fail "unexpected argument '$1'"
        ;;
    esac
  done

  case "$rid" in
    "" | *[!A-Za-z0-9._-]*) fail "--rid must name a runtime identifier, got '$rid'" ;;
  esac
  case "$profile" in
    "" | *[!a-z]*) fail "--profile must be a lowercase profile name, got '$profile'" ;;
    docs | full | platform) ;;
    *) fail "--profile must be docs, full or platform, got '$profile'" ;;
  esac
  case "$shard" in
    "" | *[!0-9]*) fail "--shard must be a positive integer, got '$shard'" ;;
  esac
  case "$shard_count" in
    "" | *[!0-9]*) fail "--shard-count must be a positive integer, got '$shard_count'" ;;
  esac
  case "$seconds" in
    "" | *[!0-9]*) fail "--seconds must be a non-negative integer, got '$seconds'" ;;
  esac
  case "$status" in
    "" | *[!0-9]*) fail "--status must be a non-negative integer, got '$status'" ;;
  esac
  if [ "$shard" -lt 1 ] || [ "$shard_count" -lt 1 ] || [ "$shard" -gt "$shard_count" ]; then
    fail "shard ${shard}/${shard_count} is not a reachable shard"
  fi

  mkdir -p "$out_dir"
  local file="${out_dir}/verify-${rid}-${shard}-of-${shard_count}.json"
  printf '{"rid":"%s","shard":%s,"shard_count":%s,"profile":"%s","seconds":%s,"exit_status":%s}\n' \
    "$rid" "$shard" "$shard_count" "$profile" "$seconds" "$status" >"$file"

  if [ -n "$summary" ]; then
    if [ ! -s "$summary" ]; then
      printf '### Verify shard wall time\n\n| OS | shard | profile | wall time | exit |\n| --- | --- | --- | --- | --- |\n' \
        >>"$summary"
    fi
    printf '| `%s` | %s/%s | %s | %ss | %s |\n' \
      "$rid" "$shard" "$shard_count" "$profile" "$seconds" "$status" >>"$summary"
  fi

  printf 'verify: wall time %ss (rid=%s shard=%s/%s profile=%s exit=%s)\n' \
    "$seconds" "$rid" "$shard" "$shard_count" "$profile" "$status"
  printf 'shard-timing: wrote %s\n' "$file"
}

_self_case() {
  local name="$1" expected="$2"
  shift 2
  local status=0
  "$@" >/dev/null 2>&1 || status=$?
  if [ "$status" -eq "$expected" ]; then
    return 0
  fi
  printf 'self-test: FAIL: %s: expected exit %s, got %s\n' "$name" "$expected" "$status" >&2
  return 1
}

_self_test_cases() {
  local dir="$1" file summary failures=0

  file="${dir}/out/verify-linux-x64-2-of-4.json"
  summary="${dir}/summary.md"
  if ! bash "$0" record --rid linux-x64 --shard 2 --shard-count 4 --profile full \
    --seconds 654 --status 0 --out-dir "${dir}/out" --summary "$summary" >/dev/null; then
    printf 'self-test: FAIL: a valid record was rejected\n' >&2
    return 1
  fi
  if [ ! -f "$file" ]; then
    printf 'self-test: FAIL: record did not write %s\n' "$file" >&2
    return 1
  fi
  if ! grep -Fq '"shard":2,"shard_count":4' "$file" ||
    ! grep -Fq '"seconds":654' "$file" ||
    ! grep -Fq '"exit_status":0' "$file"; then
    printf 'self-test: FAIL: record wrote unexpected contents: %s\n' "$(cat "$file")" >&2
    failures=$((failures + 1))
  fi
  if ! grep -Fq '| `linux-x64` | 2/4 | full | 654s | 0 |' "$summary"; then
    printf 'self-test: FAIL: summary row is missing or wrong: %s\n' "$(cat "$summary")" >&2
    failures=$((failures + 1))
  fi

  # A failed shard still records its wall time: the point is to find the pole even on a red run.
  if ! bash "$0" record --rid win-x64 --shard 4 --shard-count 4 --profile full \
    --seconds 981 --status 1 --out-dir "${dir}/out" --summary "$summary" >/dev/null; then
    printf 'self-test: FAIL: a failed shard was not recorded\n' >&2
    return 1
  fi
  if ! grep -Fq '"exit_status":1' "${dir}/out/verify-win-x64-4-of-4.json"; then
    printf 'self-test: FAIL: a non-zero exit status was not recorded\n' >&2
    failures=$((failures + 1))
  fi
  if ! grep -Fq '| `win-x64` | 4/4 | full | 981s | 1 |' "$summary"; then
    printf 'self-test: FAIL: the failed-shard summary row is missing\n' >&2
    failures=$((failures + 1))
  fi

  _self_case "an unreachable shard is refused" 2 \
    bash "$0" record --rid linux-x64 --shard 5 --shard-count 4 --profile full --seconds 1 --status 0 \
    --out-dir "${dir}/out" || failures=$((failures + 1))
  _self_case "a non-numeric time is refused" 2 \
    bash "$0" record --rid linux-x64 --shard 1 --shard-count 4 --profile full --seconds nope --status 0 \
    --out-dir "${dir}/out" || failures=$((failures + 1))
  _self_case "an unknown profile is refused" 2 \
    bash "$0" record --rid linux-x64 --shard 1 --shard-count 4 --profile slow --seconds 1 --status 0 \
    --out-dir "${dir}/out" || failures=$((failures + 1))

  return "$failures"
}

self_test() {
  local dir failures=0
  dir="$(mktemp -d "${TMPDIR:-/tmp}/lunate-shard-timing.XXXXXX")"
  _self_test_cases "$dir" || failures=$?
  rm -rf "$dir"
  if [ "$failures" -ne 0 ]; then
    printf 'self-test: FAIL: %s check(s) failed\n' "$failures" >&2
    return 1
  fi
  echo "self-test: OK"
}

if [ "${1:-}" = "--self-test" ]; then
  self_test
  exit $?
fi

if [ "${1:-}" != "record" ]; then
  fail "expected 'record' or '--self-test' (see the header for usage)"
fi
shift
record "$@"
