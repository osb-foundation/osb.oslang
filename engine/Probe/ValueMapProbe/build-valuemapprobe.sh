#!/usr/bin/env bash
# Card 2 continuation - build the freestanding ValueMapProbe.EXE.
#
# Isolates the OSLANG 'variables read back NULL' symptom at the ValueMap layer
# (no interpreter). Exercises direct object[]/bool[]/byte[] round-trip and the
# exact ValueMap insert/lookup/replace/grow path with all ValueKind tags.
#
# Pipeline (mirrors engine/host/Host.Osb/build-oslang.sh):
#   dotnet build -> ilc -> gcc(crt0.S, osb_tls_raw.S, gcstatics.S)
#   -> ld.lld -> objcopy + mkosbx
#
# Requires: dotnet 10.x, ilc 10.0.11, gcc, ld.lld, objcopy, python3.
# Produces: <this dir>/ValueMapProbe.EXE (stage into the OSB image to run).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
OSB_ROOT="$(cd "$ROOT/../../../.." && pwd)"
OBJ="${TMPDIR:-/tmp}/osb-valuemapprobe"
OUT_EXE="$ROOT/ValueMapProbe.EXE"
DLL="$ROOT/bin/Release/net10.0/ValueMapProbe.dll"

ILC="$(ls -1d "$HOME"/.nuget/packages/runtime.linux-x64.microsoft.dotnet.ilcompiler/10.0.11/tools/ilc 2>/dev/null | tail -n1)"
if [[ -z "$ILC" || ! -x "$ILC" ]]; then
    echo "ERROR: ilc 10.0.11 not found in ~/.nuget/packages." >&2
    exit 1
fi

mkdir -p "$OBJ"

echo "[1/5] dotnet build ValueMapProbe.dll ..."
dotnet build -c Release "$ROOT/ValueMapProbe.csproj" -v q -nologo
test -f "$DLL" || { echo "ERROR: $DLL not produced"; exit 1; }

echo "[2/5] ilc -> ValueMapProbe.o ..."
"$ILC" "$DLL" -o "$OBJ/ValueMapProbe.o" \
    --systemmodule ValueMapProbe \
    --targetarch x64 --targetos linux \
    -O -g --directpinvoke:osbnative \
    --map "$OBJ/ValueMapProbe.map"

echo "[3/5] assemble crt0.S + osb_tls_raw.S + gcstatics.S ..."
gcc -c -nostdlib -ffreestanding -o "$OBJ/userspace.o" "$OSB_ROOT/osbkt/crt0.S"
gcc -c -nostdlib -ffreestanding -o "$OBJ/osb_tls_raw.o" "$ROOT/../../host/Host.Osb/osbnative/osb_tls_raw.S"
gcc -c -nostdlib -ffreestanding -o "$OBJ/gcstatics.o" "$ROOT/../../host/Host.Osb/osbnative/gcstatics.S"

echo "[4/5] link ValueMapProbe.elf ..."
ld.lld -T "$OSB_ROOT/osbkt/user.ld" -o "$OBJ/ValueMapProbe.elf" \
    "$OBJ/userspace.o" "$OBJ/ValueMapProbe.o" "$OBJ/osb_tls_raw.o" "$OBJ/gcstatics.o" \
    --no-relax \
    --defsym="osb_startup=ValueMapProbe__Module___StartupCodeMain" \
    --unresolved-symbols=ignore-all

echo "[5/5] objcopy + mkosbx -> ValueMapProbe.EXE ..."
objcopy -O binary "$OBJ/ValueMapProbe.elf" "$OBJ/ValueMapProbe.bin"
python3 "$OSB_ROOT/osbkt/mkosbx.py" "$OBJ/ValueMapProbe.bin" "$OUT_EXE" 0 >/dev/null
echo "OK -> $OUT_EXE ($(stat -c %s "$OUT_EXE") bytes)"