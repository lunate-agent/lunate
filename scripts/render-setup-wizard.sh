#!/usr/bin/env bash
# Render docs/media/setup-wizard.gif from the committed VHS tape.
#
# The recording is keyless: a local OpenAI-compatible stub answers model discovery and
# the model check, and LUNATE_HOME points at a throw-away directory. Nothing is written
# to the developer's real configuration. The session runs in an empty scratch project so
# this repository's project skills (and their trust prompt) never enter the recording.
#
# Requirements: vhs, ffmpeg, python3, and a Release build of lunate. Override the binary
# with LUNATE_BIN=/path/to/lunate when rendering from a published archive.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
BIN="${LUNATE_BIN:-$REPO/src/Lunate.Coding/bin/Release/net10.0/lunate}"
# The tape types this port into the custom endpoint field, so the stub and the recording must
# agree on it. It is fixed rather than overridable on purpose: an override would only move the
# stub and leave the tape pointing at the old port.
PORT=18081
TMP_ROOT="${TMPDIR:-${HOME}/.cache}"
WORK="${LUNATE_DEMO_WORK:-${TMP_ROOT%/}/lunate-setup-wizard-demo}"
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
mkdir -p "$HOME_DIR" "$PROJECT/docs/media"

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

# The recording itself. `vhs` inherits this environment, so the tape's `lunate` is the Release
# binary, its LUNATE_HOME is the throw-away directory and its provider is the stub — nothing on
# this machine's real configuration is read. It runs in the empty scratch project, not the
# repository: a session in the repository finds its project skills and asks to trust them before
# the wizard, and the LUNATE_HOME must stay out of the workspace (the binary refuses a home inside
# it). The tape's relative `Output` lands in the scratch project; the script moves it into place.
before=$(stat -f %m "$GIF" 2>/dev/null || echo 0)
(
  cd "$PROJECT"
  PATH="$(dirname "$BIN"):$PATH" LUNATE_HOME="$HOME_DIR" vhs "$TAPE"
)
RENDERED="$PROJECT/docs/media/setup-wizard.gif"
[ -f "$RENDERED" ] || {
  echo "vhs did not write $RENDERED" >&2
  exit 1
}
mv "$RENDERED" "$GIF"
after=$(stat -f %m "$GIF" 2>/dev/null || echo 0)
if [ "$after" = "$before" ]; then
  echo "vhs did not write $GIF" >&2
  exit 1
fi
echo "wrote $GIF ($(stat -f %z "$GIF") bytes)"
echo "stub log: $WORK/provider.log"
