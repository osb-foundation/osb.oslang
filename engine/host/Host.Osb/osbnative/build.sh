#!/bin/bash
# O8.14: Build the OSB freestanding native helper library for OSLANG.
#
# The former osb_alloc.c and osb_tls.c have been replaced by C# [RuntimeExport]
# methods (OsbRuntimeExports.cs) and the FS-segment primitives in osb_tls_raw.S.
# This script now assembles only the genuinely low-level Assembly (FS segment
# access, which C# cannot express) into libosbnative.a. No C is compiled.
set -e

OUT_DIR="."
OBJS=""

# Assemble osb_tls_raw.S (FS-segment TLS primitives; the C# RuntimeExport
# wrappers osb_tls_* call these via [DllImport("osbnative")]).
echo "Assembling osb_tls_raw.S..."
gcc -c -nostdlib -ffreestanding -o osb_tls_raw.o osb_tls_raw.S
OBJS="$OBJS osb_tls_raw.o"

# Create static library
echo "Creating libosbnative.a..."
ar rcs libosbnative.a $OBJS

echo "Done. Library created at $OUT_DIR/libosbnative.a"
echo "Objects: $OBJS"