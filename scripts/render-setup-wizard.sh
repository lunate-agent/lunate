#!/usr/bin/env bash
# Launch `lunate setup` for docs/media/tapes/setup-wizard.tape.
#
# The tape types this script's path into the recorded terminal, so it is the one step between the
# tape and the wizard. It starts a local OpenAI-compatible stub that serves the recorded OpenCode Go
# discovery response (docs/media/fixtures/setup-wizard-models.json) and answers the model check, then
# runs the wizard in a throw-away home outside the workspace. Render the GIF through
# scripts/render-media.sh, never by hand: that script is the only renderer.
#
# Requirements: vhs, python3, and a Release build of lunate. The recording itself is from the
# published v0.3.0 binary; pass it with LUNATE_BIN=/path/to/lunate to reproduce that provenance.
# Nothing in the recording reaches a real provider or reads a real key.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
BIN="${LUNATE_BIN:-$REPO/src/Lunate.Coding/bin/Release/net10.0/lunate}"
# The tape types this port into the custom endpoint field, so the stub and the recording must agree
# on it. It is fixed rather than overridable on purpose: an override would only move the stub and
# leave the tape pointing at the old port.
PORT=18081
TMP_ROOT="${TMPDIR:-${HOME}/.cache}"
WORK="${LUNATE_DEMO_WORK:-${TMP_ROOT%/}/lunate-setup-wizard-demo}"
HOME_DIR="$WORK/home"
PROJECT="$WORK/project"
STUB="$REPO/docs/media/stubs/setup-wizard-provider.py"

[ -x "$BIN" ] || { echo "missing $BIN — build first or set LUNATE_BIN" >&2; exit 1; }
[ -f "$STUB" ] || { echo "missing $STUB" >&2; exit 1; }
command -v python3 >/dev/null || { echo "python3 missing" >&2; exit 1; }

rm -rf "$WORK"
mkdir -p "$HOME_DIR" "$PROJECT"
# A scratch repository root makes the workspace self-contained. Without it, a $TMPDIR that happens
# to sit inside another repository would make the wizard refuse the home as "inside the workspace".
git -C "$PROJECT" init -q

python3 -u "$STUB" --port "$PORT" >"$WORK/provider.log" 2>&1 &
SERVER_PID=$!
cleanup() {
  kill "$SERVER_PID" 2>/dev/null || true
  wait "$SERVER_PID" 2>/dev/null || true
}
trap cleanup EXIT

ready=0
for _ in $(seq 1 50); do
  if curl -fsS "http://127.0.0.1:$PORT/v1/models?probe" >/dev/null 2>&1; then
    ready=1
    break
  fi
  sleep 0.1
done
[ "$ready" = 1 ] || {
  echo "stub provider did not start; see $WORK/provider.log" >&2
  exit 1
}

# The wizard runs in the foreground: the tape drives it through this terminal, and the trap above
# stops the stub when the session quits. The empty scratch project keeps this repository's project
# skills (and their trust prompt) out of the recording.
cd "$PROJECT"
HOME="$HOME_DIR" LUNATE_HOME="$HOME_DIR" "$BIN" setup
