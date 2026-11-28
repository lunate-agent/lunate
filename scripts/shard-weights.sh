#!/usr/bin/env bash
# Records the wall time of each test project a verify shard ran, and folds a lane's records into the
# weight table the shard partition reads (#789).
#
# Usage:
#   shard-weights.sh record --rid <rid> --shard <i> --shard-count <n> --pass <default|de-at> \
#     [--out-dir <dir>] < pass-output.log
#   shard-weights.sh weights --from <dir> [--table <file>] [--out <file>] [--primary <seconds>]
#   shard-weights.sh --self-test
#
# `record` reads the test platform's own per-module summary out of one culture pass of a gate run -
# `<path>/<Name>.Tests.dll (net11.0|arm64) passed [+N/x0/?M] (3s 445ms)` - and writes one JSON line per
# project to <out-dir>/verify-<rid>-<shard>-of-<count>-<pass>-projects.jsonl, beside the shard's own
# wall-time record (scripts/shard-timing.sh). The culture pass is part of the name because a shard runs
# both of them: one file per (shard, pass) keeps both records, and the fold takes the slower.
#
# `weights` reads every shard's project records and shard records in a directory and prints the weight
# table scripts/verify-shard-weights.tsv: one row per test project with the SLOWER of its two recorded
# culture passes, plus the `#primary` row with the longest non-test time shard 1 recorded. Rows the run
# did not cover keep their value from --table, so a one-shard refresh cannot drop the other shards'
# projects.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SELF="${SCRIPT_DIR}/$(basename "${BASH_SOURCE[0]}")"
TABLE="${SCRIPT_DIR}/verify-shard-weights.tsv"

# strip_ansi, so a colored pass output parses the same as a plain one.
. "${SCRIPT_DIR}/lib.sh"

fail() {
  printf 'shard-weights: %s\n' "$1" >&2
  exit 2
}

TAB="$(printf '\t')"

# The weight table's own text, so a refresh regenerates the file a reviewer reads rather than a bare
# list of numbers.
table_preamble() {
  local source="$1" recorded="$2"
  cat <<EOF
# Lunate verify shard weights - the recorded wall time of ONE culture pass of each \`lunate.slnx\`
# test project, in seconds, plus the \`#primary\` row: the wall time shard 1 spends on its non-test
# steps (tools, build, publish, startup budget, install/tool/extension roundtrips, the two
# Category=Perf budgets). scripts/test-parallel.sh reads this file and nothing else to partition the
# test phase: one unit per (project, culture pass), placed largest recorded weight first into the
# least-loaded shard, so both culture passes of a heavy project land in different shards (#789).
#
# A project with no row here is partitioned at the default weight (VERIFY_SHARD_DEFAULT_WEIGHT), so
# adding a test project does not fail the gate; a row naming a project lunate.slnx no longer has fails
# scripts/gate-tests.sh, so a rename or a deletion is what asks for a refresh.
#
# Refresh from a real run's artifacts - the per-project records a gate run writes beside its shard
# record - rather than by estimating:
#   scripts/shard-weights.sh weights --from <dir holding verify-timing artifacts> \\
#     --out scripts/verify-shard-weights.tsv
#
# Recorded from $source on $recorded.
EOF
}

# Milliseconds of a test platform duration ("799ms", "3s 445ms", "1m 2s 7ms").
parse_ms() {
  local value="$1" ms=0 unit
  while [ -n "$value" ]; do
    case "$value" in
      *m' '*)
        unit="${value%%m *}"
        ms=$((ms + unit * 60000))
        value="${value#*m }"
        ;;
      *s' '*)
        unit="${value%%s *}"
        ms=$((ms + unit * 1000))
        value="${value#*s }"
        ;;
      *ms)
        unit="${value%ms}"
        ms=$((ms + unit))
        value=""
        ;;
      *) value="" ;;
    esac
  done
  printf '%s\n' "$ms"
}

record() {
  local rid="" shard="" shard_count="" pass="" out_dir="artifacts/verify-timing"
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
      --pass)
        pass="${2:-}"
        shift 2
        ;;
      --out-dir)
        out_dir="${2:-}"
        shift 2
        ;;
      --*)
        fail "unknown argument '$1' (usage: shard-weights.sh record --rid <rid> --shard <i> --shard-count <n> --pass <default|de-at> [--out-dir <dir>])"
        ;;
      *)
        fail "unexpected argument '$1'"
        ;;
    esac
  done

  case "$rid" in
    "" | *[!A-Za-z0-9._-]*) fail "--rid must name a runtime identifier, got '$rid'" ;;
  esac
  case "$pass" in
    default | de-at) ;;
    *) fail "--pass must be default or de-at, got '$pass'" ;;
  esac
  case "$shard" in
    "" | *[!0-9]*) fail "--shard must be a positive integer, got '$shard'" ;;
  esac
  case "$shard_count" in
    "" | *[!0-9]*) fail "--shard-count must be a positive integer, got '$shard_count'" ;;
  esac
  if [ "$shard" -lt 1 ] || [ "$shard_count" -lt 1 ] || [ "$shard" -gt "$shard_count" ]; then
    fail "shard ${shard}/${shard_count} is not a reachable shard"
  fi

  # The summary block repeats each module line with leading spaces, so the same module can appear
  # twice: the last value per project wins and the records are sorted by project, which keeps the file
  # stable across runs. The runner colors the line when it believes it is on CI
  # (`GITHUB_ACTIONS=true`), escapes and all, even with the pass redirected to a file, so the escapes
  # come off first: they sit after the duration and around the verdict, and a parser that keeps them
  # matches no line at all - which is how every CI run recorded its shard but no project (#1043).
  local parsed
  parsed="$(strip_ansi | awk -v root="${PWD}/" '
    /\.dll \(/ && /\) (passed|failed) / {
      line = $0
      if (match(line, /\(([0-9]+m )?([0-9]+s )?[0-9]+ms\)[[:space:]]*$/) == 0) {
        next
      }
      duration = substr(line, RSTART + 1, RLENGTH - 2)
      module = line
      sub(/ \(.*/, "", module)
      sub(/^[[:space:]]+/, "", module)
      if (index(module, root) == 1) {
        module = substr(module, length(root) + 1)
      }
      project = module
      sub(/\/bin\/.*/, "", project)
      name = project
      sub(/.*\//, "", name)
      duration_by_project[project "/" name ".csproj"] = duration
    }
    END {
      for (project in duration_by_project) {
        print project "\t" duration_by_project[project]
      }
    }' | LC_ALL=C sort)"

  if [ -z "$parsed" ]; then
    fail "no per-module result found in the ${pass} pass output - the test platform summary changed shape? (see the header)"
  fi

  mkdir -p "$out_dir"
  local file="${out_dir}/verify-${rid}-${shard}-of-${shard_count}-${pass}-projects.jsonl"
  : >"$file"
  local project duration ms seconds count=0
  while IFS="$TAB" read -r project duration; do
    ms="$(parse_ms "$duration")"
    seconds=$(((ms + 999) / 1000))
    printf '{"rid":"%s","shard":%s,"shard_count":%s,"pass":"%s","project":"%s","seconds":%s}\n' \
      "$rid" "$shard" "$shard_count" "$pass" "$project" "$seconds" >>"$file"
    count=$((count + 1))
  done <<<"$parsed"
  printf 'shard-weights: wrote %s (%s projects, %s pass)\n' "$file" "$count" "$pass"
}

weights() {
  local from="" table="$TABLE" out="" primary=""
  while [ $# -gt 0 ]; do
    case "$1" in
      --from)
        from="${2:-}"
        shift 2
        ;;
      --table)
        table="${2:-}"
        shift 2
        ;;
      --out)
        out="${2:-}"
        shift 2
        ;;
      --primary)
        primary="${2:-}"
        shift 2
        ;;
      --*)
        fail "unknown argument '$1' (usage: shard-weights.sh weights --from <dir> [--table <file>] [--out <file>] [--primary <seconds>])"
        ;;
      *)
        fail "unexpected argument '$1'"
        ;;
    esac
  done
  if [ -z "$from" ] || [ ! -d "$from" ]; then
    fail "--from must name the directory holding a run's verify-timing artifacts"
  fi
  case "${primary:-0}" in
    *[!0-9]*) fail "--primary must be a non-negative integer, got '$primary'" ;;
  esac

  # Recorded project weights: the slower of the two culture passes wins, because the partition has to
  # compensate for the pass that hurts.
  local recorded=""
  if compgen -G "${from}/*-projects.jsonl" >/dev/null; then
    recorded="$(cat "${from}"/*-projects.jsonl | sed -n 's/.*"project":"\([^"]*\)","seconds":\([0-9][0-9]*\).*/\1\t\2/p')"
  fi
  if [ -z "$recorded" ]; then
    fail "no per-project records under $from — was the run sharded by scripts/verify.sh with scripts/test-parallel.sh?"
  fi

  # The recorded non-test time of shard 1, from the shard records that passed. Only a green shard's
  # split between test and non-test time means anything.
  local recorded_primary=""
  if compgen -G "${from}/verify-*-of-*.json" >/dev/null; then
    recorded_primary="$(grep -h '"exit_status":0' "${from}"/verify-*-of-*.json 2>/dev/null |
      sed -n 's/.*"primary":\([0-9][0-9]*\).*/\1/p' | LC_ALL=C sort -n | tail -n 1 || true)"
  fi
  if [ -z "$primary" ]; then
    primary="${recorded_primary:-}"
  fi
  if [ -z "$primary" ]; then
    primary="$(sed -n 's/^#primary\t\([0-9][0-9]*\)$/\1/p' "$table" 2>/dev/null | head -n 1 || true)"
  fi
  if [ -z "$primary" ]; then
    fail "no recorded non-test time: pass --primary <seconds> or keep a #primary row in $table"
  fi

  # Merge: recorded values win, rows the run did not cover keep the table's value.
  local old_rows=""
  if [ -f "$table" ]; then
    old_rows="$(grep -v '^[[:space:]]*$' "$table" | grep -v '^#' || true)"
  fi
  local merged
  merged="$(
    {
      printf '%s\n' "$old_rows"
      printf '%s\n' "$recorded"
    } | grep -v '^[[:space:]]*$' | LC_ALL=C sort -t"$TAB" -k1,1 -k2,2nr |
      awk -F'\t' '$1 != last { print; last = $1 }'
  )"

  local rendered
  rendered="$(
    table_preamble "the verify-timing records under $from" "$(date +%Y-%m-%d)"
    printf '#primary%s%s\n' "$TAB" "$primary"
    printf '%s\n' "$merged"
  )"

  if [ -n "$out" ]; then
    printf '%s\n' "$rendered" >"$out"
    printf 'shard-weights: wrote %s (%s project rows, #primary %ss)\n' \
      "$out" "$(printf '%s\n' "$merged" | grep -c . || true)" "$primary"
  else
    printf '%s\n' "$rendered"
  fi
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
  local dir="$1" failures=0 file record out

  local work="${dir}/work/lunate"
  mkdir -p "$work"
  file="${dir}/run.log"
  cat >"$file" <<LOG
Running tests from ${work}/tests/Lunate.Coding.Tests/bin/Release/net11.0/Lunate.Coding.Tests.dll (net11.0|arm64)
  failed Lunate.Coding.Tests.Slow.Child_process_times_out
${work}/tests/Lunate.Coding.Tests/bin/Release/net11.0/Lunate.Coding.Tests.dll (net11.0|arm64) failed [+218/x1/?3] (7m 2s 9ms)
${work}/tests/Lunate.Ai.Tests/bin/Release/net11.0/Lunate.Ai.Tests.dll (net11.0|arm64) passed [+481/x0/?10] (3s 445ms)
${work}/tests/Mermaid.Terminal.Tests/bin/Release/net11.0/Mermaid.Terminal.Tests.dll (net11.0|arm64) passed [+72/x0/?0] (799ms)
  ${work}/tests/Lunate.Ai.Tests/bin/Release/net11.0/Lunate.Ai.Tests.dll (net11.0|arm64) passed [+481/x0/?10] (3s 445ms)
  total: 772
  duration: 7m 8s
LOG
  if ! (cd "$work" && bash "$SELF" record --rid linux-x64 --shard 2 --shard-count 4 --pass default \
    --out-dir "${dir}/out" <"$file" >/dev/null); then
    printf 'self-test: FAIL: a readable pass output was rejected\n' >&2
    return 1
  fi
  record="${dir}/out/verify-linux-x64-2-of-4-default-projects.jsonl"
  if [ ! -f "$record" ]; then
    printf 'self-test: FAIL: record wrote no per-project file\n' >&2
    return 1
  fi
  if ! grep -Fq '{"rid":"linux-x64","shard":2,"shard_count":4,"pass":"default","project":"tests/Lunate.Coding.Tests/Lunate.Coding.Tests.csproj","seconds":423}' "$record"; then
    printf 'self-test: FAIL: a duration was not recorded in seconds: %s\n' "$(cat "$record")" >&2
    failures=$((failures + 1))
  fi
  if ! grep -Fq '"project":"tests/Mermaid.Terminal.Tests/Mermaid.Terminal.Tests.csproj","seconds":1' "$record"; then
    printf 'self-test: FAIL: a sub-second module was recorded as zero: %s\n' "$(cat "$record")" >&2
    failures=$((failures + 1))
  fi
  if [ "$(grep -c . "$record")" -ne 3 ]; then
    printf 'self-test: FAIL: the repeated summary line produced a duplicate record: %s\n' "$(cat "$record")" >&2
    failures=$((failures + 1))
  fi

  # The same shard, its other culture pass: a shard runs both, so each pass gets its own record. The
  # de-AT record must not replace the default one, or the fold can never see the slower pass.
  printf '%s\n' \
    "${work}/tests/Lunate.Coding.Tests/bin/Release/net11.0/Lunate.Coding.Tests.dll (net11.0|arm64) passed [+221/x0/?0] (5m 0s 0ms)" \
    >"${dir}/run2.log"
  if ! (cd "$work" && bash "$SELF" record --rid linux-x64 --shard 2 --shard-count 4 --pass de-at \
    --out-dir "${dir}/out" <"${dir}/run2.log" >/dev/null); then
    printf 'self-test: FAIL: the de-AT pass output was rejected\n' >&2
    return 1
  fi
  if [ ! -f "${dir}/out/verify-linux-x64-2-of-4-de-at-projects.jsonl" ]; then
    printf 'self-test: FAIL: the de-AT pass did not get its own record beside the default pass\n' >&2
    failures=$((failures + 1))
  fi
  records="$(ls "${dir}/out" | grep -c -- '-projects.jsonl$' || true)"
  if [ "$records" -ne 2 ]; then
    printf 'self-test: FAIL: one shard wrote %s project records for its two culture passes\n' "$records" >&2
    failures=$((failures + 1))
  fi

  # The runner colors its output when it knows it is on CI (`GITHUB_ACTIONS=true`), even with the pass
  # redirected to a file, so the module summary line carries escape sequences around the verdict and
  # after the duration. A parser anchored on the duration at the end of the line then matches nothing
  # and no run records a weight; this is the shape that made every CI artifact carry only the shard's
  # own record (#1043).
  printf '\033[m%s (net11.0|arm64) \033[32mpassed\033[m [\033[32m+481\033[m/\033[31mx0\033[m/\033[33m?10\033[m] \033[90m(3s 445ms)\033[m\n' \
    "${work}/tests/Lunate.Ai.Tests/bin/Release/net11.0/Lunate.Ai.Tests.dll" >"${dir}/colored.log"
  if ! (cd "$work" && bash "$SELF" record --rid linux-x64 --shard 1 --shard-count 4 --pass default \
    --out-dir "${dir}/out-colored" <"${dir}/colored.log" >/dev/null 2>&1); then
    printf 'self-test: FAIL: a colored pass output was rejected\n' >&2
    failures=$((failures + 1))
  elif ! grep -Fq '"project":"tests/Lunate.Ai.Tests/Lunate.Ai.Tests.csproj","seconds":4' \
    "${dir}/out-colored/verify-linux-x64-1-of-4-default-projects.jsonl" 2>/dev/null; then
    printf 'self-test: FAIL: a colored pass output recorded no project weight\n' >&2
    failures=$((failures + 1))
  fi
  printf '{"rid":"linux-x64","shard":1,"shard_count":4,"profile":"full","seconds":600,"exit_status":0,"primary":120}\n' \
    >"${dir}/out/verify-linux-x64-1-of-4.json"
  printf '{"rid":"linux-x64","shard":2,"shard_count":4,"profile":"full","seconds":500,"exit_status":1,"primary":999}\n' \
    >"${dir}/out/verify-linux-x64-2-of-4.json"
  printf 'tests/Lunate.Gone.Tests/Lunate.Gone.Tests.csproj\t42\n' >"${dir}/old-table.tsv"

  out="${dir}/weights.tsv"
  if ! bash "$SELF" weights --from "${dir}/out" --table "${dir}/old-table.tsv" --out "$out" >/dev/null; then
    printf 'self-test: FAIL: weights refused a readable artifact directory\n' >&2
    return 1
  fi
  if ! grep -Fq "#primary${TAB}120" "$out"; then
    printf 'self-test: FAIL: the recorded non-test time of the green shard was not written: %s\n' "$(head -20 "$out")" >&2
    failures=$((failures + 1))
  fi
  if ! grep -q "^tests/Lunate.Coding.Tests/Lunate.Coding.Tests.csproj${TAB}423$" "$out"; then
    printf 'self-test: FAIL: the slower culture pass did not win\n' >&2
    failures=$((failures + 1))
  fi
  if ! grep -Fq "Lunate.Ai.Tests.csproj${TAB}4" "$out"; then
    printf 'self-test: FAIL: a project only the default pass ran is missing\n' >&2
    failures=$((failures + 1))
  fi
  if ! grep -Fq "Lunate.Gone.Tests.csproj${TAB}42" "$out"; then
    printf 'self-test: FAIL: a row the run did not cover was dropped\n' >&2
    failures=$((failures + 1))
  fi

  mkdir -p "${dir}/empty"
  _self_case "an empty artifact directory is refused" 2 \
    bash "$SELF" weights --from "${dir}/empty" || failures=$((failures + 1))
  _self_case "a missing artifact directory is refused" 2 \
    bash "$SELF" weights --from "${dir}/absent" || failures=$((failures + 1))
  _self_case "a non-numeric primary weight is refused" 2 \
    bash "$SELF" weights --from "${dir}/out" --primary slow || failures=$((failures + 1))
  _self_case "an unknown culture pass is refused" 2 \
    bash "$SELF" record --rid linux-x64 --shard 1 --shard-count 4 --pass german <"$file" \
    || failures=$((failures + 1))
  _self_case "output without a module summary is refused" 2 \
    bash -c "printf 'total: 0\\n' | bash \"$SELF\" record --rid linux-x64 --shard 1 --shard-count 4 --pass default" \
    || failures=$((failures + 1))
  _self_case "an unreachable shard is refused" 2 \
    bash "$SELF" record --rid linux-x64 --shard 5 --shard-count 4 --pass default <"$file" \
    || failures=$((failures + 1))

  return "$failures"
}

self_test() {
  local dir failures=0
  dir="$(mktemp -d "${TMPDIR:-/tmp}/lunate-shard-weights.XXXXXX")"
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

case "${1:-}" in
  record)
    shift
    record "$@"
    ;;
  weights)
    shift
    weights "$@"
    ;;
  *)
    fail "expected 'record', 'weights' or '--self-test' (see the header for usage)"
    ;;
esac
