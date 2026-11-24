/* O8.14: Low-level FS-segment TLS primitives for the OSLANG freestanding runtime.
 *
 * These are the genuine machine-level operations that C# cannot express: reading
 * and writing the FS segment base (IA32_FS_BASE) and the per-task TLS object the
 * kernel provisions at TP-0xD0 (osb.kernel/Processes/Scheduler.cs InitTaskTls,
 * osb.kernel/Memory/Layout.cs). The C# [RuntimeExport] wrappers in
 * OsbRuntimeExports.cs call these via [DllImport("osbnative")].
 *
 * Layout (verified against kernel + probe.S):
 *   [FS:0]        = TP (self-pointer)
 *   [FS:0 - 0xD0] = combined_limit  (tls_CurrentThread + 0)
 *   [FS:0 - 0xC8] = alloc_ptr       (tls_CurrentThread + 8)
 *
 * The symbols are named osb_tls_raw_* so they do not collide with the C#
 * RuntimeExport symbols osb_tls_* that the runtime/ABI expects. probe.S is NOT
 * modified or linked here; this file is the sole Assembly source for OSLANG.EXE.
 */

.section .tbss,"awT",@nobits
.align 8
.global tls_CurrentThread
.hidden tls_CurrentThread
.type tls_CurrentThread, @object
.size tls_CurrentThread, 208
tls_CurrentThread:
    .zero 208

.section .text

/* ulong osb_tls_raw_tp(): return FS:0. */
.global osb_tls_raw_tp
.type osb_tls_raw_tp, @function
osb_tls_raw_tp:
    movq %fs:0, %rax
    ret

/* ulong osb_tls_raw_get_alloc(): *(ulong*)(FS:0 - 0xC8). */
.global osb_tls_raw_get_alloc
.type osb_tls_raw_get_alloc, @function
osb_tls_raw_get_alloc:
    movq %fs:0, %rax
    movq -0xC8(%rax), %rax
    ret

/* ulong osb_tls_raw_get_limit(): *(ulong*)(FS:0 - 0xD0). */
.global osb_tls_raw_get_limit
.type osb_tls_raw_get_limit, @function
osb_tls_raw_get_limit:
    movq %fs:0, %rax
    movq -0xD0(%rax), %rax
    ret

/* void osb_tls_raw_set_alloc(ulong v): *(ulong*)(FS:0 - 0xC8) = v. */
.global osb_tls_raw_set_alloc
.type osb_tls_raw_set_alloc, @function
osb_tls_raw_set_alloc:
    movq %fs:0, %rax
    movq %rdi, -0xC8(%rax)
    ret

/* void osb_tls_raw_set_limit(ulong v): *(ulong*)(FS:0 - 0xD0) = v. */
.global osb_tls_raw_set_limit
.type osb_tls_raw_set_limit, @function
osb_tls_raw_set_limit:
    movq %fs:0, %rax
    movq %rdi, -0xD0(%rax)
    ret

.section .note.GNU-stack, "", @progbits