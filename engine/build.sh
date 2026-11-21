#!/usr/bin/env bash
# Build script for O3.5.3 M8.7: Reproducible NativeAOT Build for Osb.Engine.NativeAOT.
#
# Pipeline:
#   1. Verify required tools are available (dotnet, file, readelf)
#   2. Report SDK and runtime versions
#   3. dotnet publish (csc + ilc) compiles C# against BCL -> native ELF executable
#   4. Verify output is a native ELF for linux-x64
#   5. Execute the published executable
#
# Requires: dotnet (10.x SDK), file, readelf
#
# This script publishes only Osb.Engine.NativeAOT. It does NOT touch MiniCoreLib,
# the kernel, or any freestanding/OSBEXE path.
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
PROJECT="$ROOT/Osb.Engine.NativeAOT.csproj"
PUBLISH_DIR="$ROOT/bin/Release/net10.0/linux-x64/publish"
OUT_EXE="$PUBLISH_DIR/OsbEngineNativeAot"

# --- Locate dotnet ---
if ! command -v dotnet >/dev/null 2>&1; then
    echo "ERROR: dotnet not found in PATH. Install .NET SDK 10.0.400." >&2
    exit 1
fi

if ! command -v file >/dev/null 2>&1; then
    echo "ERROR: 'file' not found in PATH. Install the 'file' package." >&2
    exit 1
fi

if ! command -v readelf >/dev/null 2>&1; then
    echo "ERROR: 'readelf' not found in PATH. Install binutils." >&2
    exit 1
fi

echo "=== Osb.Engine.NativeAOT Build (M8.7) ==="
echo
echo "Using dotnet: $(command -v dotnet)"
dotnet --version
echo
echo "Target project: $PROJECT"
echo "Target RID: linux-x64"
echo "Configuration: Release"
echo

# --- Step 1: Publish NativeAOT executable ---
echo "[1/4] Publishing NativeAOT executable ..."
dotnet publish -c Release -r linux-x64 "$PROJECT" -v q -nologo

if [[ ! -f "$OUT_EXE" ]]; then
    echo "ERROR: $OUT_EXE not produced by publish." >&2
    exit 1
fi

echo "  Executable: $OUT_EXE"
echo "  Size:       $(stat -c %s "$OUT_EXE") bytes"
echo

# --- Step 2: Verify output is a native ELF ---
echo "[2/4] Verifying ELF format and architecture ..."
FILE_OUT=$(file "$OUT_EXE")
echo "  file: $FILE_OUT"

if ! echo "$FILE_OUT" | grep -qi "ELF"; then
    echo "ERROR: $OUT_EXE is not an ELF executable." >&2
    exit 1
fi

if ! echo "$FILE_OUT" | grep -qi "x86-64"; then
    echo "ERROR: $OUT_EXE is not an x86-64 executable." >&2
    exit 1
fi

# Verify ELF magic bytes
MAGIC=$(head -c 4 "$OUT_EXE" | od -An -tx1 | tr -d ' ')
if [[ "$MAGIC" != "7f454c46" ]]; then
    echo "ERROR: $OUT_EXE does not have ELF magic bytes (got $MAGIC)." >&2
    exit 1
fi

echo "  ELF magic: OK"
echo "  Architecture: x86-64 (verified)"
echo

# --- Step 3: Execute the published executable ---
echo "[3/4] Executing published executable ..."
"$OUT_EXE"
EXEC_RC=$?
echo "  Exit code: $EXEC_RC"
if [[ $EXEC_RC -ne 0 ]]; then
    echo "ERROR: $OUT_EXE exited with non-zero code $EXEC_RC." >&2
    exit $EXEC_RC
fi
echo

# --- Step 4: Done ---
echo "[4/4] Build complete."
echo "OK -> $OUT_EXE"
