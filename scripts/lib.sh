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

# The tests a failing test step named, one per line, in the order the runner printed them: the
# `failed Namespace.Type.Method (7ms)` line the Microsoft.Testing.Platform runner writes, with its
# duration stripped, plus the `[FAIL]` and `xunit.v3 ... failed` shapes that have appeared in the gate's
# output before. Anything else -- a build or restore failure, a module that never started -- names no
# test, and prints nothing rather than a guess. Every output handed in is read, in argument order, and a
# name two attempts both printed appears once.
#
# The same three shapes are what record_flake reads and what scripts/verify-test-runner.ps1 reads, so the
# ledger, the flake line and the annotation agree about which test failed.
failed_test_names() {
  local path

  for path in "$@"; do
    if [ -z "$path" ] || [ ! -f "$path" ]; then
      continue
    fi
    grep -E '^[[:space:]]*(failed|xunit\.v3[^ ]* failed|\[FAIL\])[[:space:]]+' "$path" |
      sed -E -e 's/^[[:space:]]*//' \
        -e 's/^(failed|xunit\.v3[^ ]* failed|\[FAIL\])[[:space:]]+//' \
        -e 's/[[:space:]]+\(.*\)$//' \
        -e 's/[[:space:]]*$//' || true
  done
}

# The failing modules of a red pass, one per line: the
# `.../Lunate.Coding.Tests.dll (net11.0|arm64) failed with 1 error(s) [+2949/x0/?1] (6m 28s 855ms)` line
# `dotnet test` writes per module, cut back to the file name so a check run carries no runner path. A
# module whose session aborted or crashed has no failing test to name, and this line is then the whole
# attribution its output holds: the counters say how many passed, failed and aborted. A module both
# attempts reported is named once, from the output handed in first.
failed_modules() {
  local path

  for path in "$@"; do
    if [ -z "$path" ] || [ ! -f "$path" ]; then
      continue
    fi
    grep -E '\.dll \([^)]*\) failed with ' "$path" | sed -E 's:^.*/([^/]+\.dll \(.*)$:\1:' || true
  done | awk '!seen[$1]++'
}

# The verdict a red pass printed when it named no test: every Microsoft.Testing.Platform run ends with
# `Test run completed with non-success exit code: N. <reason>`, and a pass that never got that far -- a
# host that crashed, a module that never started -- points at its cause with an `error:` line. The
# exit-code link the line ends with is dropped: it is the same URL on every red and says nothing about
# this one. An output that holds neither is left alone rather than guessed at.
failed_pass_verdict() {
  local output="${1:-}"

  if [ -z "$output" ] || [ ! -f "$output" ]; then
    return 0
  fi
  grep -E '^[[:space:]]*(error:|Test run completed with non-success exit code:)' "$output" |
    head -n 1 |
    sed -E -e 's/^[[:space:]]*//' -e 's/[[:space:]]+See: https?:\/\/[^[:space:]]+$//' || true
}

# Names the failing tests of a red test step in the run's check run, for the same reader and the same
# reason as report_failed_step: a job's log on a public run needs an account, a step's `::error::` line
# does not. Naming the step alone still left `verify (macos-latest, 2/2)` of run 44189133429 untriageable
# -- its test step failed twice, the annotation said so, and which test failed could only be read from the
# log (#948) -- while the gate had already read the failing lines for the flake ledger. At most five
# names: a step that failed sixteen tests must not bury the annotation that names the step itself.
#
# The gate retries a red pass once, so a report that read only the final attempt's output dropped what the
# first one said. `verify (macos-latest, 2/2)` was red again in run 44466042157 and the check run carried
# only `the test (de-AT culture) step failed (exit 1, 782s)`: a first attempt that failed on a test and a
# retry whose session aborted leave no failing-test line in the output the report read (#951). Both
# outputs are therefore read, the pass's own exit status goes with them, and a pass without any test name
# names its failing modules and the verdict it ended with instead.
report_failed_tests() {
  local label="${1:-}" status="${2:-}" output="${3:-}" first="${4:-}"
  local name module verdict count=0

  if [ -z "$label" ]; then
    return 0
  fi
  while IFS= read -r name; do
    [ -n "$name" ] || continue
    printf '::error::verify: %s: %s failed\n' "$label" "$name"
    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
      printf -- '- failing test: %s (%s)\n' "$name" "$label" >> "$GITHUB_STEP_SUMMARY"
    fi
    count=$((count + 1))
    [ "$count" -lt 5 ] || break
  done < <(failed_test_names "$output" "$first" | awk '!seen[$0]++')
  if [ "$count" -gt 0 ]; then
    return 0
  fi
  while IFS= read -r module; do
    [ -n "$module" ] || continue
    printf '::error::verify: %s: %s\n' "$label" "$module"
    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
      printf -- '- failing module: %s (%s)\n' "$module" "$label" >> "$GITHUB_STEP_SUMMARY"
    fi
    count=$((count + 1))
    [ "$count" -lt 3 ] || break
  done < <(failed_modules "$output" "$first")
  verdict="$(failed_pass_verdict "$output")"
  if [ -z "$verdict" ]; then
    verdict="$(failed_pass_verdict "$first")"
  fi
  if [ -n "$verdict" ]; then
    printf '::error::verify: %s: the pass named no failing test (exit %s); it ended with: %s\n' \
      "$label" "$status" "$verdict"
    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
      printf -- '- failed pass: %s named no failing test (exit %s): %s\n' \
        "$label" "$status" "$verdict" >> "$GITHUB_STEP_SUMMARY"
    fi
  elif [ "$count" -eq 0 ]; then
    printf '::error::verify: %s: the pass named no failing test and printed no verdict (exit %s)\n' \
      "$label" "$status"
    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
      printf -- '- failed pass: %s named no failing test and printed no verdict (exit %s)\n' \
        "$label" "$status" >> "$GITHUB_STEP_SUMMARY"
    fi
  fi
}
