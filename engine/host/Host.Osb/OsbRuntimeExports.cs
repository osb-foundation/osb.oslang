// O8.14 — OSLANG freestanding runtime allocation/TLS helpers implemented as C#
// [RuntimeExport] methods. These replace the former osb_alloc.c and osb_tls.c.
//
// The [RuntimeExport] mechanism (System.Runtime.RuntimeExportAttribute, recognized
// by ILC's UnmanagedEntryPointsRootProvider) causes ILC to emit each method as a
// plain native symbol with the exact name below, resolving the R_X86_64_PLT32
// relocations the generated NativeAOT code (and MiniCoreLib DllImports) emit. This
// is the mechanism already proven by MiniCoreLib's RhpFallbackFailFast / RhpPInvoke
// exports in this build (O8.3.7).
//
// The FS-segment TLS primitives (osb_tls_raw_*) live in osb_tls_raw.S; C# has no
// syntax for segment-relative access, so the osb_tls_* RuntimeExports below are thin
// wrappers over those Assembly primitives. The TLS layout is the verified OSB layout
// (kernel Scheduler.InitTaskTls + Layout.cs): [FS:0]=TP, [FS:0-0xD0]=combined_limit,
// [FS:0-0xC8]=alloc_ptr. The legacy clang __thread layout in the old osb_tls.c was
// incompatible with the kernel and is intentionally NOT reproduced.
//
// ABI NOTE (important): the former osb_alloc.c used INCORRECT signatures (e.g.
// RhpNewFast(size), RhNewString(length)). The real NativeAOT ABI — verified against
// osb.runtime RuntimeImports.cs / RuntimeExports.cs / portable.cpp and the MiniCoreLib
// DllImports — is MethodTable*-based:
//   RhpNewFast(MethodTable* mt)                       size = mt->BaseSize
//   RhpNewArrayFast(MethodTable* mt, int length)      size = mt->BaseSize + length*mt->ComponentSize
//   RhpNewPtrArrayFast(MethodTable* mt, int length)   == RhpNewArrayFast(mt, length)
//   RhNewString(MethodTable* mt, nint length)         == RhpNewArrayFast(mt, length) (tail call)
// MethodTable layout (Runtime/inc/MethodTable.h): m_usComponentSize (uint16) @ +0,
// m_uBaseSize (uint32) @ +4. This matches the TAlloc test's MtBuf and MiniCoreLib.
//
// No GC, no relocation, no new heap: the bump-pointer arena semantics are preserved.

using System;
using System.Runtime.InteropServices;

namespace Osb.OSLang.Runtime
{
    internal static unsafe class OsbRuntimeExports
    {
        // ---- Assembly FS-segment primitives (osb_tls_raw.S) ----
        [DllImport("osbnative", EntryPoint = "osb_tls_raw_tp")]
        private static extern ulong osb_tls_raw_tp();

        [DllImport("osbnative", EntryPoint = "osb_tls_raw_get_alloc")]
        private static extern ulong osb_tls_raw_get_alloc();

        [DllImport("osbnative", EntryPoint = "osb_tls_raw_get_limit")]
        private static extern ulong osb_tls_raw_get_limit();

        [DllImport("osbnative", EntryPoint = "osb_tls_raw_set_alloc")]
        private static extern void osb_tls_raw_set_alloc(ulong v);

        [DllImport("osbnative", EntryPoint = "osb_tls_raw_set_limit")]
        private static extern void osb_tls_raw_set_limit(ulong v);

        // ---- TLS ABI (expected by the runtime / OsbMain DllImports) ----
        [System.Runtime.RuntimeExport("osb_tls_tp")]
        public static ulong osb_tls_tp() => osb_tls_raw_tp();

        [System.Runtime.RuntimeExport("osb_tls_get_alloc")]
        public static ulong osb_tls_get_alloc() => osb_tls_raw_get_alloc();

        [System.Runtime.RuntimeExport("osb_tls_get_limit")]
        public static ulong osb_tls_get_limit() => osb_tls_raw_get_limit();

        [System.Runtime.RuntimeExport("osb_tls_set_alloc")]
        public static void osb_tls_set_alloc(ulong v) => osb_tls_raw_set_alloc(v);

        [System.Runtime.RuntimeExport("osb_tls_set_limit")]
        public static void osb_tls_set_limit(ulong v) => osb_tls_raw_set_limit(v);

        // ---- Allocation helpers (real NativeAOT MethodTable*-based ABI) ----

        // Bump-pointer allocation from the thread-local arena. Aligns size up to 8
        // bytes; returns NULL on exhaustion (preserves original behavior; no GC).
        private static void* BumpAlloc(ulong size)
        {
            ulong alloc = osb_tls_get_alloc();
            ulong limit = osb_tls_get_limit();
            ulong aligned = (size + 7UL) & ~7UL;
            if (alloc + aligned > limit)
            {
                return null;
            }
            void* obj = (void*)alloc;
            osb_tls_set_alloc(alloc + aligned);
            return obj;
        }

        // RhpNewFast(MethodTable* mt): size = mt->BaseSize (uint32 @ +4).
        // The real helper (portable.cpp RhpNewFast) stamps the object's MethodTable
        // at +0 (set_EEType); the generated `new` code relies on the helper for this,
        // so the stub must write it too. Without it the object header MT is NULL.
        [System.Runtime.RuntimeExport("RhpNewFast")]
        public static void* RhpNewFast(void* mt)
        {
            byte* m = (byte*)mt;
            ulong baseSize = *(uint*)(m + 4);
            void* obj = BumpAlloc(baseSize);
            if (obj != null) *(void**)obj = mt;
            return obj;
        }

        // RhpNewArrayFast(MethodTable* mt, int length):
        //   size = mt->BaseSize + length * mt->ComponentSize (uint16 @ +0).
        // The real helper (portable.cpp RhpNewArrayFast) also stamps the MethodTable
        // at +0 and the element count at +8 (Array::m_Length, uint32). The generated
        // `new T[N]` code relies on the helper for both, so the stub must set them;
        // otherwise every array reports Length == 0 and the engine breaks.
        [System.Runtime.RuntimeExport("RhpNewArrayFast")]
        public static void* RhpNewArrayFast(void* mt, int length)
        {
            byte* m = (byte*)mt;
            ulong baseSize = *(uint*)(m + 4);
            ulong compSize = *(ushort*)m;
            ulong size = baseSize + (ulong)length * compSize;
            void* obj = BumpAlloc(size);
            if (obj != null)
            {
                *(void**)obj = mt;
                *(uint*)((byte*)obj + 8) = (uint)length;
            }
            return obj;
        }

        // RhpNewPtrArrayFast(MethodTable* mt, int length): == RhpNewArrayFast.
        [System.Runtime.RuntimeExport("RhpNewPtrArrayFast")]
        public static void* RhpNewPtrArrayFast(void* mt, int length) => RhpNewArrayFast(mt, length);

        // RhNewString(MethodTable* mt, nint length): tail-calls RhpNewArrayFast
        // (verified in portable.cpp). The string MethodTable carries BaseSize=header
        // and ComponentSize=2 (char), so the same array formula yields the correct
        // string size; the length field is at offset 8 (caller sets it, as MiniCoreLib
        // String.FastAllocateString does).
        [System.Runtime.RuntimeExport("RhNewString")]
        public static void* RhNewString(void* mt, nint length) => RhpNewArrayFast(mt, (int)length);

        // ---- Write barriers: no-op for the no-GC runtime (verified: no GC linked) ----
        [System.Runtime.RuntimeExport("RhpAssignRef")]
        public static void RhpAssignRef(void** dst, void* src) { *dst = src; }

        [System.Runtime.RuntimeExport("RhpCheckedAssignRef")]
        public static void RhpCheckedAssignRef(void** dst, void* src) { *dst = src; }

        [System.Runtime.RuntimeExport("RhpByRefAssignRef")]
        public static void RhpByRefAssignRef(void** dst, void* src) { *dst = src; }

        // Exception throwing: the original C halts (hlt; while(1){}). hlt is
        // privileged in Ring3, so preserve the halt intent as an infinite loop
        // (matching the C while(1){} and probe.S's no-return intent). No fake success.
        [System.Runtime.RuntimeExport("RhpThrowEx")]
        public static void RhpThrowEx(void* exception)
        {
            while (true) { }
        }

        // Initial dynamic interface dispatch: required by the 98 InterfaceDispatchCell
        // entries (R_X86_64_64 relocations). Preserve the verified NULL-return behavior.
        [System.Runtime.RuntimeExport("RhpInitialDynamicInterfaceDispatch")]
        public static void* RhpInitialDynamicInterfaceDispatch(void* obj, void* interfaceType) => null;
    }
}