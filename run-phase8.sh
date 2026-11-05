#!/usr/bin/env bash
set -Eeuo pipefail
cd "$(dirname "$0")"
GAME="SoscMonoGame/SoscMonoGame.csproj"
TESTS="SoscMonoGame.EngineChecks/SoscMonoGame.EngineChecks.csproj"
DOTNET="/usr/bin/dotnet"
[[ -x "$DOTNET" ]] || { echo "ERROR: $DOTNET missing" >&2; exit 1; }
echo "=== SDK ==="
env -u DOTNET_ROOT "$DOTNET" --version
echo "=== RESTORE ==="
env -u DOTNET_ROOT "$DOTNET" restore "$GAME"
env -u DOTNET_ROOT "$DOTNET" restore "$TESTS"
echo "=== BUILD GAME ==="
env -u DOTNET_ROOT "$DOTNET" build "$GAME" --no-restore -warnaserror
echo "=== BUILD ENGINE CHECKS ==="
env -u DOTNET_ROOT "$DOTNET" build "$TESTS" --no-restore -warnaserror
echo "=== ENGINE CHECKS ==="
env -u DOTNET_ROOT "$DOTNET" run --project "$TESTS" --no-build
echo "=== AUTOMATED AUDIT PASSED ==="
if [[ "${1:-}" == "--no-gui" ]]; then exit 0; fi
echo "=== LAUNCH MONOGAME ==="
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 "$DOTNET" run --project "$GAME" --no-build
