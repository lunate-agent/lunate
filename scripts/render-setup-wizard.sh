#!/usr/bin/env bash
# Render docs/media/setup-wizard.gif from the committed VHS tape.
#
# The recording is keyless: a local OpenAI-compatible stub answers model discovery and
# the model check, and LUNATE_HOME points at a throw-away directory. Nothing is written
# to the developer's real configuration.
#
# Requirements: vhs, ffmpeg, python3, and a Release build of lunate. Override the binary
# with LUNATE_BIN=/path/to/lunate when rendering from a published archive.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
BIN="${LUNATE_BIN:-$REPO/src/Lunate.Coding/bin/Release/net10.0/lunate}"
PORT="${LUNATE_DEMO_PORT:-18081}"
WORK="${LUNATE_DEMO_WORK:-/tmp/lunate-setup-wizard-demo}"
HOME_DIR="$WORK/home"
PROJECT="$WORK/project"
STUB="$REPO/docs/media/stubs/setup-wizard-provider.py"
GIF="$REPO/docs/media/setup-wizard.gif"
TAPE="$REPO/docs/media/setup-wizard.tape"

[ -x "$BIN" ] || { echo "missing $BIN — build first or set LUNATE_BIN" >&2; exit 1; }
[ -f "$STUB" ] || { echo "missing $STUB" >&2; exit 1; }
[ -f "$TAPE" ] || { echo "missing $TAPE" >&2; exit 1; }
command -v python3 >/dev/null || { echo "python3 missing" >&2; exit 1; }
command -v vhs >/dev/null || { echo "vhs missing: brew install vhs" >&2; exit 1; }

rm -rf "$WORK"
mkdir -p "$HOME_DIR" "$PROJECT"

python3 -u "$STUB" --port "$PORT" >"$WORK/provider.log" 2>&1 &
SERVER_PID=$!
cleanup() {
  kill "$SERVER_PID" 2>/dev/null || true
  wait "$SERVER_PID" 2>/dev/null || true
}
trap cleanup EXIT

for _ in $(seq 1 50); do
  if curl -fsS "http://127.0.0.1:$PORT/v1/models" >/dev/null 2>&1; then
    break
  fi
  sleep 0.1
done
curl -fsS "http://127.0.0.1:$PORT/v1/models" >/dev/null || {
  echo "stub provider did not start; see $WORK/provider.log" >&2
  exit 1
}

# The recording itself. `vhs` inherits this environment, so the tape's `lunate` is the published
# binary, its LUNATE_HOME is the throw-away directory and its provider is the stub — nothing on this
# machine's real configuration is read. The working directory is the project, deliberately not the
# home: a LUNATE_HOME inside the workspace is refused by the binary.
before=$(stat -f %m "$GIF" 2>/dev/null || echo 0)
# Run from the repository root: the tape's `Output docs/media/setup-wizard.gif` is relative to the
# working directory, and only the LUNATE_HOME has to stay out of the workspace (the binary refuses a
# home inside it), not the recording's own directory.
(
  cd "$REPO"
  PATH="$(dirname "$BIN"):$PATH" LUNATE_HOME="$HOME_DIR" LUNATE_DEMO_PORT="$PORT" \
    LUNATE_BIN="$BIN" vhs "$TAPE"
)
after=$(stat -f %m "$GIF" 2>/dev/null || echo 0)
if [ "$after" = "$before" ]; then
  echo "vhs did not write $GIF" >&2
  exit 1
fi
echo "wrote $GIF ($(stat -f %z "$GIF") bytes)"
