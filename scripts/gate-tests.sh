#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "${SCRIPT_DIR}/lib.sh"

cd "${SCRIPT_DIR}/.."

RID="${RID:-$(detect_rid gate-tests)}"
BINARY="artifacts/publish/${RID}/lunate"
if [ "${RID#win}" != "$RID" ]; then
  BINARY="${BINARY}.exe"
fi

if [ ! -x "$BINARY" ]; then
  echo "gate-tests: published binary not found or not executable: $BINARY" >&2
  echo "gate-tests: run scripts/verify.sh first (or dotnet publish for RID=${RID})" >&2
  exit 1
fi

if ! dotnet csharpier --version >/dev/null 2>&1; then
  echo "gate-tests: csharpier not found; install it with: dotnet tool restore" >&2
  exit 1
fi

echo "gate-tests: perf.sh must fail with BUDGET_MS=1"
if BUDGET_MS=1 PERF_RUNS=5 PERF_WARMUP=1 PERF_RESULTS=artifacts/perf-gate-breach.json \
  "${SCRIPT_DIR}/perf.sh" "$BINARY" >/dev/null 2>&1; then
  echo "gate-tests: FAIL: perf.sh passed with BUDGET_MS=1; budget breach was not detected" >&2
  exit 1
fi

echo "gate-tests: perf.sh must pass with BUDGET_MS=999999"
if ! BUDGET_MS=999999 PERF_RUNS=5 PERF_WARMUP=1 PERF_RESULTS=artifacts/perf-gate-pass.json \
  "${SCRIPT_DIR}/perf.sh" "$BINARY" >/dev/null; then
  echo "gate-tests: FAIL: perf.sh failed with BUDGET_MS=999999" >&2
  exit 1
fi

# The measurement itself: hyperfine 2.x writes its statistics under
# .results[].summary.time_wall_clock, 1.x at the top level, and a JSON that fits
# neither once sailed through as an empty string and an empty comparison — a gate
# that could not read its own number passed. These three shims pin all of it down
# without needing the real hyperfine.
echo "gate-tests: perf.sh must read the hyperfine 2.x JSON, gate on its floor, and refuse a JSON it cannot read"
SHIM_DIR="$(mktemp -d)"
shim_hyperfine() {
  cat >"${SHIM_DIR}/hyperfine" <<'SHIM'
#!/usr/bin/env bash
if [ "$1" = "--version" ]; then
  echo "hyperfine 2.0.0"
  exit 0
fi
out=""
while [ $# -gt 0 ]; do
  case "$1" in
    --export-json)
      out="$2"
      shift 2
      ;;
    *) shift ;;
  esac
done
cat >"$out" <<'JSON'
{"results":[{"command":"x","summary":{"time_wall_clock":{"unit":"second","count":3,"mean":0.011,"stddev":0.001,"median":0.012,"min":0.010,"max":0.013}}}]}
JSON
exit 0
SHIM
  chmod +x "${SHIM_DIR}/hyperfine"
}
shim_hyperfine
if ! PATH="${SHIM_DIR}:$PATH" BUDGET_MS=100 PERF_RESULTS=artifacts/perf-shim-pass.json \
  "${SCRIPT_DIR}/perf.sh" "$BINARY" >/dev/null; then
  echo "gate-tests: FAIL: perf.sh did not read a hyperfine 2.x measurement" >&2
  rm -rf "$SHIM_DIR"
  exit 1
fi
if PATH="${SHIM_DIR}:$PATH" BUDGET_MS=5 PERF_RESULTS=artifacts/perf-shim-floor.json \
  "${SCRIPT_DIR}/perf.sh" "$BINARY" >/dev/null 2>&1; then
  echo "gate-tests: FAIL: perf.sh passed with a floor above the budget (10 ms against 5 ms)" >&2
  rm -rf "$SHIM_DIR"
  exit 1
fi
cat >"${SHIM_DIR}/hyperfine" <<'SHIM'
#!/usr/bin/env bash
if [ "$1" = "--version" ]; then
  echo "hyperfine 2.0.0"
  exit 0
fi
out=""
while [ $# -gt 0 ]; do
  case "$1" in
    --export-json)
      out="$2"
      shift 2
      ;;
    *) shift ;;
  esac
done
printf '{"results":[{"command":"x"}]}' >"$out"
exit 0
SHIM
chmod +x "${SHIM_DIR}/hyperfine"
if PATH="${SHIM_DIR}:$PATH" PERF_RESULTS=artifacts/perf-shim-unreadable.json \
  "${SCRIPT_DIR}/perf.sh" "$BINARY" >/dev/null 2>&1; then
  echo "gate-tests: FAIL: perf.sh passed without a readable measurement" >&2
  rm -rf "$SHIM_DIR"
  exit 1
fi
rm -rf "$SHIM_DIR"

FORMAT_DIR=""
CS15_DIR=""
SHIPPED_DIR=""
WAIT_DIR=""
SHARD_DIR=""
cleanup() {
  if [ -n "$FORMAT_DIR" ]; then
    rm -rf "$FORMAT_DIR"
  fi
  if [ -n "$CS15_DIR" ]; then
    rm -rf "$CS15_DIR"
  fi
  if [ -n "$SHIPPED_DIR" ]; then
    rm -rf "$SHIPPED_DIR"
  fi
  if [ -n "$WAIT_DIR" ]; then
    rm -rf "$WAIT_DIR"
  fi
  if [ -n "$SHARD_DIR" ]; then
    rm -rf "$SHARD_DIR"
  fi
}
trap cleanup EXIT
# Route signals into a plain exit so a killed run still fires the EXIT trap.
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

# Stale scratch sweep: a hard-killed run can leave a .gate-* dir in the repo
# root; remove ones older than 4 hours (a live run never lasts that long).
find "${SCRIPT_DIR}/.." -maxdepth 1 -type d -name '.gate-*' -mmin +240 -exec rm -rf {} + 2>/dev/null || true

echo "gate-tests: formatting gate must fail on an unformatted file"
FORMAT_DIR="$(mktemp -d "${SCRIPT_DIR}/../.gate-format.XXXXXX")"
printf 'class C { void M() { var x = 1; } }\n' >"${FORMAT_DIR}/Unformatted.cs"
if dotnet csharpier check "$FORMAT_DIR" >/dev/null 2>&1; then
  echo "gate-tests: FAIL: csharpier check passed for an unformatted file" >&2
  exit 1
fi

echo "gate-tests: formatting gate must pass on a formatted file"
rm "${FORMAT_DIR}/Unformatted.cs"
printf 'class C\n{\n    void M()\n    {\n        var x = 1;\n    }\n}\n' >"${FORMAT_DIR}/Formatted.cs"
if ! dotnet csharpier check "$FORMAT_DIR" >/dev/null; then
  echo "gate-tests: FAIL: csharpier check failed for a formatted file" >&2
  exit 1
fi

# C# 15 constructs (#901). The pinned CSharpier (`.config/dotnet-tools.json`,
# `rollForward: false`) is what fixes how union, closed, labeled-jump and
# collection-expression-argument syntax is laid out. A formatter below the pin rejects the
# syntax as a parse error, so the check exits non-zero; pin one sample of each construct
# here, and pin the trip too, so the gate reports this by exit code and not by a diff.
CS15_DIR="$(mktemp -d "${SCRIPT_DIR}/../.gate-cs15.XXXXXX")"
cs15_check() {
  local name="$1"
  local formatted="${CS15_DIR}/${name}.cs"
  if ! dotnet csharpier check "$formatted" >/dev/null; then
    echo "gate-tests: FAIL: the pinned CSharpier cannot format the C# 15 ${name} construct" >&2
    dotnet csharpier check "$formatted" >&2 || true
    exit 1
  fi
  mkdir -p "${CS15_DIR}/unformatted"
  sed 's/^/    /' "$formatted" >"${CS15_DIR}/unformatted/${name}.cs"
  if dotnet csharpier check "${CS15_DIR}/unformatted/${name}.cs" >/dev/null 2>&1; then
    echo "gate-tests: FAIL: csharpier check passed for an unformatted C# 15 ${name} file" >&2
    exit 1
  fi
}

cat >"${CS15_DIR}/union.cs" <<'CSHARP'
public union Pet(Cat, Dog);

public sealed record Cat(string Name);

public sealed record Dog(string Name);
CSHARP

cat >"${CS15_DIR}/closed.cs" <<'CSHARP'
public closed class Shape
{
    public string Name { get; init; } = "";
}
CSHARP

cat >"${CS15_DIR}/labeled-jump.cs" <<'CSHARP'
public static class Loops
{
    public static int Sum(int[][] rows)
    {
        var total = 0;
        rows:
        foreach (var row in rows)
        {
            foreach (var value in row)
            {
                if (value < 0)
                {
                    continue rows;
                }

                total += value;
            }
        }

        return total;
    }
}
CSHARP

cat >"${CS15_DIR}/collection-expression-arguments.cs" <<'CSHARP'
public static class Build
{
    public static int[] Make(int size) => [with(capacity: size), 1, 2, 3];
}
CSHARP

echo "gate-tests: formatting gate must pass on the C# 15 constructs and trip on their unformatted copies"
for cs15_name in union closed labeled-jump collection-expression-arguments; do
  cs15_check "$cs15_name"
done

echo "gate-tests: shipped-API gate must fail for an uncommitted Shipped change"
SHIPPED_DIR="$(mktemp -d "${SCRIPT_DIR}/../.gate-shipped.XXXXXX")"
git -C "$SHIPPED_DIR" init -q
git -C "$SHIPPED_DIR" branch -m main
git -C "$SHIPPED_DIR" config user.name "gate-tests"
git -C "$SHIPPED_DIR" config user.email "gate-tests@example.invalid"
mkdir -p "${SHIPPED_DIR}/src/P"
printf '#nullable enable\n' >"${SHIPPED_DIR}/src/P/PublicAPI.Shipped.txt"
git -C "$SHIPPED_DIR" add -A
git -C "$SHIPPED_DIR" commit -qm "base"
git -C "$SHIPPED_DIR" checkout -qb change
printf 'P.Type\n' >>"${SHIPPED_DIR}/src/P/PublicAPI.Shipped.txt"
if (cd "$SHIPPED_DIR" && . "${SCRIPT_DIR}/lib.sh" && check_shipped_api_unchanged) >/dev/null 2>&1; then
  echo "gate-tests: FAIL: shipped-API gate passed for an uncommitted Shipped change" >&2
  exit 1
fi

echo "gate-tests: shipped-API gate must fail for a committed Shipped change on a branch"
git -C "$SHIPPED_DIR" add -A
git -C "$SHIPPED_DIR" commit -qm "change"
if (cd "$SHIPPED_DIR" && . "${SCRIPT_DIR}/lib.sh" && check_shipped_api_unchanged) >/dev/null 2>&1; then
  echo "gate-tests: FAIL: shipped-API gate passed for a committed Shipped change on a branch" >&2
  exit 1
fi

echo "gate-tests: shipped-API gate must fail for a deleted Shipped file"
git -C "$SHIPPED_DIR" checkout -- src/P/PublicAPI.Shipped.txt
git -C "$SHIPPED_DIR" rm -q src/P/PublicAPI.Shipped.txt
git -C "$SHIPPED_DIR" commit -qm "delete shipped"
if (cd "$SHIPPED_DIR" && . "${SCRIPT_DIR}/lib.sh" && check_shipped_api_unchanged) >/dev/null 2>&1; then
  echo "gate-tests: FAIL: shipped-API gate passed for a deleted Shipped file" >&2
  exit 1
fi

echo "gate-tests: shipped-API gate must pass for a newly added tracking file"
git -C "$SHIPPED_DIR" checkout -q main
git -C "$SHIPPED_DIR" checkout -qb bootstrap
mkdir -p "${SHIPPED_DIR}/src/P2"
printf '#nullable enable\n' >"${SHIPPED_DIR}/src/P2/PublicAPI.Shipped.txt"
git -C "$SHIPPED_DIR" add -A
git -C "$SHIPPED_DIR" commit -qm bootstrap
if ! (cd "$SHIPPED_DIR" && . "${SCRIPT_DIR}/lib.sh" && check_shipped_api_unchanged) >/dev/null; then
  echo "gate-tests: FAIL: shipped-API gate failed for a newly added tracking file" >&2
  exit 1
fi

echo "gate-tests: shipped-API gate must pass on a clean main tree"
git -C "$SHIPPED_DIR" checkout -q main
if ! (cd "$SHIPPED_DIR" && . "${SCRIPT_DIR}/lib.sh" && check_shipped_api_unchanged) >/dev/null; then
  echo "gate-tests: FAIL: shipped-API gate failed on a clean main tree" >&2
  exit 1
fi

echo "gate-tests: wait-for-nuget.sh must wait for a version the fixture feed indexes late"
# A file:// URL is read by curl, which may be the native build: on Windows an MSYS path (/tmp/...)
# is not a path it can open, so every fixture URL is written the way Windows sees it.
fixture_url() {
  if command -v cygpath >/dev/null 2>&1; then
    printf 'file://%s\n' "$(cygpath -m "$1")"
  else
    printf 'file://%s\n' "$1"
  fi
}
wait_for_nuget="${SCRIPT_DIR}/wait-for-nuget.sh"
WAIT_DIR="$(mktemp -d)"
fc="${WAIT_DIR}/flatcontainer"
reg="${WAIT_DIR}/registration"
fixture_id="lunate.fixture.pkg"
fc_url="$(fixture_url "$fc")"
reg_url="$(fixture_url "$reg")"
mkdir -p "${fc}/${fixture_id}" "${reg}/${fixture_id}"
# The feed starts with an older version only: index, content and registration page all lack the
# version the run asks for.
printf '{"versions":["0.1.0"]}\n' >"${fc}/${fixture_id}/index.json"
printf '{"items":[{"@id":"%s/%s/page1.json","items":[{"catalogEntry":{"version":"0.1.0"}}]},' \
  "$reg_url" "$fixture_id" >"${reg}/${fixture_id}/index.json"
printf '{"@id":"%s/%s/page2.json","@type":"catalog:CatalogPage"}]}\n' \
  "$reg_url" "$fixture_id" >>"${reg}/${fixture_id}/index.json"
printf '{"items":[{"catalogEntry":{"version":"0.1.0"}}]}\n' >"${reg}/${fixture_id}/page2.json"
# The version appears while the script polls — index, then content, then the linked registration
# page — which is how nuget.org indexes a fresh push.
(
  sleep 2
  printf '{"versions":["0.1.0","0.2.0"]}\n' >"${fc}/${fixture_id}/index.json"
  mkdir -p "${fc}/${fixture_id}/0.2.0"
  printf 'nupkg\n' >"${fc}/${fixture_id}/0.2.0/${fixture_id}.0.2.0.nupkg"
  printf '{"items":[{"catalogEntry":{"version":"0.1.0"}},{"catalogEntry":{"version":"0.2.0"}}]}\n' \
    >"${reg}/${fixture_id}/page2.json"
) &
indexer=$!
wait_log="${WAIT_DIR}/wait.log"
wait_rc=0
bash "$wait_for_nuget" --package Lunate.Fixture.Pkg --version 0.2.0 \
  --budget-seconds 30 --interval-seconds 1 \
  --flatcontainer-base "$fc_url" --registration-base "$reg_url" >"$wait_log" 2>&1 || wait_rc=$?
wait "$indexer"
if [ "$wait_rc" -ne 0 ]; then
  echo "gate-tests: FAIL: wait-for-nuget.sh gave up on a version the feed indexed mid-budget:" >&2
  cat "$wait_log" >&2
  exit 1
fi
if ! grep -q 'not listed yet after' "$wait_log"; then
  echo "gate-tests: FAIL: wait-for-nuget.sh reported no wait, so the fixture was already indexed:" >&2
  cat "$wait_log" >&2
  exit 1
fi
if ! grep -q 'lists Lunate.Fixture.Pkg 0.2.0 after' "$wait_log"; then
  echo "gate-tests: FAIL: wait-for-nuget.sh does not name the package and version it waited for:" >&2
  cat "$wait_log" >&2
  exit 1
fi
if ! grep -q 'registration .*3 version(s) across 1 linked page(s)' "$wait_log"; then
  echo "gate-tests: FAIL: wait-for-nuget.sh did not read the linked registration page:" >&2
  cat "$wait_log" >&2
  exit 1
fi

echo "gate-tests: wait-for-nuget.sh must fail naming the feed state when the version never appears"
printf '{"versions":["0.1.0"]}\n' >"${fc}/${fixture_id}/index.json"
wait_rc=0
bash "$wait_for_nuget" --package Lunate.Fixture.Pkg --version 0.3.0 \
  --budget-seconds 3 --interval-seconds 1 \
  --flatcontainer-base "$fc_url" --registration-base "$reg_url" >"$wait_log" 2>&1 || wait_rc=$?
if [ "$wait_rc" -eq 0 ]; then
  echo "gate-tests: FAIL: wait-for-nuget.sh passed although the feed never listed the version" >&2
  cat "$wait_log" >&2
  exit 1
fi
for expected in 'did not list Lunate.Fixture.Pkg 0.3.0 within 3s' '0.3.0 absent' \
  'not checked (the index does not list the version)'; do
  if ! grep -qF "$expected" "$wait_log"; then
    echo "gate-tests: FAIL: wait-for-nuget.sh budget report does not name '$expected':" >&2
    cat "$wait_log" >&2
    exit 1
  fi
done

echo "gate-tests: wait-for-nuget.sh must not pass a listed version whose content is missing"
printf '{"versions":["0.1.0","0.4.0"]}\n' >"${fc}/${fixture_id}/index.json"
wait_rc=0
bash "$wait_for_nuget" --package Lunate.Fixture.Pkg --version 0.4.0 \
  --budget-seconds 3 --interval-seconds 1 \
  --flatcontainer-base "$fc_url" --registration-base "$reg_url" >"$wait_log" 2>&1 || wait_rc=$?
if [ "$wait_rc" -eq 0 ]; then
  echo "gate-tests: FAIL: wait-for-nuget.sh passed although the .nupkg was not downloadable" >&2
  cat "$wait_log" >&2
  exit 1
fi
if ! grep -qF 'the .nupkg is not downloadable' "$wait_log"; then
  echo "gate-tests: FAIL: wait-for-nuget.sh budget report does not name the missing content:" >&2
  cat "$wait_log" >&2
  exit 1
fi
rm -rf "$WAIT_DIR"
WAIT_DIR=""

echo "gate-tests: check-package-metadata.sh self-test must pass"
if ! bash "${SCRIPT_DIR}/check-package-metadata.sh" --self-test >/dev/null; then
  echo "gate-tests: FAIL: check-package-metadata.sh accepted or misreported an incomplete package" >&2
  exit 1
fi

echo "gate-tests: the shard spec parser must accept i/n and reject junk, zero and out-of-range"
. "${SCRIPT_DIR}/test-parallel.sh"
shard_got="$(shard_spec_parse '2/4')"
shard_i="${shard_got%% *}"
shard_n="${shard_got#* }"
if [ "$shard_i" != "2" ] || [ "$shard_n" != "4" ]; then
  echo "gate-tests: FAIL: shard_spec_parse '2/4' parsed as '$shard_got'" >&2
  exit 1
fi
for shard_bad in '2' '1/0' '0/2' '3/2' '4/2' 'a/2' '1/a' '1//2' '1/2/3' '' '  1/2'; do
  if shard_spec_parse "$shard_bad" >/dev/null 2>&1; then
    echo "gate-tests: FAIL: shard_spec_parse accepted '$shard_bad'" >&2
    exit 1
  fi
done

# One shard's slice of the real lunate.slnx for one culture pass, from the recorded weight table.
shard_slice() {
  SHARD_I="$1" SHARD_N="$2" verify_shard_projects "${3:-default}"
}

# One shard's predicted wall time, in the table's seconds, including shard 1's non-test pre-load.
shard_load() {
  SHARD_I="$1" SHARD_N="$2" verify_shard_loads | sed -n "s/^$1$(printf '\t')//p"
}

# Prints "<shard> <load> <lane total>" for the first shard over a third of its lane, nothing when every
# shard is within it. The real table and the fixture below go through this one function, so the
# self-test proves the arithmetic the gate enforces.
shard_over_a_third() {
  local shard_count="$1" shard_index total=0 load
  for shard_index in $(seq 1 "$shard_count"); do
    total=$((total + $(shard_load "$shard_index" "$shard_count")))
  done
  for shard_index in $(seq 1 "$shard_count"); do
    load="$(shard_load "$shard_index" "$shard_count")"
    if [ $((load * 3)) -gt "$total" ]; then
      printf '%s %s %s\n' "$shard_index" "$load" "$total"
      return 0
    fi
  done
  return 0
}

shard_fail() {
  echo "gate-tests: FAIL: $1" >&2
  exit 1
}

echo "gate-tests: the project list must one-to-one match the solution's test projects"
partition_all="$(LC_ALL=C sort < <(verify_test_projects))"
partition_count="$(printf '%s\n' "$partition_all" | wc -l | tr -d ' ')"
if [ "$partition_count" -eq 0 ] || [ "$partition_count" -ne "$(grep -o 'Tests\.csproj' lunate.slnx | wc -l | tr -d ' ')" ]; then
  shard_fail "verify_test_projects reads $partition_count projects; lunate.slnx has $(grep -o 'Tests\.csproj' lunate.slnx | wc -l | tr -d ' ') (post-#179 extraction regression?)"
fi

echo "gate-tests: no weight row may name a test project the solution does not have"
stale_rows="$(verify_shard_stale_rows)"
if [ -n "$stale_rows" ]; then
  shard_fail "scripts/verify-shard-weights.tsv names projects lunate.slnx no longer has:
${stale_rows}
Refresh it with: scripts/shard-weights.sh weights --from <dir of verify-timing artifacts> --out scripts/verify-shard-weights.tsv"
fi

echo "gate-tests: every culture pass must be partitioned, and every shard must own a unit, at 1 to 4 shards"
for shard_count in 1 2 3 4; do
  for shard_pass in default de-at; do
    shard_units_all=""
    for shard_index in $(seq 1 "$shard_count"); do
      shard_slice_one="$(shard_slice "$shard_index" "$shard_count" "$shard_pass")"
      if [ -z "$shard_slice_one" ]; then
        shard_fail "shard ${shard_index}/${shard_count} owns zero test projects in the ${shard_pass} pass"
      fi
      shard_units_all="${shard_units_all}${shard_slice_one}"$'\n'
    done
    shard_union="$(printf '%s' "$shard_units_all" | grep -v '^$' | LC_ALL=C sort)"
    if [ "$shard_union" != "$partition_all" ]; then
      echo "gate-tests: FAIL: shards 1/${shard_count}..${shard_count}/${shard_count} of the ${shard_pass} pass do not partition the test projects" >&2
      diff <(printf '%s\n' "$shard_union") <(printf '%s\n' "$partition_all") | head -20 >&2
      exit 1
    fi
    # Equal multisets: the sorted union has exactly one line per project, so no project is in two
    # shards and none is missing.
    if [ "$(printf '%s\n' "$shard_union" | grep -c .)" -ne "$partition_count" ]; then
      shard_fail "shards 1/${shard_count} of the ${shard_pass} pass assign a project twice"
    fi
  done
done

echo "gate-tests: the partition must be byte-identical across calls"
shard_plan_once="$(SHARD_I=1 SHARD_N=4 verify_shard_plan)"
shard_plan_twice="$(SHARD_I=1 SHARD_N=4 verify_shard_plan)"
if [ "$shard_plan_once" != "$shard_plan_twice" ]; then
  shard_fail "the four-shard plan is not deterministic"
fi

echo "gate-tests: at four shards no shard may carry more than a third of the lane's recorded weight"
lane_total=0
shard_loads=""
for shard_index in 1 2 3 4; do
  shard_load_one="$(shard_load "$shard_index" 4)"
  shard_loads="${shard_loads}${shard_index}:${shard_load_one}s "
  lane_total=$((lane_total + shard_load_one))
done
if [ "$lane_total" -le 0 ]; then
  shard_fail "the lane's recorded weight is $lane_total s - scripts/verify-shard-weights.tsv carries no weights"
fi
shard_over="$(shard_over_a_third 4)"
if [ -n "$shard_over" ]; then
  shard_fail "shard $(printf '%s' "$shard_over" | cut -d' ' -f1)/4 carries $(printf '%s' "$shard_over" | cut -d' ' -f2)s of the lane's $(printf '%s' "$shard_over" | cut -d' ' -f3)s, over a third (${shard_loads})"
fi

echo "gate-tests: every shard count must stay within the longest-processing-time bound"
for shard_count in 1 2 3 4; do
  lane_total=0
  for shard_index in $(seq 1 "$shard_count"); do
    lane_total=$((lane_total + $(shard_load "$shard_index" "$shard_count")))
  done
  for shard_index in $(seq 1 "$shard_count"); do
    shard_load_one="$(shard_load "$shard_index" "$shard_count")"
    if [ $((shard_load_one * 3 * shard_count)) -gt $((lane_total * (4 * shard_count - 1))) ]; then
      shard_fail "shard ${shard_index}/${shard_count} carries ${shard_load_one}s of a ${lane_total}s lane, beyond the longest-processing-time bound"
    fi
  done
done

echo "gate-tests: a project heavier than a shard's fair share must have its two culture passes in different shards"
heaviest_project=""
heaviest_weight=0
while IFS= read -r shard_project; do
  shard_weight_one="$(verify_shard_weight "$shard_project")"
  if [ "$shard_weight_one" -gt "$heaviest_weight" ]; then
    heaviest_weight="$shard_weight_one"
    heaviest_project="$shard_project"
  fi
done < <(verify_test_projects)
lane_total=0
for shard_index in 1 2 3 4; do
  lane_total=$((lane_total + $(shard_load "$shard_index" 4)))
done
if [ -n "$heaviest_project" ] && [ $((heaviest_weight * 4)) -gt "$lane_total" ]; then
  heaviest_default="$(SHARD_I=1 SHARD_N=4 verify_shard_plan | awk -F'\t' -v project="$heaviest_project" '$1 == "default" && $2 == project { print $3 }')"
  heaviest_deat="$(SHARD_I=1 SHARD_N=4 verify_shard_plan | awk -F'\t' -v project="$heaviest_project" '$1 == "de-at" && $2 == project { print $3 }')"
  if [ -z "$heaviest_default" ] || [ -z "$heaviest_deat" ]; then
    shard_fail "the heaviest project $heaviest_project has no unit in the four-shard plan"
  fi
  if [ "$heaviest_default" = "$heaviest_deat" ]; then
    shard_fail "the heaviest project $heaviest_project (${heaviest_weight}s a pass) has both culture passes in shard ${heaviest_default}"
  fi
fi

echo "gate-tests: a project with no weight row must be partitioned at the default weight, not dropped"
SHARD_DIR="$(mktemp -d "${SCRIPT_DIR}/../.gate-shard.XXXXXX")"
printf '#primary\t0\ntests/Lunate.Ai.Tests/Lunate.Ai.Tests.csproj\t7\n' >"${SHARD_DIR}/partial.tsv"
defaulted_weight="$(VERIFY_SHARD_WEIGHTS_FILE="${SHARD_DIR}/partial.tsv" verify_shard_weight tests/Lunate.Coding.Tests/Lunate.Coding.Tests.csproj)"
if [ "$defaulted_weight" != "$VERIFY_SHARD_DEFAULT_WEIGHT" ]; then
  shard_fail "a project without a row weighs ${defaulted_weight}s instead of the default ${VERIFY_SHARD_DEFAULT_WEIGHT}s"
fi
defaulted_units="$(VERIFY_SHARD_WEIGHTS_FILE="${SHARD_DIR}/partial.tsv" SHARD_I=1 SHARD_N=4 verify_shard_plan | grep -c . || true)"
if [ "$defaulted_units" -ne $((partition_count * 2)) ]; then
  shard_fail "a partial weight table planned $defaulted_units units for $((partition_count * 2)) expected"
fi
if [ ! -f "${SCRIPT_DIR}/verify-shard-weights.tsv" ]; then
  shard_fail "scripts/verify-shard-weights.tsv is missing; the partition would fall back to the default weight for every project"
fi

echo "gate-tests: a weight row naming a project the solution does not have is reported as stale"
printf '#primary\t0\ntests/Lunate.Gone.Tests/Lunate.Gone.Tests.csproj\t42\n' >"${SHARD_DIR}/stale.tsv"
if [ -z "$(VERIFY_SHARD_WEIGHTS_FILE="${SHARD_DIR}/stale.tsv" verify_shard_stale_rows)" ]; then
  shard_fail "a weight row naming a project lunate.slnx does not have was not reported as stale"
fi

echo "gate-tests: the balance check must reject a lane whose pole is a third of it, not wave it through"
# Every project weighs 1 s and the pole weighs 5000 s: its two culture passes still land in different
# shards, but one pass alone is then over a third of the lane, which no partition can fix - the check
# has to say so rather than report a partition it cannot balance.
{
  printf '#primary\t0\n'
  while IFS= read -r shard_project; do
    if [ "$shard_project" = "tests/Lunate.Coding.Tests/Lunate.Coding.Tests.csproj" ]; then
      printf 'tests/Lunate.Coding.Tests/Lunate.Coding.Tests.csproj\t5000\n'
    else
      printf '%s\t1\n' "$shard_project"
    fi
  done < <(verify_test_projects)
} >"${SHARD_DIR}/pole.tsv"
pole_over="$(VERIFY_SHARD_WEIGHTS_FILE="${SHARD_DIR}/pole.tsv" shard_over_a_third 4)"
if [ -z "$pole_over" ]; then
  shard_fail "the balance check accepted a lane whose pole is a third of it, so it cannot catch an unbalanced partition"
fi
pole_split="$(VERIFY_SHARD_WEIGHTS_FILE="${SHARD_DIR}/pole.tsv" SHARD_I=1 SHARD_N=4 verify_shard_plan |
  awk -F'\t' '$2 == "tests/Lunate.Coding.Tests/Lunate.Coding.Tests.csproj" { print $3 }' |
  LC_ALL=C sort -u | grep -c . || true)"
if [ "$pole_split" -ne 2 ]; then
  shard_fail "the pole's two culture passes were planned into ${pole_split} shard(s), not 2"
fi
if [ -n "$(shard_over_a_third 4)" ]; then
  shard_fail "the recorded table's own partition is over a third, which the check above should have failed on"
fi

echo "gate-tests: the shard module expression must be one built-assembly path per culture pass, joined with ';'"
for shard_pass in default de-at; do
  shard_modules_all="$(CONFIGURATION=Release SHARD_I=1 SHARD_N=1 verify_shard_modules "$shard_pass")"
  module_count="$(printf '%s\n' "$shard_modules_all" | tr ';' '\n' | grep -c .)"
  if [ "$module_count" -ne "$partition_count" ]; then
    shard_fail "the 1/1 ${shard_pass} module expression carries $module_count entries for $partition_count projects"
  fi
  case "$shard_modules_all" in
    *"/bin/Release/net11.0/Lunate.Tui.Tests.dll"*) ;;
    *)
      shard_fail "the ${shard_pass} module expression is not a built-assembly path per project: $shard_modules_all"
      ;;
  esac
  shard_modules_repeat="$(CONFIGURATION=Release SHARD_I=1 SHARD_N=1 verify_shard_modules "$shard_pass")"
  if [ "$shard_modules_all" != "$shard_modules_repeat" ]; then
    shard_fail "the ${shard_pass} module expression is not deterministic across calls"
  fi
  module_union_all="$(printf '%s\n' "$shard_modules_all" | tr ';' '\n' | LC_ALL=C sort)"
  for shard_count in 2 4; do
    module_union="$(for shard_index in $(seq 1 "$shard_count"); do
      CONFIGURATION=Release SHARD_I="$shard_index" SHARD_N="$shard_count" verify_shard_modules "$shard_pass" | tr ';' '\n'
    done | LC_ALL=C sort)"
    if [ "$module_union" != "$module_union_all" ]; then
      shard_fail "the ${shard_count}-shard ${shard_pass} module expressions do not cover the solution set"
    fi
  done
done

echo "gate-tests: the shard wall-time recorders write the shard, the project and the non-test time"
if ! grep -Fq 'shard-timing.sh' "${SCRIPT_DIR}/verify.sh"; then
  shard_fail "verify.sh does not invoke shard-timing.sh, so no shard records its wall time"
fi
if ! grep -Fq 'shard-weights.sh' "${SCRIPT_DIR}/verify.sh"; then
  shard_fail "verify.sh does not invoke shard-weights.sh, so no run records the per-project wall times the partition reads"
fi
if ! bash "${SCRIPT_DIR}/shard-timing.sh" --self-test >/dev/null; then
  shard_fail "shard-timing.sh self-test failed"
fi
if ! bash "${SCRIPT_DIR}/shard-weights.sh" --self-test >/dev/null; then
  shard_fail "shard-weights.sh self-test failed"
fi

echo "gate-tests: a shard that would own zero test projects must fail instead of running nothing"
for shard_pass in default de-at; do
  shard_rc=0
  ( CONFIGURATION=Release SHARD_I=25 SHARD_N=25 verify_shard_modules "$shard_pass" ) >/dev/null 2>&1 || shard_rc=$?
  if [ "$shard_rc" -eq 0 ]; then
    shard_fail "shard 25/25 produced a ${shard_pass} module expression for zero projects"
  fi
done

echo "gate-tests: OK"
