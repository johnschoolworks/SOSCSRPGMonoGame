#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
./scripts/verify.sh
mkdir -p artifacts
dotnet publish TerminalRpg/TerminalRpg.csproj -c Release -r linux-x64 --self-contained false -o artifacts/linux-x64
( cd artifacts && tar -czf soscsrpg-terminal-v1.0.0-rc1-linux-x64.tar.gz linux-x64 )
printf 'Release archive: artifacts/soscsrpg-terminal-v1.0.0-rc1-linux-x64.tar.gz\n'
