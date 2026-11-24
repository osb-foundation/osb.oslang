#!/usr/bin/env bash
# O8.14 — Reproducible build for OSLANG.EXE (freestanding NativeAOT, Ring 3).
#
# Pipeline (replaces the former C-file build):
#   1. dotnet build      -> OSLANG.dll (C# + MiniCoreLib + osbkt + Engine.Core)
#   2. ilc               -> OSLANG.o   (native; emits the [RuntimeExport] symbols)
#   3. gcc -c            -> userspace.o (osbkt/crt0.S), osb_tls_raw.o (osb_tls_raw.S)
#   4. ld.lld            -> OSLANG.elf (linked at 0x2000000 via osbkt/user.ld)
#   5. objcopy + mkosbx  -> OSLANG.EXE (OSBX-wrapped flat image)
#
# No C is compiled. The allocation/TLS helpers are C# [RuntimeExport] methods
# (OsbRuntimeExports.cs); only the FS-segment primitives (osb_tls_raw.S) remain
# as Assembly, because C# cannot express segment-relative access.
#
# Requires: dotnet (10.x), ilc (10.0.11), gcc, ld.lld, objcopy, python3.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
OSB_ROOT="$(cd "$ROOT/../../../.." && pwd)"
OBJ="${TMPDIR:-/tmp}/osb1410-oslang"
OUT_EXE="$ROOT/OSLANG.EXE"
DLL="$ROOT/bin/Release/net10.0/OSLANG.dll"

ILC="$(ls -1d "$HOME"/.nuget/packages/runtime.linux-x64.microsoft.dotnet.ilcompiler/10.0.11/tools/ilc 2>/dev/null | tail -n1)"
if [[ -z "$ILC" || ! -x "$ILC" ]]; then
    echo "ERROR: ilc 10.0.11 not found in ~/.nuget/packages." >&2
    exit 1
fi

mkdir -p "$OBJ"

echo "[1/5] dotnet build OSLANG.dll ..."
dotnet build -c Release "$ROOT/Osb.Engine.Osb.csproj" -v q -nologo
test -f "$DLL" || { echo "ERROR: $DLL not produced"; exit 1; }

echo "[2/5] ilc -> OSLANG.o ..."
"$ILC" "$DLL" -o "$OBJ/OSLANG.o" \
    --systemmodule OSLANG \
    --targetarch x64 --targetos linux \
    -O -g --directpinvoke:osbnative \
    --map "$OBJ/OSLANG.map"

echo "[3/5] assemble crt0.S + osb_tls_raw.S ..."
gcc -c -nostdlib -ffreestanding -o "$OBJ/userspace.o" "$OSB_ROOT/osbkt/crt0.S"
gcc -c -nostdlib -ffreestanding -o "$OBJ/osb_tls_raw.o" "$ROOT/osbnative/osb_tls_raw.S"

echo "[4/5] link OSLANG.elf ..."
# --unresolved-symbols=ignore-all matches the committed baseline: the only
# unresolved symbol is `fmod` (a pre-existing freestanding dependency on libm
# that is outside O8.14 scope; see the O8.14 report).
ld.lld -T "$OSB_ROOT/osbkt/user.ld" -o "$OBJ/OSLANG.elf" \
    "$OBJ/userspace.o" "$OBJ/OSLANG.o" "$OBJ/osb_tls_raw.o" \
    --no-relax \
    --defsym="osb_startup=OSLANG__Module___StartupCodeMain" \
    --unresolved-symbols=ignore-all

echo "[5/5] objcopy + mkosbx -> OSLANG.EXE ..."
objcopy -O binary "$OBJ/OSLANG.elf" "$OBJ/OSLANG.bin"
python3 "$OSB_ROOT/osbkt/mkosbx.py" "$OBJ/OSLANG.bin" "$OUT_EXE" 0 >/dev/null

echo "OK -> $OUT_EXE ($(stat -c %s "$OUT_EXE") bytes)"