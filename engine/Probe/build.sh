#!/usr/bin/env bash
# Build script for O3.5.3 M8.3: Direct NativeAOT toolchain for Engine.Core Probe.
#
# Pipeline:
#   1. dotnet build (csc) compiles C# against MiniCoreLib -> OsbEngineProbe.dll (managed IL)
#   2. ilc (NativeAOT compiler) turns IL into a native object file
#   3. ld.lld links the object file into a native ELF executable
#
# Requires: dotnet (10.x SDK), ld.lld, NuGet packages (ilc, NativeAOT runtime pack)
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
OBJ="$ROOT/obj/nativeaot"
OUT_EXE="$OBJ/OsbEngineProbe"

mkdir -p "$OBJ"

# --- Locate ILCompiler ---
ILC="$(ls -1d "$HOME"/.nuget/packages/runtime.linux-x64.microsoft.dotnet.ilcompiler/*/tools/ilc 2>/dev/null | sort -V | tail -n1)"
if [[ -z "$ILC" || ! -x "$ILC" ]]; then
    echo "ERROR: ilc (NativeAOT compiler) not found in ~/.nuget/packages." >&2
    echo "       Build the project once (dotnet build) or restore the SDK to fetch it." >&2
    exit 1
fi
echo "Using ILC: $ILC"

# --- Locate linker ---
if ! command -v ld.lld >/dev/null 2>&1; then
    echo "ERROR: ld.lld not found in PATH." >&2
    exit 1
fi
echo "Using linker: $(command -v ld.lld)"

# --- Step 1: Compile C# to managed DLL ---
echo "[1/4] Compiling C# (managed, mini-CoreLib) ..."
dotnet build -c Release "$ROOT/Osb.Engine.Probe.csproj" -v q -nologo
DLL="$ROOT/bin/Release/net10.0/OsbEngineProbe.dll"
test -f "$DLL" || { echo "ERROR: $DLL not produced"; exit 1; }
echo "  DLL: $DLL"

# --- Step 2: NativeAOT code generation with ilc ---
echo "[2/4] NativeAOT (ilc) ..."
# --noscan: skip the IL scanner (avoids needing ReadyToRunHelper intrinsics
#           that MiniCoreLib does not yet provide). Produces unoptimized code.
"$ILC" "$DLL" -o "$OBJ/OsbEngineProbe.o" \
    --systemmodule OsbEngineProbe \
    --targetarch x64 --targetos linux \
    --noscan 2>&1 || {
    echo "ERROR: ilc failed." >&2
    echo "       ilc requires runtime intrinsics (System.Runtime.TypeCast, System.Runtime.RuntimeExports," >&2
    echo "       string constructors, etc.) that MiniCoreLib does not yet fully provide." >&2
    echo "       See O3.5.3-M8.2b-ARCHITECTURE-REPORT.md for analysis." >&2
    exit 1
}
test -f "$OBJ/OsbEngineProbe.o" || { echo "ERROR: $OBJ/OsbEngineProbe.o not produced"; exit 1; }
echo "  Object: $OBJ/OsbEngineProbe.o"

# --- Step 3: Link native executable ---
echo "[3/4] Linking native executable ..."
# The ilc object file needs crt1.o and libc to form a complete ELF executable.
# crt1.o provides the _start entry point that calls main().
CRT1=$(gcc -print-file-name=crt1.o 2>/dev/null || echo "")
LIBC=$(gcc -print-file-name=libc.so 2>/dev/null || echo "")
if [[ -z "$CRT1" || ! -f "$CRT1" ]]; then
    echo "ERROR: crt1.o not found. Install gcc or libc-dev." >&2
    exit 1
fi
ld.lld -o "$OUT_EXE" \
    "$CRT1" "$OBJ/OsbEngineProbe.o" \
    -lc --dynamic-linker /lib64/ld-linux-x86-64.so.2 2>&1 || {
    echo "ERROR: ld.lld linking failed." >&2
    exit 1
}
test -f "$OUT_EXE" || { echo "ERROR: $OUT_EXE not produced"; exit 1; }
echo "  Executable: $OUT_EXE"

# --- Step 4: Smoke test ---
echo "[4/4] Running smoke test ..."
"$OUT_EXE"
SMOKE_RC=$?
echo "  Exit code: $SMOKE_RC"
if [[ $SMOKE_RC -ne 0 ]]; then
    echo "WARN: Smoke test exited with non-zero code $SMOKE_RC." >&2
    echo "      This may indicate missing runtime intrinsics (GCHandle, etc.)." >&2
fi

echo "OK -> $OUT_EXE"
