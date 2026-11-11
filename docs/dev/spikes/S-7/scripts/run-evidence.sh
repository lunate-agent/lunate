#!/usr/bin/env bash
# S-7 spike (#625): produce the container execution evidence.
#
# Runs the real WriteTool/ReadTool/BashTool against a container whose workspace is bind-mounted at
# the same absolute path, and writes what it measured into evidence/. Nothing here is part of the
# build or the verify gate; see README.md.
#
#   ./scripts/run-evidence.sh              # podman, the default image
#   S7_ENGINE=docker ./scripts/run-evidence.sh
#
# Requires a running engine (on macOS: `podman machine start`) and the .NET SDK global.json pins.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
spike="$(cd "$here/.." && pwd)"
root="$(cd "$spike/../../.." && pwd)"
test -f "$root/AGENTS.md" || { printf '%s\n' "the repository root was not found above $spike"; exit 1; }
harness="$spike/S7.Harness/S7.Harness.csproj"
evidence="$spike/evidence"

engine="${S7_ENGINE:-podman}"
container="${S7_CONTAINER:-lunate-s7-evidence}"
image="${S7_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"
runs="${S7_RUNS:-15}"
dotnet="${DOTNET:-dotnet}"

mkdir -p "$evidence"

say() { printf '%s\n' "$*"; }

say "workspace: $root"
say "engine:    $engine"
say "container: $container"
say "image:     $image"

"$engine" version >/dev/null || { say "the engine is not running"; exit 1; }

say "build the harness"
MSBUILDDISABLENODEREUSE=1 "$dotnet" build "$harness" -c Release -nodeReuse:false -v q --nologo

run_mode() {
    local mode="$1"
    say "run: $mode"
    S7_ENGINE="$engine" S7_CONTAINER="$container" S7_IMAGE="$image" S7_RUNS="$runs" \
        S7_WORKSPACE="$root" "$dotnet" "$spike/S7.Harness/bin/Release/net10.0/S7.Harness.dll" "$mode" \
        | tee "$evidence/$mode.txt"
}

# A leftover container would hold a stale mount; a running one is reused as it is.
if "$engine" container exists "$container" >/dev/null 2>&1; then
    say "remove the leftover container"
    "$engine" rm -f "$container" >/dev/null
fi

say "start the container with the workspace mounted at the same absolute path"
"$engine" run -d --name "$container" \
    -v "$root:$root" -w "$root" -e HOME=/tmp \
    "$image" sh -c 'while true; do sleep 3600; done' >/dev/null

finish() {
    say "clean up the workspace probes"
    S7_CONTAINER="$container" S7_WORKSPACE="$root" \
        "$dotnet" "$spike/S7.Harness/bin/Release/net10.0/S7.Harness.dll" cleanup >/dev/null || true
    say "remove the container"
    "$engine" rm -f "$container" >/dev/null || true
}
trap finish EXIT

run_mode env
run_mode file-tool
run_mode bash-tool
run_mode startup

say "evidence written to $evidence"
