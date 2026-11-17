#!/usr/bin/env bash

detect_rid() {
  local label="${1:-rid}" os arch

  case "$(uname -s)" in
    Darwin) os="osx" ;;
    Linux) os="linux" ;;
    MINGW*|MSYS*|CYGWIN*) os="win" ;;
    *) echo "${label}: unsupported OS: $(uname -s); set RID explicitly, for example RID=linux-x64" >&2; return 1 ;;
  esac

  case "$(uname -m)" in
    arm64|aarch64) arch="arm64" ;;
    x86_64|amd64) arch="x64" ;;
    *) echo "${label}: unsupported architecture: $(uname -m); set RID explicitly, for example RID=linux-x64" >&2; return 1 ;;
  esac

  echo "${os}-${arch}"
}

# Parses a shard spec "i/n" and prints "<i> <n>" (both integers, 1 <= i <= n).
# Exits 2 on anything else, including an out-of-range shard: an unreachable
# shard (i > n, or n = 0) must fail loudly rather than silently run nothing.
# The gate scripts source this and call it with the value of --shard.
shard_spec_parse() {
  local spec="${1:-}" i n
  i="${spec%%/*}"
  n="${spec#*/}"
  if [ "$i" = "$spec" ] || [ "$n" = "$spec" ]; then
    echo "shard spec must be <i>/<n>, got '$spec'" >&2
    return 2
  fi
  case "$i" in
    "" | *[!0-9]* | 0) echo "shard index must be an integer 1..n, got '$i'" >&2; return 2 ;;
  esac
  case "$n" in
    "" | *[!0-9]* | 0) echo "shard count must be a positive integer, got '$n'" >&2; return 2 ;;
  esac
  if [ "$i" -gt "$n" ]; then
    echo "shard index $i is out of range (1..$n)" >&2
    return 2
  fi
  printf '%s %s\n' "$i" "$n"
}

# Fails when any existing PublicAPI.Shipped.txt is edited or deleted relative
# to the merge base with main; a newly added tracking file is the bootstrap
# path for a project that starts tracking its surface and does not trip the gate.
# The comparison base is the merge base with origin/main, then main, then HEAD
# (working tree only) when neither exists; the chosen base is printed on breach.
check_shipped_api_unchanged() {
  local base=""

  base="$(git merge-base HEAD origin/main 2>/dev/null)" || base=""
  if [ -z "$base" ]; then
    base="$(git merge-base HEAD main 2>/dev/null)" || base=""
  fi
  if [ -z "$base" ]; then
    base="HEAD"
    echo "note: no main ref found; comparing the working tree only" >&2
  fi

  if ! git diff --exit-code --diff-filter=MD "$base" -- '*PublicAPI.Shipped.txt'; then
    echo "verify: PublicAPI.Shipped.txt changed relative to ${base}" >&2
    return 1
  fi
}

# Names the step a failed gate run stopped in, for a reader who has only the run's check run.
#
# A red run on `main` is triaged from the API, not from a log: GitHub answers "Sign in to view logs"
# on the job page of a public run, while a step's `::error::` line is always in the check run's
# annotations. The gate printed its steps but named no failing one, so a card could only say
# "Process completed with exit code 1" (`verify (windows-latest, 1/4)` of runs 44033633019 and
# 44051817008). `status` 0 and an empty `step` -- a failure outside any step, in argument parsing --
# report nothing. `scripts/verify.sh` calls this from its EXIT trap, where `set -e` leaves the last
# step still known.
report_failed_step() {
  local step="${1:-}" status="${2:-}" elapsed="${3:-}"

  if [ -z "$step" ] || [ "$status" = "0" ]; then
    return 0
  fi
  printf '::error::verify: the %s step failed (exit %s, %ss)\n' "$step" "$status" "$elapsed"
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
    printf -- '- failed step: %s (exit %s, %ss)\n' "$step" "$status" "$elapsed" >> "$GITHUB_STEP_SUMMARY"
  fi
}
