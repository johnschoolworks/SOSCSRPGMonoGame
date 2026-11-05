#!/usr/bin/env bash
set -Eeuo pipefail
cd "$(dirname "$0")"
GAME=SoscMonoGame/SoscMonoGame.csproj
TESTS=SoscMonoGame.EngineChecks/SoscMonoGame.EngineChecks.csproj
DOTNET=/usr/bin/dotnet
env -u DOTNET_ROOT "$DOTNET" restore "$GAME"
env -u DOTNET_ROOT "$DOTNET" restore "$TESTS"
env -u DOTNET_ROOT "$DOTNET" build "$GAME" --no-restore
env -u DOTNET_ROOT "$DOTNET" build "$TESTS" --no-restore
env -u DOTNET_ROOT "$DOTNET" run --project "$TESTS" --no-build
env -u DOTNET_ROOT SDL_VIDEODRIVER=x11 "$DOTNET" run --project "$GAME" --no-build
