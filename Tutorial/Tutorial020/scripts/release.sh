#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

REPO_ROOT="$(git rev-parse --show-toplevel)"
VERSION="v1.0.0"
OUTPUT="artifacts/linux-x64"
ARCHIVE="artifacts/soscsrpg-terminal-${VERSION}-linux-x64.tar.gz"

./scripts/verify.sh

mkdir -p "$OUTPUT"

dotnet publish TerminalRpg/TerminalRpg.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o "$OUTPUT"

cp "$REPO_ROOT/LICENSE" "$OUTPUT/LICENSE"
cp "$REPO_ROOT/THIRD-PARTY-NOTICES.md" \
  "$OUTPUT/THIRD-PARTY-NOTICES.md"

tar -C artifacts \
  -czf "$ARCHIVE" \
  linux-x64

echo "Release archive: $ARCHIVE"
