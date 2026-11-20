#!/usr/bin/env bash
# Build script for O3.5.3 M8.3.5: Official .NET NativeAOT runtime for Probe.
#
# Pipeline:
#   1. dotnet publish (csc + ilc) compiles C# against BCL -> native ELF executable
#
# Requires: dotnet (10.x SDK)
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
PUBLISH_DIR="$ROOT/bin/Release/net10.0/linux-x64/publish"
OUT_EXE="$PUBLISH_DIR/OsbEngineProbe"

# --- Locate dotnet ---
if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: dotnet not found in PATH." >&2
    exit 1
fi
echo "Using dotnet: $(command -v dotnet)"
dotnet --version

# --- Step 1: Publish NativeAOT executable ---
echo "[1/2] Publishing NativeAOT executable ..."
dotnet publish -c Release -r linux-x64 "$ROOT/Osb.Engine.Probe.csproj" -v q -nologo
test -f "$OUT_EXE" || { echo "ERROR: $OUT_EXE not produced"; exit 1; }
echo "  Executable: $OUT_EXE"

# --- Step 2: Smoke test ---
echo "[2/2] Running smoke test ..."
"$OUT_EXE"
SMOKE_RC=$?
echo "  Exit code: $SMOKE_RC"
if [[ $SMOKE_RC -ne 0 ]]; then
    echo "ERROR: Smoke test exited with non-zero code $SMOKE_RC." >&2
    exit $SMOKE_RC
fi

echo "OK -> $OUT_EXE"
