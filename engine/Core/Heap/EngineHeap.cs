using System;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace Osb.Engine.Core.Heap;

/// <summary>
/// Engine heap with bump allocator for platform memory.
/// Managed object lifetime is owned by the runtime GC (NativeAOT or CLR).
/// EngineHeap no longer performs independent mark-sweep collection.
/// </summary>
public sealed class EngineHeap : IDisposable
{
    private readonly IMemoryProvider _memoryProvider;
    private int _bumpPtr;
    private readonly int _regionSize;

    private int _allocCount;
    private int _gcCycles;

    public EngineHeap(IMemoryProvider memoryProvider)
    {
        _memoryProvider = memoryProvider;
        _regionSize = memoryProvider.RegionSize;
        _bumpPtr = 0;
    }

    public int UsedBytes => _bumpPtr;
    public int CapacityBytes => _regionSize;
    public int FreeBytes => _regionSize - _bumpPtr;
    public int AllocCount => _allocCount;
    public int FreedCount => 0; // NativeAOT/CLR GC reclaims; EngineHeap does not sweep
    public int GcCycles => _gcCycles;

    public T Allocate<T>(Func<T> factory) where T : HeapObject
    {
        T obj = factory();
        if (obj == null) return null!;
        _allocCount++;
        return obj;
    }

    public HeapString AllocString(string text) => Allocate(() => new HeapString(text));
    public HeapArray AllocArray(int capacity) => Allocate(() => new HeapArray(capacity));
    public HeapClass AllocClass(string name, HeapClass? baseClass, string[] fieldNames, Lang.MethodDecl[] methods)
        => Allocate(() => new HeapClass(name, baseClass, fieldNames, methods));
    public HeapClosure AllocClosure(Lang.FunctionDecl function, Value[] captured)
        => Allocate(() => new HeapClosure(function, captured));
    public HeapObjectInstance AllocObject(HeapClass classDef)
        => Allocate(() => new HeapObjectInstance(classDef));

    /// <summary>
    /// Triggers garbage collection. In Hosted, invokes CLR GC.
    /// In NativeAOT, invokes the NativeAOT GC.
    /// EngineHeap no longer performs independent mark-sweep collection.
    /// </summary>
    public int Collect()
    {
        _gcCycles++;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return 0;
    }

    /// <summary>
    /// No-op: NativeAOT/CLR GC tracks managed references automatically.
    /// EngineHeap no longer maintains a separate root table.
    /// </summary>
    public void AddRoot(Value value) { }

    /// <summary>
    /// No-op: NativeAOT/CLR GC tracks managed references automatically.
    /// </summary>
    public void RemoveRoot(Value value) { }

    public void Dispose()
    {
        _memoryProvider.Reset();
        _bumpPtr = 0;
        _allocCount = 0;
    }
}
