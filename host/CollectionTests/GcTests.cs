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
        TestUnrootedObjectCollected();
        TestNodeListReferencedObjectCollectedByEngineHeap();
        TestRootedObjectNotCollected();
    }

    private static void TestUnrootedObjectCollected()
    {
        using var provider = new GcTestMemoryProvider();
        var heap = new EngineHeap(provider);

        var str = heap.AllocString("gc-test-unrooted");
        var val = Value.String(str);
        // val is NOT added as root, NOT stored in NodeList

        int freed = heap.Collect();
        Program.Check("GC.UnrootedObjectCollected", freed == 1);
    }

    private static void TestNodeListReferencedObjectCollectedByEngineHeap()
    {
        using var provider = new GcTestMemoryProvider();
        var heap = new EngineHeap(provider);

        var str = heap.AllocString("gc-test-nodelist");
        var val = Value.String(str);

        var list = new NodeList<Value>();
        list.Add(val);
        // val is stored in NodeList<T> but NOT added as EngineHeap root

        int freed = heap.Collect();
        // EngineHeap does NOT scan NodeList<T>'s backing array,
        // so the object is collected despite being referenced by NodeList<T>
        Program.Check("GC.NodeListReferencedCollectedByEngineHeap", freed == 1);
    }

    private static void TestRootedObjectNotCollected()
    {
        using var provider = new GcTestMemoryProvider();
        var heap = new EngineHeap(provider);

        var str = heap.AllocString("gc-test-rooted");
        var val = Value.String(str);

        // Add as EngineHeap root
        heap.AddRoot(val);

        // Also store in NodeList<T>
        var list = new NodeList<Value>();
        list.Add(val);

        int freed = heap.Collect();
        Program.Check("GC.RootedObjectNotCollected", freed == 0);

        heap.RemoveRoot(val);
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
