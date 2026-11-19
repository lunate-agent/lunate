#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "${SCRIPT_DIR}/lib.sh"

cd "${SCRIPT_DIR}/.."

BUDGET_MS="${BUDGET_MS:-150}"
RUNS="${PERF_RUNS:-20}"
WARMUP="${PERF_WARMUP:-3}"
RESULTS_FILE="${PERF_RESULTS:-artifacts/perf.json}"

BINARY="${1:-}"
if [ -z "$BINARY" ]; then
  RID="${RID:-$(detect_rid perf)}"
  BINARY="artifacts/publish/${RID}/lunate"
  if [ "${RID#win}" != "$RID" ]; then
    BINARY="${BINARY}.exe"
  fi
fi

if [ ! -x "$BINARY" ]; then
  echo "perf: binary not found or not executable: $BINARY" >&2
  exit 1
fi

if ! "$BINARY" --version >/dev/null 2>&1; then
  echo "perf: preflight failed: cannot run '$BINARY --version'" >&2
  exit 1
fi

mkdir -p "$(dirname "$RESULTS_FILE")"
RESULTS_ABS="$(cd "$(dirname "$RESULTS_FILE")" && pwd)/$(basename "$RESULTS_FILE")"
# hyperfine executes commands through the system shell (sh on Unix, cmd.exe on
# Windows). cmd.exe cannot run POSIX-style relative paths such as ./name, so on
# Windows convert to the absolute Windows path (backslash form) via cygpath.
BINARY_ABS="$(cd "$(dirname "$BINARY")" && pwd)/$(basename "$BINARY")"
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*)
    if command -v cygpath >/dev/null 2>&1; then
      HYPERFINE_CMD="$(cygpath -w "$BINARY_ABS") --version"
    else
      HYPERFINE_CMD="$BINARY_ABS --version"
    fi
    ;;
  *)
    HYPERFINE_CMD="$BINARY_ABS --version"
    ;;
esac

echo "perf: benchmarking: $HYPERFINE_CMD"
echo "perf: $(hyperfine --version)"

# The gate is the FLOOR: the fastest of the runs, printed beside the median for
# context (ADR-0005 addendum, 2026-10-25). Interference on a shared runner only
# ever adds time, so the fastest run estimates what the binary costs and the
# median estimates what the machine was doing; a budget taken on the median was
# reading the runner's thermal and scheduler state (identical code measured 81 to
# 364 ms on macOS legs). A real regression sits on the startup path and moves the
# floor with it, so nothing is hidden by the change.
measure_startup() {
  hyperfine --warmup "$WARMUP" --runs "$RUNS" --export-json "$RESULTS_ABS" "$HYPERFINE_CMD" >/dev/null
  # Two JSON shapes are in the wild: hyperfine 2.x writes the statistics under
  # .results[].summary.time_wall_clock, 1.x writes .min/.median at the top level.
  # Read whichever the installed version wrote — runner images and developer
  # machines drift (the macOS runner already carries 2.0.0 while a dev machine can
  # still have 1.20). A shape that fits neither is an error, never a silent pass.
  jq -r '
    .results[0] as $result
    | ($result | .min // .summary.time_wall_clock.min // (if .times then (.times | min) else null end)) as $floor
    | ($result | .median // .summary.time_wall_clock.median) as $median
    | if $floor == null or $median == null then
        error("perf: the hyperfine JSON carries no wall-clock timing")
      else
        "\($floor * 1000 | round) \($median * 1000 | round)"
      end
  ' "$RESULTS_ABS"
}

# Names a failing measurement in the run's check run and its job summary, for the reader a red
# `perf (macos-latest)` sends to the run rather than to the log: GitHub answers "Must have admin
# rights" on a job's log for anyone without a session, while every `::error::` line of a step is in
# the check run's annotations. `perf (macos-latest)` of run 44466042157 failed with nothing but
# "Process completed with exit code 1", so its card could not say whether the floor was 305 ms (the
# shared macOS runner's known noise) or a doubled startup (#950). The measurement is the only thing that
# knows, and `perf.sh` printed it into that log alone.
report_failure() {
  local detail="$1"

  printf '::error::perf: %s\n' "$detail"
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    printf -- '- perf: %s\n' "$detail" >> "$GITHUB_STEP_SUMMARY"
  fi
}

# One measurement, its floor into FLOOR_MS and its median into MEDIAN_MS. `label` names the attempt in
# the printed line. A measurement that cannot be read fails the step — never a silent pass — and is
# named in the annotation too, because a broken measurement is as unattributable as a breach.
measure_once() {
  local label="${1:-}" floor median

  read -r floor median <<<"$(measure_startup || true)"
  if ! [[ "${floor:-}" =~ ^[0-9]+$ && "${median:-}" =~ ^[0-9]+$ ]]; then
    local detail="could not read the startup measurement (floor='${floor:-}', median='${median:-}'"
    if [ -n "$label" ]; then
      detail="${detail}; ${label}"
    fi
    detail="${detail})"
    echo "perf: ${detail}" >&2
    report_failure "$detail"
    exit 1
  fi
  printf 'startup: floor %s ms (median %s ms, budget %s ms%s)\n' \
    "$floor" "$median" "$BUDGET_MS" "${label:+; $label}"
  FLOOR_MS="$floor"
  MEDIAN_MS="$median"
}

FIRST_FLOOR_MS=""
measure_once

# Noise policy (ADR-0005): a single budget miss re-runs the measurement once
# before failing, so a shared-runner noise burst does not redden the gate while
# a material regression still misses both attempts. Consecutive misses are a
# re-calibration (or regression) signal.
if [ "$FLOOR_MS" -gt "$BUDGET_MS" ]; then
  echo "startup floor ${FLOOR_MS} ms exceeds budget ${BUDGET_MS} ms; re-running once (noise policy)"
  FIRST_FLOOR_MS="$FLOOR_MS"
  measure_once retry
fi

if [ "$FLOOR_MS" -gt "$BUDGET_MS" ]; then
  breach="startup budget exceeded: fastest of ${RUNS} runs ${FLOOR_MS} ms > ${BUDGET_MS} ms (median ${MEDIAN_MS} ms"
  if [ -n "$FIRST_FLOOR_MS" ]; then
    breach="${breach}; first attempt ${FIRST_FLOOR_MS} ms"
  fi
  breach="${breach})"
  echo "${breach}" >&2
  # `STARTUP_BUDGET_ENFORCE=0` is a leg that prints the measurement without failing on it
  # (ci.yml's verify matrix, where the shared macOS runner's floor is bimodal); its breach is not
  # the step's verdict, so it must not leave an error annotation on a green job.
  if [ "${STARTUP_BUDGET_ENFORCE:-1}" != "0" ]; then
    report_failure "${breach}"
  fi
  exit 1
fi
