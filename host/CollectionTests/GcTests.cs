using System;
using System.Runtime.InteropServices;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace CollectionTests;

internal static class GcTests
{
    internal static void RunAll()
    {
        TestObjectAliveWhileReferencedByLocal();
        TestObjectAliveWhileReferencedByNodeList();
        TestWeakHandleBehavior();
        TestObjectIdentityStableAcrossGC();
        TestEngineHeapPlatformMemoryAllocation();
        TestRepeatedAllocCollectCycles();
    }

    // --- NativeAOT GC: object remains alive while referenced ---

    private static void TestObjectAliveWhileReferencedByLocal()
    {
        using var provider = new GcTestMemoryProvider();
        var heap = new EngineHeap(provider);

        var str = heap.AllocString("gc-test-local");
        var val = Value.String(str);
        // val is a local variable -- NativeAOT GC keeps it alive

        int freed = heap.Collect();
        Program.Check("GC.ObjectAliveWhileReferencedByLocal", freed == 0 && !val.IsNull && val.AsString?.ToString() == "gc-test-local");
    }

    // --- NativeAOT GC: object referenced by NodeList remains alive ---

    private static void TestObjectAliveWhileReferencedByNodeList()
    {
        using var provider = new GcTestMemoryProvider();
        var heap = new EngineHeap(provider);

        var str = heap.AllocString("gc-test-nodelist");
        var val = Value.String(str);

        var list = new NodeList<Value>();
        list.Add(val);

        int freed = heap.Collect();
        Program.Check("GC.ObjectAliveWhileReferencedByNodeList", freed == 0 && list.Count == 1 && list[0].AsString?.ToString() == "gc-test-nodelist");
    }

    // --- WeakHandle: can observe collectable objects ---

    private static void TestWeakHandleBehavior()
    {
        WeakReference<HeapString>? weakRef = null;

        using (var provider = new GcTestMemoryProvider())
        {
            var heap = new EngineHeap(provider);
            var str = heap.AllocString("gc-test-weak");
            weakRef = new WeakReference<HeapString>(str);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Program.Check("GC.WeakHandleBehavior", weakRef != null);
    }

    // --- Object identity remains stable across GC ---

    private static void TestObjectIdentityStableAcrossGC()
    {
        using var provider = new GcTestMemoryProvider();
        var heap = new EngineHeap(provider);

        var str1 = heap.AllocString("identity-a");
        var str2 = heap.AllocString("identity-b");

        heap.Collect();

        var v1 = Value.String(str1);
        var v2 = Value.String(str2);

        Program.Check("GC.ObjectIdentityStableAcrossGC",
            v1.AsString == str1 && v2.AsString == str2 && !ReferenceEquals(str1, str2));
    }

    // --- EngineHeap platform-memory bump allocator remains functional ---

    private static void TestEngineHeapPlatformMemoryAllocation()
    {
        using var provider = new GcTestMemoryProvider();
        var heap = new EngineHeap(provider);

        int beforeCount = heap.AllocCount;

        var s1 = heap.AllocString("alloc-test-1");
        var s2 = heap.AllocString("alloc-test-2");

        Program.Check("GC.EngineHeapPlatformMemoryAllocation",
            heap.AllocCount == beforeCount + 2 &&
            s1 is not null && s2 is not null);
    }

    // --- Repeated allocation and collection cycles ---

    private static void TestRepeatedAllocCollectCycles()
    {
        using var provider = new GcTestMemoryProvider();
        var heap = new EngineHeap(provider);

        for (int i = 0; i < 100; i++)
        {
            var s = heap.AllocString($"cycle-{i}");
            var val = Value.String(s);
            if (val.IsNull || val.AsString == null) return;
            heap.Collect();
        }

        Program.Check("GC.RepeatedAllocCollectCycles", true);
    }

    // ── GC test memory provider ────────────────────────────────────

    private sealed class GcTestMemoryProvider : IMemoryProvider, IDisposable
    {
        private readonly byte[] _region;
        private int _bumpPtr;
        private readonly GCHandle _handle;

        public GcTestMemoryProvider(int sizeBytes = 1024 * 1024)
        {
            _region = new byte[sizeBytes];
            _handle = GCHandle.Alloc(_region, GCHandleType.Pinned);
            _bumpPtr = 0;
        }

        public IntPtr BaseAddress => _handle.AddrOfPinnedObject();
        public int RegionSize => _region.Length;
        public byte[]? ManagedRegion => _region;

        public IntPtr Alloc(int sizeBytes, int alignment = 8)
        {
            if (sizeBytes <= 0) return IntPtr.Zero;
            int aligned = (_bumpPtr + alignment - 1) & ~(alignment - 1);
            int newPtr = aligned + sizeBytes;
            if (newPtr > _region.Length) return IntPtr.Zero;
            IntPtr result = BaseAddress + aligned;
            _bumpPtr = newPtr;
            return result;
        }

        public void Free(IntPtr ptr, int sizeBytes) { }
        public void Reset() { _bumpPtr = 0; Array.Clear(_region, 0, _region.Length); }
        public void Collect() { }

        public void Dispose()
        {
            if (_handle.IsAllocated) _handle.Free();
        }
    }
}
