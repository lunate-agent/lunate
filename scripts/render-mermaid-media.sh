#!/usr/bin/env bash
# Render the Mermaid example tapes to the PNGs the docs embed and CI uploads. Each tape drives
# tests/tools/MermaidTape, which renders its example through Mermaid.Terminal; VHS captures the
# terminal, and the last frame becomes the still. The GIF is an intermediate and is not committed.
#
# Requirements: vhs 0.12.1, ttyd and ffmpeg on PATH, and a Release build of the tape renderer
# (`dotnet build tests/tools/MermaidTape/MermaidTape.csproj -c Release`). CI installs the same
# versions in .github/workflows/mermaid-media.yml.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
. "$REPO/scripts/lib.sh"
TFM="$(target_framework "$REPO")"
MEDIA="$REPO/docs/media/mermaid"
VHS="${VHS:-vhs}"

command -v "$VHS" >/dev/null || { echo "vhs missing"; exit 1; }
command -v ffmpeg >/dev/null || { echo "ffmpeg missing"; exit 1; }
[ -f "$REPO/tests/tools/MermaidTape/bin/Release/${TFM}/MermaidTape.dll" ] || {
    echo "build first: dotnet build tests/tools/MermaidTape/MermaidTape.csproj -c Release"
    exit 1
}

cd "$REPO"
mkdir -p "$MEDIA" "$REPO/artifacts/mermaid"
for name in flowchart-td flowchart-lr sequence fallback; do
    echo "==> $name"
    "$VHS" "docs/media/mermaid/tapes/$name.tape"
    # The still is the last captured frame, after the drawing has finished.
    ffmpeg -y -loglevel error -i "$MEDIA/$name.gif" -vf reverse -frames:v 1 "$MEDIA/$name.png"
    rm -f "$MEDIA/$name.gif"
    cp "$MEDIA/$name.png" "$REPO/artifacts/mermaid/$name.png"
done

echo "mermaid media render complete"
