#!/usr/bin/env bash
# Test-phase runner for scripts/verify.sh (sourced, not executed).
#
# A solution-wide `dotnet test` already fans assemblies out inside one pass, but
# the gate runs that pass twice: once with the machine culture and once with
# de-AT. Those two passes are independent processes with independent
# environments, yet the second waited for the first and doubled the wall time.
# This runner starts both at once, each with its own bounded pool of JOBS
# slots, and reports each assembly's output in a deterministic order.
#
# OPT-IN: verify.sh uses this runner only when LUNATE_VERIFY_JOBS is 1 or more
# (2 is the sweet spot locally). The default is 0 — the solution-wide run the
# gate has always used — because three timing-sensitive suites (a 5 MB edit
# tripwire, a crashed-MCP state read, a remote wire timeout) flaked on the
# Windows CI runner under the pooled load.
#
# Assemblies that spawn child processes or manipulate process-wide state keep a
# serial lane: a serial job owns every slot of its pass, so no other assembly of
# the same pass runs beside it. Measurement on the reference Mac (Release,
# 2026-10-20): Lunate.Coding.Tests takes 3m46s alone and 3m47s while the whole
# suite runs, so its time is spent waiting on children rather than on CPU. The
# lane is kept for isolation of process/env-sensitive tests, not for timing.
# The default culture and de-AT variants of a serial assembly still run beside
# each other: they are separate processes with separate environments.
#
# The same run, 4045 tests per pass: the two passes took 3m53s and 3m47s back
# to back (7m40s total) before this runner, and 5m55s-5m58s together after it.
# The full gate (scripts/verify.sh) took 7m46s after the change.

# Test projects as the solution sees them, in a stable order. The built
# assembly check in verify.sh uses the same list, so a project that was not
# built cannot silently drop out of the gate.
#
# lunate.slnx is the SDK's XML solution format (#179): one `Project Path="..."`
# attribute per project, no GUID rows. gate-tests.sh pins the extraction: the
# list must be as long as the solution's .Tests.csproj entries.
verify_test_projects() {
  sed -n 's:.*<Project Path="\([^"]*\.Tests\.csproj\)" />.*:\1:p' lunate.slnx |
    tr '\\' '/' |
    LC_ALL=C sort
}

# --- The shard partition -----------------------------------------------------
#
# One unit per culture pass of one test project, placed largest recorded weight
# first into the least-loaded shard (longest processing time first). Two
# properties follow from that and are checked by scripts/gate-tests.sh:
#
#   * a project heavier than a shard's fair share has its default-culture and
#     de-AT passes in different shards, so no shard carries a whole slow project;
#   * no shard is over a third of the lane's recorded weight on the shard count
#     every lane runs (four), which gate-tests.sh reads from the workflow.
#
# The partition is computed inside every shard from the committed table alone, so
# the legs of a lane agree without talking to each other, and it is a pure
# function of (table, shard count): the same input prints the same plan on every
# platform. Weights never decide correctness - every unit is placed exactly once
# whatever the table says.

# The recorded weight table: one row per `lunate.slnx` test project with the
# wall time in seconds of ONE culture pass, plus a `#primary` row with the wall
# time shard 1 spends on its non-test steps. Overridable so the gate self-test
# can partition a fixture table.
VERIFY_SHARD_WEIGHTS_FILE="${VERIFY_SHARD_WEIGHTS_FILE:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/verify-shard-weights.tsv}"

# The weight of a project the table does not name yet: one recorded culture pass
# of a small test project. A change that adds a test project partitions it on
# this default until the table is refreshed, and never fails the gate.
VERIFY_SHARD_DEFAULT_WEIGHT="${VERIFY_SHARD_DEFAULT_WEIGHT:-60}"

# The culture passes, in the order the units are numbered.
VERIFY_SHARD_PASSES="default de-at"

VERIFY_SHARD_TAB="$(printf '\t')"

verify_shard_pass_valid() {
  case " ${VERIFY_SHARD_PASSES} " in
    *" ${1:-} "*) return 0 ;;
  esac
  echo "unknown culture pass '${1:-}' (expected one of: ${VERIFY_SHARD_PASSES})" >&2
  return 1
}

# Prints the table's rows as `<key><TAB><seconds>`; a missing table is empty.
verify_shard_table() {
  if [ ! -f "$VERIFY_SHARD_WEIGHTS_FILE" ]; then
    return 0
  fi
  grep -v '^[[:space:]]*$' "$VERIFY_SHARD_WEIGHTS_FILE" || true
}

# The recorded weight of one culture pass of a project, in seconds.
verify_shard_weight() {
  local project="$1" key value
  while IFS="$VERIFY_SHARD_TAB" read -r key value; do
    case "$key" in
      '' | '#'*) continue ;;
    esac
    if [ "$key" = "$project" ]; then
      printf '%s\n' "$value"
      return 0
    fi
  done <<EOF
$(verify_shard_table)
EOF
  printf '%s\n' "$VERIFY_SHARD_DEFAULT_WEIGHT"
}

# The recorded wall time of shard 1's non-test steps, in seconds (0 when untold).
verify_shard_primary_weight() {
  local key value
  while IFS="$VERIFY_SHARD_TAB" read -r key value; do
    if [ "$key" = "#primary" ]; then
      printf '%s\n' "$value"
      return 0
    fi
  done <<EOF
$(verify_shard_table)
EOF
  printf '0\n'
}

# Weight rows naming a project lunate.slnx does not have: a rename or a deletion
# the table has not followed. gate-tests.sh fails on a non-empty answer.
verify_shard_stale_rows() {
  local projects key value
  projects="$(verify_test_projects)"
  while IFS="$VERIFY_SHARD_TAB" read -r key value; do
    case "$key" in
      '' | '#'*) continue ;;
    esac
    if ! printf '%s\n' "$projects" | grep -Fqx -- "$key"; then
      printf '%s\n' "$key"
    fi
  done <<EOF
$(verify_shard_table)
EOF
}

# Every unit of the lane as `<recorded weight><TAB><pass><TAB><project>`.
verify_shard_units() {
  local project weight pass
  while IFS= read -r project; do
    weight="$(verify_shard_weight "$project")"
    for pass in $VERIFY_SHARD_PASSES; do
      printf '%s\t%s\t%s\n' "$weight" "$pass" "$project"
    done
  done < <(verify_test_projects)
}

# The plan: `<pass><TAB><project><TAB><shard>` in placement order. Units are
# sorted by recorded weight (descending), then project, then pass, so the plan
# is reproducible; each unit goes to the shard whose load is lowest, ties to the
# lowest shard number. Shard 1 starts at the `#primary` weight, because the leg
# that owns the publish, the budgets and the roundtrips pays for them on top of
# its tests.
#
# The heaviest unit of each culture pass is handed to the least-loaded shard
# first, once per shard, before the rest are placed. That seeding changes none of
# the balance (it is the first move the longest-processing-time rule would make
# anyway) and it guarantees the property the gate checks: with at least as many
# projects in a pass as shards, every shard runs something in both passes.
verify_shard_plan() {
  local weight pass project target shard index count=0 seeded seeded_shards
  local -a unit_weight=() unit_pass=() unit_project=() placed=() load=()

  while IFS="$VERIFY_SHARD_TAB" read -r weight pass project; do
    unit_weight[$count]="$weight"
    unit_pass[$count]="$pass"
    unit_project[$count]="$project"
    count=$((count + 1))
  done < <(verify_shard_units | LC_ALL=C sort -t"$VERIFY_SHARD_TAB" -k1,1nr -k3,3 -k2,2)

  load[1]="$(verify_shard_primary_weight)"
  for ((shard = 2; shard <= SHARD_N; shard++)); do
    load[$shard]=0
  done

  for pass in $VERIFY_SHARD_PASSES; do
    seeded=0
    seeded_shards=" "
    for ((index = 0; index < count; index++)); do
      [ "$seeded" -lt "$SHARD_N" ] || break
      [ "${unit_pass[$index]}" = "$pass" ] || continue
      target=""
      for ((shard = 1; shard <= SHARD_N; shard++)); do
        case "$seeded_shards" in
          *" $shard "*) continue ;;
        esac
        if [ -z "$target" ] || [ "${load[$shard]}" -lt "${load[$target]}" ]; then
          target=$shard
        fi
      done
      [ -n "$target" ] || break
      seeded_shards="${seeded_shards}${target} "
      load[$target]=$((load[$target] + ${unit_weight[$index]}))
      placed[$index]=1
      printf '%s\t%s\t%s\n' "$pass" "${unit_project[$index]}" "$target"
      seeded=$((seeded + 1))
    done
  done

  for ((index = 0; index < count; index++)); do
    [ -z "${placed[$index]:-}" ] || continue
    target=1
    for ((shard = 2; shard <= SHARD_N; shard++)); do
      if [ "${load[$shard]}" -lt "${load[$target]}" ]; then
        target=$shard
      fi
    done
    load[$target]=$((load[$target] + ${unit_weight[$index]}))
    printf '%s\t%s\t%s\n' "${unit_pass[$index]}" "${unit_project[$index]}" "$target"
  done
}

# The load each shard carries, as `<shard><TAB><seconds>`, including the
# `#primary` pre-load of shard 1: the partition's recorded weight, and the number
# the gate's balance check reads.
verify_shard_loads() {
  local weight pass project shard
  local -a load=()

  load[1]="$(verify_shard_primary_weight)"
  for ((shard = 2; shard <= SHARD_N; shard++)); do
    load[$shard]=0
  done
  while IFS="$VERIFY_SHARD_TAB" read -r pass project shard; do
    weight="$(verify_shard_weight "$project")"
    load[$shard]=$((load[$shard] + weight))
  done < <(verify_shard_plan)
  for ((shard = 1; shard <= SHARD_N; shard++)); do
    printf '%s\t%s\n' "$shard" "${load[$shard]}"
  done
}

# The projects this shard runs for one culture pass. SHARD_I/SHARD_N come from
# --shard (default 1/1, which owns every project and makes the plan a no-op).
verify_shard_projects() {
  local want="${1:-}" pass project shard
  verify_shard_pass_valid "$want" || return 1
  while IFS="$VERIFY_SHARD_TAB" read -r pass project shard; do
    if [ "$pass" = "$want" ] && [ "$shard" = "$SHARD_I" ]; then
      printf '%s\n' "$project"
    fi
  done < <(verify_shard_plan)
}

# The shard's test-module expression for `dotnet test --test-modules`: the built
# assembly of every project the shard owns for that culture pass, joined with
# semicolons (the separator the MTP dotnet test accepts for multiple patterns).
# Relative to the repository root, where verify.sh runs the command. Fails when
# the shard owns nothing - SHARD_N may exceed the unit count - because a shard
# that would run zero tests must never look green.
verify_shard_modules() {
  local pass="${1:-}" project expr
  verify_shard_pass_valid "$pass" || return 1
  while IFS= read -r project; do
    expr="${expr:+${expr};}${project%/*}/bin/${CONFIGURATION}/net11.0/$(basename "$project" .csproj).dll"
  done < <(verify_shard_projects "$pass")
  if [ -z "$expr" ]; then
    echo "shard ${SHARD_I}/${SHARD_N} owns zero test projects in the ${pass} pass - reachable shards only" >&2
    return 1
  fi
  printf '%s\n' "$expr"
}

# Filters verify_test_queue items ("<index>:<kind>:<project>") to the ones this
# shard runs for one culture pass. The queue item keeps its project index, so
# reporting stays in solution order, and the pooled runner and the module path
# read the same plan: they cannot disagree about which assembly belongs here.
verify_shard_queue() {
  local pass="${1:-}" item project owned
  verify_shard_pass_valid "$pass" || return 1
  owned="$(verify_shard_projects "$pass")"
  while IFS= read -r item; do
    project="${item#*:}"
    project="${project#*:}"
    if printf '%s\n' "$owned" | grep -Fqx -- "$project"; then
      printf '%s\n' "$item"
    fi
  done
}

verify_test_is_serial() {
  case " ${SERIAL_TEST_PROJECTS:-} " in
    *" $1 "*) return 0 ;;
  esac
  return 1
}

# Queue items are "<project-index>:<serial|parallel>:<project>". Reporting
# stays in project order by using the project index for filenames; the order
# knob only decides when the serial lane starts.
#
# The default pass runs its serial lane first, the de-AT pass runs it last.
# That staggers the two copies of a serial assembly: they overlap for part of
# their run instead of starting together, which keeps the wall-time win while
# halving the window in which the two heaviest assemblies compete for the
# machine.
verify_test_queue() {
  local order="${1:-serial-first}"
  if [ "$order" = "serial-last" ]; then
    verify_test_queue_part parallel
    verify_test_queue_part serial
  else
    verify_test_queue_part serial
    verify_test_queue_part parallel
  fi
}

verify_test_queue_part() {
  local want="$1" project name index=0

  while IFS= read -r project; do
    name="$(basename "${project%/*}")"
    if { [ "$want" = "serial" ] && verify_test_is_serial "$name"; } ||
      { [ "$want" = "parallel" ] && ! verify_test_is_serial "$name"; }; then
      printf '%s:%s:%s\n' "$index" "$want" "$project"
    fi
    index=$((index + 1))
  done < <(verify_test_projects)
}

# Runs in a subshell. The status file is written even when the test run fails,
# and the marker is the scheduling signal: bash 3.2 (macOS) has no `wait -n`,
# and a background process's PID remains a valid child until it is reaped.
verify_test_job() {
  local dir="$1" index="$2" project="$3" status
  set +e
  dotnet test --project "$project" -c "$CONFIGURATION" --no-build \
    --filter-not-trait Category=Perf >"${dir}/${index}.log" 2>&1
  status=$?
  set -e
  printf '%s\n' "$status" >"${dir}/${index}.status"
  : >"${dir}/${index}.done"
}

# Runs the queue with at most JOBS slots in use at a time. A serial item needs
# all JOBS slots, so it waits until the pass is idle and then runs alone.
verify_run_test_pass() {
  local dir="$1" rest item index kind project need
  shift
  local -a queue=("$@") pids=() slots=() job_index=()
  local next=0 active=0 i

  mkdir -p "$dir"
  while [ "$next" -lt "${#queue[@]}" ] || [ "$active" -gt 0 ]; do
    while [ "$next" -lt "${#queue[@]}" ]; do
      item="${queue[$next]}"
      index="${item%%:*}"
      rest="${item#*:}"
      kind="${rest%%:*}"
      project="${rest#*:}"

      need=1
      if [ "$kind" = "serial" ]; then
        need="$JOBS"
      fi
      if [ $((active + need)) -gt "$JOBS" ]; then
        break
      fi

      (verify_test_job "$dir" "$index" "$project") &
      pids[$next]=$!
      slots[$next]="$need"
      job_index[$next]="$index"
      active=$((active + need))
      next=$((next + 1))
    done

    if [ "$active" -eq 0 ]; then
      echo "verify: internal error: no test job could be started" >&2
      return 1
    fi

    sleep 0.1
    i=0
    while [ "$i" -lt "$next" ]; do
      if [ -n "${pids[$i]:-}" ] && [ -f "${dir}/${job_index[$i]}.done" ]; then
        wait "${pids[$i]}" 2>/dev/null || true
        active=$((active - ${slots[$i]}))
        pids[$i]=""
      fi
      i=$((i + 1))
    done
  done
}

# Prints one pass in project order and checks every assembly the shard runs for
# that pass: a non-zero exit fails the gate, and so does a missing or zero
# "total: N" summary. Failures accumulate in VERIFY_TEST_FAILURES so both passes
# report before the caller exits. The queue is recomputed here rather than taken
# from the caller so a sharded run checks its own slice and not the other shards'
# projects, which it never ran.
verify_check_test_pass() {
  local dir="$1" label="$2" order="$3" pass="$4"
  local item project log status_file status item_total index=0
  local total=0 assemblies=0 failures=0

  while IFS= read -r item; do
    index="${item%%:*}"
    project="${item#*:}"
    project="${project#*:}"
    log="${dir}/${index}.log"
    status_file="${dir}/${index}.status"
    if [ ! -f "$log" ] || [ ! -f "$status_file" ]; then
      echo "verify: ${label}: ${project} produced no result" >&2
      failures=$((failures + 1))
    else
      cat "$log"
      status="$(cat "$status_file")"
      item_total="$(grep -oE 'total: [0-9]+' "$log" | tail -n 1 | grep -oE '[0-9]+' || true)"
      if [ "$status" -ne 0 ]; then
        echo "verify: ${label} failed for ${project} (exit ${status})" >&2
        failures=$((failures + 1))
      elif [ -z "$item_total" ] || [ "$item_total" -eq 0 ]; then
        echo "verify: ${label} executed zero tests for ${project} — test discovery regression?" >&2
        failures=$((failures + 1))
      else
        total=$((total + item_total))
        assemblies=$((assemblies + 1))
      fi
    fi
  done < <(verify_test_queue "$order" | verify_shard_queue "$pass")

  printf 'verify: %s: %s tests across %s assemblies\n' "$label" "$total" "$assemblies"
  VERIFY_TEST_FAILURES=$((VERIFY_TEST_FAILURES + failures))
}
