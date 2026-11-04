#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet --version
dotnet restore TerminalRpg/TerminalRpg.csproj
dotnet restore RegressionTests/RegressionTests.csproj
dotnet build TerminalRpg/TerminalRpg.csproj -c Release --no-restore
dotnet build RegressionTests/RegressionTests.csproj -c Release --no-restore
dotnet run --project RegressionTests/RegressionTests.csproj -c Release --no-build
printf 'Verification passed.\n'
