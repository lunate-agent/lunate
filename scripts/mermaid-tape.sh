#!/usr/bin/env bash
# Run one of the Mermaid examples that the VHS tapes capture. The Release build lives beside its
# project, so a tape never spends its sleep on a first compile; the fallback build covers a manual
# render from a fresh checkout.
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
DLL="$REPO/tests/tools/MermaidTape/bin/Release/net11.0/MermaidTape.dll"
if [ ! -f "$DLL" ]; then
    dotnet build "$REPO/tests/tools/MermaidTape/MermaidTape.csproj" -c Release --nologo >/dev/null
fi

exec dotnet "$DLL" "$@"
