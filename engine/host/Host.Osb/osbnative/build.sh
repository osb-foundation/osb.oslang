#!/bin/bash
set -e

OUT_DIR="."
OBJS=""

# Compile osb_tls.c
echo "Compiling osb_tls.c..."
gcc -c -O2 -fPIC osb_tls.c -o osb_tls.o
OBJS="$OBJS osb_tls.o"

# Compile osb_alloc.c
echo "Compiling osb_alloc.c..."
gcc -c -O2 -fPIC osb_alloc.c -o osb_alloc.o
OBJS="$OBJS osb_alloc.o"

# Create static library
echo "Creating libosbnative.a..."
ar rcs libosbnative.a $OBJS

echo "Done. Library created at $OUT_DIR/libosbnative.a"
echo "Objects: $OBJS"
