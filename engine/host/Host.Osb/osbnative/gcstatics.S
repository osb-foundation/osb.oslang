/* O8.15 / M5 — NativeAOT GC-statics base initialization for freestanding Ring 3.
 *
 * ILC emits __GCSTATICS_<Type> symbols as placeholders that do not contain
 * valid pointers. In a normal NativeAOT build the runtime
 * (RhInitializeRuntime / __nativeaot_init) patches these to point at
 * allocated, zeroed GC-static storage. The freestanding crt0 -> StartupCodeMain
 * path never runs that runtime, so every __GCSTATICS_<Type> base pointer holds
 * an invalid value and the first access to a reference-type static field
 * (e.g. Keywords._map) faults with a page fault at (garbage_base + 8).
 *
 * The generated code accesses a static reference field as:
 *     P = *(__GCSTATICS_<Type>)   // base pointer
 *     field = *(P + 8)            // first reference static field
 *
 * This module provides zeroed storage for every emitted GC-static block and
 * patches each __GCSTATICS_<Type> to point at its own block, so P+8 lands in
 * valid, null-initialized storage. No field offsets are hardcoded: the +8 is
 * the offset the NativeAOT codegen itself emits, and this module only
 * establishes the base pointers the runtime would normally establish.
 *
 * Called from OsbMain.Main() after TLS/heap setup and before the OSLANG
 * engine starts, matching the required ordering:
 *   TLS -> heap -> NativeAOT -> GC-statics -> cctors -> engine startup.
 */

.section .data, "aw", @progbits
.align 8
gcstatic_block_keywords:   .zero 16
gcstatic_block_parser:     .zero 16
gcstatic_block_datetime_c: .zero 16
gcstatic_block_math_c:     .zero 16
gcstatic_block_empty_md:   .zero 16
gcstatic_block_empty_str:  .zero 16

.section .text
.align 4
.globl osb_gcstatics_init
.type osb_gcstatics_init, @function
osb_gcstatics_init:
    /* Anchor on __GCSTATICS_Keywords (simple symbol name). ILC lays the
     * __GCSTATICS_<Type> symbols out contiguously, 8 bytes apart, in
     * symbol-table order:
     *   +0  Keywords, +8 Parser, +16 DateTimeNamespace___c,
     *   +24 MathNamespace___c, +32 EmptyArray_1<MethodDecl>,
     *   +40 EmptyArray_1<String>.
     * Each entry is patched with the address of its own zeroed block so
     * P+8 lands in valid, null-initialized storage. */
    lea gcstatic_block_keywords(%rip), %rax
    lea __GCSTATICSOSLANG_Osb_Engine_Core_Lang_Keywords(%rip), %rcx
    mov %rax, (%rcx)

    lea gcstatic_block_parser(%rip), %rax
    lea __GCSTATICSOSLANG_Osb_Engine_Core_Lang_Parser(%rip), %rcx
    mov %rax, (%rcx)

    lea gcstatic_block_datetime_c(%rip), %rax
    lea __GCSTATICSOSLANG_Osb_Engine_Core_Eval_DateTimeNamespace___c(%rip), %rcx
    mov %rax, (%rcx)

    lea gcstatic_block_math_c(%rip), %rax
    lea __GCSTATICSOSLANG_Osb_Engine_Core_Eval_MathNamespace___c(%rip), %rcx
    mov %rax, (%rcx)

    /* The two EmptyArray symbols contain '<'/'>' which the assembler cannot
     * name directly; they are contiguous with MathNamespace___c, so patch
     * them by symbol-table offset from that entry. */
    lea gcstatic_block_empty_md(%rip), %rax
    lea __GCSTATICSOSLANG_Osb_Engine_Core_Eval_MathNamespace___c(%rip), %rcx
    mov %rax, 8(%rcx)

    lea gcstatic_block_empty_str(%rip), %rax
    lea __GCSTATICSOSLANG_Osb_Engine_Core_Eval_MathNamespace___c(%rip), %rcx
    mov %rax, 16(%rcx)

    ret
.size osb_gcstatics_init, .-osb_gcstatics_init

.section .note.GNU-stack, "", @progbits
