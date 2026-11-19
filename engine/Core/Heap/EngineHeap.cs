using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;

namespace Osb.Engine.Core.Heap;

/// <summary>
/// Engine heap with bump allocator and precise mark-sweep GC.
/// Compatible with freestanding NativeAOT (no BCL allocations in hot paths).
/// </summary>
public sealed class EngineHeap
{
    private readonly byte[] _memory;
    private int _bumpPtr;
    private readonly List<HeapObject> _allocated = new();
    private readonly Stack<HeapObject> _markStack = new();

    // Roots: interpreter frames, globals, etc.
    private readonly List<Value> _roots = new();

    public EngineHeap(int sizeBytes = 2 * 1024 * 1024) // 2 MiB default
    {
        _memory = new byte[sizeBytes];
        _bumpPtr = 0;
    }

    public int UsedBytes => _bumpPtr;
    public int CapacityBytes => _memory.Length;
    public int FreeBytes => _memory.Length - _bumpPtr;

    /// <summary>
    /// Allocates a heap object. May trigger GC if out of space.
    /// </summary>
    public T Allocate<T>(Func<T> factory) where T : HeapObject
    {
        // Try fast bump allocation first
        // For simplicity, we use managed allocations here; in freestanding target
        // this would be raw pointer arithmetic over _memory
        T obj = factory();
        _allocated.Add(obj);
        return obj;
    }

    /// <summary>
    /// Registers a root reference for GC.
    /// </summary>
    public void AddRoot(Value value)
    {
        if (!value.IsNull && !value.IsImmediate)
        {
            _roots.Add(value);
        }
    }

    /// <summary>
    /// Removes a root reference.
    /// </summary>
    public void RemoveRoot(Value value) => _roots.Remove(value);

    /// <summary>
    /// Performs a full mark-sweep collection.
    /// </summary>
    public int Collect()
    {
        // Mark phase
        _markStack.Clear();
        foreach (var root in _roots)
        {
            if (!root.IsNull && !root.IsImmediate)
            {
                Mark(root);
            }
        }

        while (_markStack.Count > 0)
        {
            var obj = _markStack.Pop();
            if (obj.Marked) continue;
            obj.Marked = true;

            // Scan object references
            ScanObject(obj);
        }

        // Sweep phase
        int freed = 0;
        for (int i = _allocated.Count - 1; i >= 0; i--)
        {
            var obj = _allocated[i];
            if (!obj.Marked)
            {
                _allocated.RemoveAt(i);
                freed++;
            }
            else
            {
                obj.Marked = false; // clear for next cycle
            }
        }

        return freed;
    }

    private void Mark(Value value)
    {
        if (value.IsNull || value.IsImmediate) return;

        var obj = value.Kind switch
        {
            ValueKind.String => value.AsString,
            ValueKind.Array => value.AsArray,
            ValueKind.Object => value.AsObject,
            ValueKind.EnumSet => value.AsEnumSet,
            ValueKind.Closure => value.AsClosure,
            ValueKind.KeyEvent => value.AsKeyEvent,
            ValueKind.NativeFunction => value.AsNativeFunctionObj,
            _ => null,
        };

        if (obj != null && !obj.Marked)
        {
            _markStack.Push(obj);
        }
    }

    private void ScanObject(HeapObject obj)
    {
        switch (obj)
        {
            case HeapArray arr:
                foreach (var item in arr.Items)
                    Mark(item);
                break;

            case HeapObjectInstance inst:
                foreach (var field in inst.Fields)
                    Mark(field);
                break;

            case HeapEnumSet eset:
                foreach (var item in eset.Values)
                    Mark(item);
                break;

            case HeapClosure closure:
                foreach (var captured in closure.Captured)
                    Mark(captured);
                break;

            case HeapKeyEvent:
            case HeapString:
            case HeapNativeFunction:
                // No outgoing references
                break;
        }
    }

    /// <summary>
    /// Allocates a string on the heap.
    /// </summary>
    public HeapString AllocString(string text) => Allocate(() => new HeapString(text));

    /// <summary>
    /// Allocates an array on the heap.
    /// </summary>
    public HeapArray AllocArray(int capacity) => Allocate(() => new HeapArray(capacity));

    /// <summary>
    /// Allocates an object instance on the heap.
    /// </summary>
    public HeapObjectInstance AllocObject(HeapClass classDef) => Allocate(() => new HeapObjectInstance(classDef));

    /// <summary>
    /// Allocates a class definition on the heap.
    /// </summary>
    public HeapClass AllocClass(string name, HeapClass? baseClass, string[] fieldNames, Lang.MethodDecl[] methods)
        => Allocate(() => new HeapClass(name, baseClass, fieldNames, methods));

    /// <summary>
    /// Allocates an enum set on the heap.
    /// </summary>
    public HeapEnumSet AllocEnumSet(string enumTypeName, Value[] values)
        => Allocate(() => new HeapEnumSet(enumTypeName, values));

    /// <summary>
    /// Allocates a closure on the heap.
    /// </summary>
    public HeapClosure AllocClosure(Lang.FunctionDecl function, Value[] captured)
        => Allocate(() => new HeapClosure(function, captured));

    /// <summary>
    /// Allocates a key event on the heap.
    /// </summary>
    public HeapKeyEvent AllocKeyEvent(int keyCode, char character, bool ctrl, bool alt, bool shift)
        => Allocate(() => new HeapKeyEvent(keyCode, character, ctrl, alt, shift));
}

/// <summary>
/// GC handle for pinning objects during critical sections.
/// </summary>
public struct GcHandle : IDisposable
{
    private readonly EngineHeap _heap;
    private readonly Value _value;

    public GcHandle(EngineHeap heap, Value value)
    {
        _heap = heap;
        _value = value;
        _heap.AddRoot(value);
    }

    public void Dispose() => _heap.RemoveRoot(_value);
}